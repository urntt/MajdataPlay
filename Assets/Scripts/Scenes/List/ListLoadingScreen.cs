using Cysharp.Threading.Tasks;
using MajdataPlay.Diagnostics;
using MajdataPlay.Misc.Base;
using MajdataPlay.Scenes.Game;
using System;
using System.Globalization;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#nullable enable
namespace MajdataPlay.Scenes.List
{
    /// <summary>
    /// 列表式选歌点“游玩”后的加载界面：浅色风格，显示曲绘、难度、曲名、曲与谱、最佳成绩和加载进度。
    /// 盖在一切之上，跨场景保留；游戏打开转场时随转场淡出，露出游戏画面后销毁
    /// </summary>
    internal sealed class ListLoadingScreen : MonoBehaviour
    {
        const float FADE_IN_SEC = 0.3f;
        const float FADE_OUT_SEC = 0.3f;
        const float TIMEOUT_SEC = 60f;
        /// <summary>
        /// 一直没切到游戏场景（例如进入游戏失败）时，过这么久就淡出
        /// </summary>
        const float NO_SWITCH_TIMEOUT_SEC = 5f;

        static readonly Color PAGE_COLOR = new Color32(0xF5, 0xF6, 0xF8, 0xFF);
        static readonly Color TEXT_COLOR = new Color32(0x20, 0x26, 0x30, 0xFF);
        static readonly Color MUTED_COLOR = new Color32(0x78, 0x83, 0x91, 0xFF);
        static readonly Color LINE_COLOR = new Color32(0xE4, 0xE7, 0xEC, 0xFF);
        static readonly Color ACCENT_COLOR = new Color32(0x58, 0x6F, 0xDD, 0xFF);

        ISongDetail _song = null!;
        ChartLevel _level;
        Color _levelColor;
        TMP_FontAsset? _font;
        Sprite? _cover;
        readonly CancellationTokenSource _cts = new();

        RectTransform _root = null!;
        CanvasGroup _group = null!;
        RectTransform _content = null!;
        Image _coverImage = null!;
        /// <summary>
        /// 全局摄像机会画的 layer（与原加载界面相同）；用 Overlay 时为 -1，不改 layer
        /// </summary>
        int _layer = -1;
        bool _isRevealing = false;
        TextMeshProUGUI _loadingText = null!;
        RectTransform _barTrack = null!;
        RectTransform _barSegment = null!;
        Vector2 _lastSize = Vector2.zero;
        /// <summary>
        /// 转场上没有文字时显示的“加载中…”（预先取好，加载期间不再分配）
        /// </summary>
        string _fallbackLoadingText = string.Empty;

        float _elapsed = 0f;
        float _fadeInAlpha = 0f;
        float _fadeOutAlpha = 1f;
        bool _isFadingOut = false;
        bool _hasSeenGame = false;
        bool _hasSeenClosedInGame = false;

        /// <param name="cover">详情页已加载的曲绘；为空时自己加载</param>
        public static void Show(ISongDetail song, ChartLevel level, Color levelColor, Sprite? cover, TMP_FontAsset? font)
        {
            try
            {
                var obj = new GameObject("ListLoadingScreen");
                DontDestroyOnLoad(obj);
                var screen = obj.AddComponent<ListLoadingScreen>();
                screen.Init(song, level, levelColor, cover, font);
            }
            catch (Exception e)
            {
                MajDebug.LogError(e);
            }
        }
        void Init(ISongDetail song, ChartLevel level, Color levelColor, Sprite? cover, TMP_FontAsset? font)
        {
            _song = song;
            _level = level;
            _levelColor = levelColor;
            _cover = cover;
            _font = font;
            _fallbackLoadingText = "MAJTEXT_LIST_LOADING".i18n();

            var canvasObject = new GameObject("ListLoadingCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            // 由全局摄像机渲染，和原加载界面（GlobalCanvas，GlobalMask 层）在同一次渲染里，盖在它上面。
            // 用 Overlay 时，着色器预热前后有几帧只有摄像机画面，会闪出原加载界面。不加 GraphicRaycaster，不拦截触摸
            var camera = SceneSwitcher.MainCamera;
            var switcher = MajInstances.SceneSwitcher;
            if (camera != null && switcher != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 50f;
                canvas.sortingLayerName = "Debug";
                canvas.sortingOrder = 100;
                _layer = switcher.gameObject.layer;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // 在列表页（30）之上，暂停菜单（1000）之下
                canvas.sortingOrder = 40;
            }
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)canvasObject.transform;
            _group = canvasObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var background = CreateImage(_root, PAGE_COLOR, 0f);
            Stretch(background.rectTransform);
            _content = CreateRect("Content", _root);
            Stretch(_content);
            ApplyLayer(transform);

            if (_cover == null)
            {
                LoadCoverAsync(_cts.Token).Forget();
            }
        }
        async UniTaskVoid LoadCoverAsync(CancellationToken token)
        {
            try
            {
                var sprite = await _song.GetCoverAsync(false, token: token);
                await UniTask.SwitchToMainThread();
                if (token.IsCancellationRequested || this == null)
                {
                    return;
                }
                _cover = sprite;
                if (_coverImage != null)
                {
                    SetCover(_coverImage, sprite);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                MajDebug.LogWarning($"[ListLoadingScreen] Failed to load cover of {_song.Title}: {e.Message}");
            }
        }
        void Update()
        {
            var deltaTime = Time.unscaledDeltaTime;
            _elapsed += deltaTime;
            _fadeInAlpha = Mathf.Min(1f, _fadeInAlpha + deltaTime / FADE_IN_SEC);

            var scene = SceneSwitcher.CurrentScene;
            var progress = SceneSwitcher.TransitionProgress;
            var revealAlpha = 1f;
            if (scene == MajScenes.Game)
            {
                _hasSeenGame = true;
                if (progress >= 0.999f)
                {
                    _hasSeenClosedInGame = true;
                }
                else if (_hasSeenClosedInGame && (_isRevealing || IsLoadingTextEmpty()))
                {
                    // 游戏清空加载文字、开始打开转场：随转场淡出，露出游戏画面。
                    // 加载途中转场进度即使短暂变化，也不会提前变透明
                    _isRevealing = true;
                    revealAlpha = progress;
                    if (progress <= 0.001f)
                    {
                        Destroy(gameObject);
                        return;
                    }
                }
            }
            else if (_hasSeenGame || scene != MajScenes.List || (_elapsed > NO_SWITCH_TIMEOUT_SEC && progress < 0.5f))
            {
                // 进入游戏失败回到了别的场景，或一直没有切换场景
                _isFadingOut = true;
            }
            if (_elapsed > TIMEOUT_SEC)
            {
                _isFadingOut = true;
            }
            if (_isFadingOut)
            {
                _fadeOutAlpha -= deltaTime / FADE_OUT_SEC;
                if (_fadeOutAlpha <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            _group.alpha = Mathf.Min(_fadeInAlpha, Mathf.Min(revealAlpha, _fadeOutAlpha));

            UpdateLoadingText();
            UpdateProgressBar();
        }
        void LateUpdate()
        {
            // 屏幕方向或尺寸变化时重新排版（下落式会在加载中切到横屏）
            var size = _root.rect.size;
            if (size.x > 0f && size.y > 0f && (size - _lastSize).sqrMagnitude > 1f)
            {
                _lastSize = size;
                Layout(size.x, size.y);
            }
            // 曲名滚动时会新建子对象，同样要放到摄像机会画的 layer 上
            ApplyLayer(_root);
        }
        void OnDestroy()
        {
            _cts.Cancel();
        }
        void UpdateLoadingText()
        {
            if (_loadingText == null)
            {
                return;
            }
            var switcher = MajInstances.SceneSwitcher;
            var text = switcher != null ? switcher.LoadingText : string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                text = _fallbackLoadingText;
            }
            if (_loadingText.text != text)
            {
                _loadingText.text = text;
            }
            // 平时是白字（在浅色背景上看不见），换成灰色；出错时的红字等保留原色
            var color = switcher != null ? switcher.LoadingTextColor : Color.white;
            _loadingText.color = IsWhite(color) ? MUTED_COLOR : color;
        }
        static bool IsWhite(Color color)
        {
            return color.r > 0.95f && color.g > 0.95f && color.b > 0.95f;
        }
        /// <summary>
        /// 不确定进度的加载条：一小段在轨道里来回移动
        /// </summary>
        void UpdateProgressBar()
        {
            if (_barTrack == null || _barSegment == null)
            {
                return;
            }
            var trackWidth = _barTrack.rect.width;
            var segmentWidth = trackWidth * 0.28f;
            var t = (Mathf.Sin(_elapsed * 2.4f) + 1f) * 0.5f;
            _barSegment.sizeDelta = new Vector2(segmentWidth, _barSegment.sizeDelta.y);
            _barSegment.anchoredPosition = new Vector2(t * (trackWidth - segmentWidth), 0f);
        }
        /// <summary>
        /// 横屏：左边大曲绘，右边难度、曲名、曲与谱、最佳成绩；竖屏：上下排列、居中。底部是加载文字和加载条
        /// </summary>
        void Layout(float width, float height)
        {
            ClearChildren(_content);
            var isLandscape = width >= height * 1.1f;
            var difficulty = ScoreExchange.GetDifficultyName(_level);
            var levels = _song.Levels;
            var levelIndex = (int)_level;
            var levelText = levelIndex < levels.Length ? levels[levelIndex] : string.Empty;
            var tagText = string.IsNullOrEmpty(levelText) ? difficulty : $"{difficulty} {levelText}";
            var designers = _song.Designers;
            var designer = levelIndex < designers.Length && !string.IsNullOrWhiteSpace(designers[levelIndex]) ? designers[levelIndex] : "—";
            var artist = string.IsNullOrWhiteSpace(_song.Artist) ? "—" : _song.Artist;
            var credits = $"{"MAJTEXT_LIST_LABEL_ARTIST".i18n()} {artist}    {"MAJTEXT_LIST_LABEL_DESIGNER".i18n()} {designer}";

            float coverSize;
            float coverX;
            float coverY;
            float infoX;
            float infoY;
            float infoWidth;
            TextAlignmentOptions alignment;
            if (isLandscape)
            {
                coverSize = Mathf.Min(height * 0.5f, width * 0.3f, 520f);
                infoWidth = Mathf.Min(width - coverSize - 56f - 160f, 820f);
                var contentWidth = coverSize + 56f + infoWidth;
                coverX = (width - contentWidth) * 0.5f;
                coverY = (height - coverSize) * 0.5f - 40f;
                infoX = coverX + coverSize + 56f;
                infoY = coverY + Mathf.Max(0f, (coverSize - 330f) * 0.5f);
                alignment = TextAlignmentOptions.MidlineLeft;
            }
            else
            {
                coverSize = Mathf.Min(width * 0.6f, height * 0.36f, 560f);
                coverX = (width - coverSize) * 0.5f;
                coverY = height * 0.14f;
                infoWidth = Mathf.Min(width - 80f, 900f);
                infoX = (width - infoWidth) * 0.5f;
                infoY = coverY + coverSize + 40f;
                alignment = TextAlignmentOptions.Center;
            }

            // 柔和投影与曲绘同一圆角、中心对齐、四周等宽外扩
            const float COVER_SHADOW_BLUR = 18f;
            var coverShadow = CreateImage(_content, new Color(0.1f, 0.13f, 0.25f, 0.16f), 0f);
            UiSprites.ApplySoftShadow(coverShadow, 22f, COVER_SHADOW_BLUR);
            PlaceTopLeft(coverShadow.rectTransform, coverX - COVER_SHADOW_BLUR, coverY - COVER_SHADOW_BLUR,
                         coverSize + COVER_SHADOW_BLUR * 2f, coverSize + COVER_SHADOW_BLUR * 2f);
            _coverImage = LightUi.CreateRoundedCover(_content, 22f, PAGE_COLOR, out var coverFrame);
            PlaceTopLeft(coverFrame, coverX, coverY, coverSize, coverSize);
            if (_cover != null)
            {
                SetCover(_coverImage, _cover);
            }

            // 难度标签：难度色 12% 透明度的底，难度色粗体文字
            var tagWidth = 210f;
            var tagX = isLandscape ? infoX : infoX + (infoWidth - tagWidth) * 0.5f;
            var tag = CreateImage(_content, new Color(_levelColor.r, _levelColor.g, _levelColor.b, 0.12f), 12f);
            PlaceTopLeft(tag.rectTransform, tagX, infoY, tagWidth, 46f);
            var tagLabel = CreateText(tag.rectTransform, tagText, 20f, _levelColor, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(tagLabel.rectTransform, 4f);
            tagLabel.enableAutoSizing = true;
            tagLabel.fontSizeMin = 12f;
            tagLabel.fontSizeMax = 20f;

            var title = CreateText(_content, _song.Title, 44f, TEXT_COLOR, alignment, FontStyles.Bold);
            PlaceTopLeft(title.rectTransform, infoX, infoY + 62f, infoWidth, 68f);
            if (isLandscape)
            {
                title.gameObject.AddComponent<TMPMarqueeText>();
            }
            var creditText = CreateText(_content, credits, 21f, MUTED_COLOR, alignment, FontStyles.Normal);
            PlaceTopLeft(creditText.rectTransform, infoX, infoY + 134f, infoWidth, 34f);

            var bestLabel = CreateText(_content, "MAJTEXT_LOADING_BEST".i18n(), 17f, MUTED_COLOR, alignment, FontStyles.Normal);
            PlaceTopLeft(bestLabel.rectTransform, infoX, infoY + 196f, infoWidth, 28f);
            var score = ScoreManager.GetScore(_song, _level);
            var bestRow = CreateRect("Best", _content);
            PlaceTopLeft(bestRow, infoX, infoY + 226f, infoWidth, 64f);
            var bestLayout = bestRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            bestLayout.spacing = 14f;
            bestLayout.childAlignment = isLandscape ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            bestLayout.childControlWidth = true;
            bestLayout.childControlHeight = true;
            bestLayout.childForceExpandWidth = false;
            bestLayout.childForceExpandHeight = false;
            if (score is null || score.PlayCount <= 0)
            {
                AddFittedText(bestRow, "MAJTEXT_SCORE_UNPLAYED".i18n(), 30f, MUTED_COLOR, FontStyles.Bold);
            }
            else
            {
                var achievement = score.Acc.DX;
                var grade = GetGrade(achievement);
                AddFittedText(bestRow, achievement.ToString("0.0000", CultureInfo.InvariantCulture) + "%", 40f, TEXT_COLOR, FontStyles.Bold);
                AddBadge(bestRow, grade, GetGradeColor(grade), 22f);
                var combo = GetComboText(score.ComboState);
                if (!string.IsNullOrEmpty(combo))
                {
                    AddBadge(bestRow, combo, score.ComboState is ComboState.APPlus or ComboState.AP ? Hex(0xC98A00) : Hex(0x1E9E52), 20f);
                }
            }

            // 底部：加载文字与加载条
            var barWidth = Mathf.Min(width - 160f, 560f);
            var bottomY = height - Mathf.Max(110f, height * 0.12f);
            _loadingText = CreateText(_content, string.Empty, 20f, MUTED_COLOR, TextAlignmentOptions.Bottom, FontStyles.Normal);
            _loadingText.enableWordWrapping = true;
            PlaceTopLeft(_loadingText.rectTransform, (width - Mathf.Min(width - 80f, 1200f)) * 0.5f, bottomY - 96f, Mathf.Min(width - 80f, 1200f), 84f);
            var track = CreateImage(_content, LINE_COLOR, 3f);
            _barTrack = track.rectTransform;
            PlaceTopLeft(_barTrack, (width - barWidth) * 0.5f, bottomY, barWidth, 6f);
            var segment = CreateImage(_barTrack, ACCENT_COLOR, 3f);
            _barSegment = segment.rectTransform;
            _barSegment.anchorMin = new Vector2(0f, 0f);
            _barSegment.anchorMax = new Vector2(0f, 1f);
            _barSegment.pivot = new Vector2(0f, 0.5f);
            _barSegment.sizeDelta = new Vector2(barWidth * 0.28f, 0f);
            ApplyLayer(transform);
            UpdateLoadingText();
            UpdateProgressBar();
        }
        static bool IsLoadingTextEmpty()
        {
            var switcher = MajInstances.SceneSwitcher;
            return switcher == null || string.IsNullOrWhiteSpace(switcher.LoadingText);
        }
        /// <summary>
        /// 由摄像机渲染时，所有节点都要放在摄像机会画的 layer 上（新建的对象默认在 Default 层）
        /// </summary>
        void ApplyLayer(Transform node)
        {
            if (_layer < 0)
            {
                return;
            }
            node.gameObject.layer = _layer;
            for (var i = 0; i < node.childCount; i++)
            {
                ApplyLayer(node.GetChild(i));
            }
        }

        #region Helpers
        void AddBadge(RectTransform parent, string text, Color color, float fontSize)
        {
            var badge = CreateImage(parent, new Color(color.r, color.g, color.b, 0.14f), 8f);
            var label = CreateText(badge.transform, text, fontSize, color, TextAlignmentOptions.Center, FontStyles.Bold);
            label.overflowMode = TextOverflowModes.Overflow;
            Stretch(label.rectTransform);
            var element = badge.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = label.GetPreferredValues(text, float.PositiveInfinity, float.PositiveInfinity).x + fontSize;
            element.preferredHeight = fontSize * 1.7f;
        }
        void AddFittedText(RectTransform parent, string text, float fontSize, Color color, FontStyles style)
        {
            var label = CreateText(parent, text, fontSize, color, TextAlignmentOptions.MidlineLeft, style);
            label.overflowMode = TextOverflowModes.Overflow;
        }
        static string GetGrade(double achievement)
        {
            return achievement switch
            {
                >= 100.5 => "SSS+",
                >= 100 => "SSS",
                >= 99.5 => "SS+",
                >= 99 => "SS",
                >= 98 => "S+",
                >= 97 => "S",
                >= 94 => "AAA",
                >= 90 => "AA",
                >= 80 => "A",
                >= 75 => "BBB",
                >= 70 => "BB",
                >= 60 => "B",
                >= 50 => "C",
                _ => "D",
            };
        }
        static Color GetGradeColor(string grade)
        {
            return grade switch
            {
                "SSS+" or "SSS" => Hex(0xC98A00),
                "SS+" or "SS" => Hex(0xE8680F),
                "S+" or "S" => Hex(0xB59F00),
                "AAA" or "AA" or "A" => Hex(0x2F6FD6),
                _ => MUTED_COLOR,
            };
        }
        static string GetComboText(ComboState state)
        {
            return state switch
            {
                ComboState.APPlus => "AP+",
                ComboState.AP => "AP",
                ComboState.FCPlus => "FC+",
                ComboState.FC => "FC",
                _ => string.Empty,
            };
        }
        static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
        static void SetCover(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
        }
        static RectTransform CreateRect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return (RectTransform)obj.transform;
        }
        static Image CreateImage(Transform parent, Color color, float radius)
        {
            var rect = CreateRect("Image", parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            UiSprites.ApplyRounded(image, radius);
            return image;
        }
        TextMeshProUGUI CreateText(Transform parent, string content, float fontSize, Color color, TextAlignmentOptions alignment, FontStyles style)
        {
            var rect = CreateRect("Text", parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null)
            {
                text.font = _font;
            }
            CleanTmpMaterial.Apply(text);
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }
        static void ClearChildren(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }
        static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
        static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
        #endregion
    }
}
