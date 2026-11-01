using Cysharp.Threading.Tasks;
using MajdataPlay.Diagnostics;
using MajdataPlay.Misc.Base;
using MajdataPlay.Scenes.Game;
using System;
using System.Globalization;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
#nullable enable
namespace MajdataPlay.Scenes.List
{
    internal sealed partial class SongListBrowser
    {
        #region Widgets
        /// <summary>
        /// 圆形的爱心收藏按钮：收藏时为实心红色爱心，否则为灰色空心爱心
        /// </summary>
        Image CreateHeartButton(Transform parent, ISongDetail song, float size, Color background, out RectTransform buttonRect)
        {
            var backgroundImage = CreatePanel(parent, background, size * 0.5f);
            backgroundImage.gameObject.name = "Favorite";
            backgroundImage.raycastTarget = true;
            var button = backgroundImage.gameObject.AddComponent<Button>();
            button.targetGraphic = backgroundImage;
            SetButtonColors(button);
            button.onClick.AddListener(() => ToggleFavorite(song));
            var heart = CreateImage(backgroundImage.transform, Color.white, 0f);
            heart.preserveAspect = true;
            var heartRect = heart.rectTransform;
            heartRect.anchorMin = new Vector2(0.5f, 0.5f);
            heartRect.anchorMax = new Vector2(0.5f, 0.5f);
            heartRect.pivot = new Vector2(0.5f, 0.5f);
            heartRect.sizeDelta = new Vector2(size * 0.6f, size * 0.6f);
            heartRect.anchoredPosition = new Vector2(0f, -size * 0.02f);
            SetHeart(heart, SongStorage.IsInMyFavorites(song));
            buttonRect = backgroundImage.rectTransform;
            return heart;
        }
        /// <summary>
        /// 难度标签：非当前难度为白底、难度色文字、难度色细边框；当前难度为难度色 12% 透明度的底、难度色粗体文字
        /// </summary>
        Button CreateLevelTag(Transform parent, ChartLevel level, string label, bool isCurrent, float fontSize, UnityAction onClick)
        {
            var color = GetDifficultyColor(level);
            Image background;
            if (isCurrent)
            {
                background = CreatePanel(parent, WithAlpha(color, 0.12f), 10f);
            }
            else
            {
                background = CreatePanel(parent, color, 10f);
                var inner = CreatePanel(background.transform, PAPER_COLOR, 9f);
                Stretch(inner.rectTransform, 1.5f);
            }
            background.gameObject.name = "Level";
            background.raycastTarget = true;
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            SetButtonColors(button);
            button.onClick.AddListener(onClick);
            var text = CreateText(background.transform, label, fontSize, color, TextAlignmentOptions.Center, isCurrent ? FontStyles.Bold : FontStyles.Normal);
            Stretch(text.rectTransform, 3f);
            return button;
        }
        static void SetButtonColors(Button button)
        {
            var colors = button.colors;
            colors.highlightedColor = new Color(0.97f, 0.97f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.86f, 0.88f, 0.96f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            button.colors = colors;
        }
        /// <summary>
        /// 徽章：颜色 14% 透明度的底与该颜色的粗体文字，宽度跟随文字
        /// </summary>
        void AddBadge(RectTransform parent, string text, Color color, float fontSize)
        {
            var badge = CreatePanel(parent, WithAlpha(color, 0.14f), 8f);
            badge.gameObject.name = "Badge";
            var label = CreateText(badge.transform, text, fontSize, color, TextAlignmentOptions.Center, FontStyles.Bold);
            label.overflowMode = TextOverflowModes.Overflow;
            Stretch(label.rectTransform);
            var element = badge.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = label.GetPreferredValues(text, float.PositiveInfinity, float.PositiveInfinity).x + fontSize;
            element.preferredHeight = fontSize * 1.7f;
        }
        /// <summary>
        /// 宽高由所在的 HorizontalLayoutGroup 按文字的首选尺寸决定
        /// </summary>
        void AddFittedText(RectTransform parent, string text, float fontSize, Color color, FontStyles style)
        {
            var label = CreateText(parent, text, fontSize, color, TextAlignmentOptions.MidlineLeft, style);
            label.overflowMode = TextOverflowModes.Overflow;
        }
        /// <summary>
        /// 文字过长时横向滚动
        /// </summary>
        static void AddMarquee(TextMeshProUGUI text)
        {
            var marquee = text.gameObject.AddComponent<TMPMarqueeText>();
            marquee.StartDelay = 1.5f;
            marquee.ScrollSpeed = 60f;
        }
        static void SetCover(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
        }
        async UniTaskVoid LoadCoverAsync(ISongDetail song, Image image, bool isCompressed, CancellationToken token)
        {
            try
            {
                var sprite = await song.GetCoverAsync(isCompressed, token: token);
                await UniTask.SwitchToMainThread();
                if (token.IsCancellationRequested || image == null)
                {
                    return;
                }
                SetCover(image, sprite);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                MajDebug.LogWarning($"[SongListBrowser] Failed to load cover of {song.Title}: {e.Message}");
            }
        }
        #endregion

        #region Formatting
        static string FormatAchievement(double achievement)
        {
            return achievement.ToString("0.0000", CultureInfo.InvariantCulture) + "%";
        }
        /// <summary>
        /// “3 PC · 2026-10-30”
        /// </summary>
        static string FormatPlayStats(MaiScore score)
        {
            var count = score.PlayCount.ToString(CultureInfo.InvariantCulture);
            return $"{count} PC · {score.Timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        }
        static string FormatLength(TimeSpan length)
        {
            return $"{(int)length.TotalMinutes}:{length.Seconds:00}";
        }
        static string FormatBpm(float minBpm, float maxBpm)
        {
            return Mathf.Approximately(minBpm, maxBpm)
                ? maxBpm.ToString("0.#", CultureInfo.InvariantCulture)
                : $"{minBpm.ToString("0.#", CultureInfo.InvariantCulture)}-{maxBpm.ToString("0.#", CultureInfo.InvariantCulture)}";
        }
        static bool HasLevel(ISongDetail song, ChartLevel level)
        {
            var levels = song.Levels;
            var index = (int)level;
            return index < levels.Length && !string.IsNullOrEmpty(levels[index]);
        }
        static ChartLevel GetHighestLevel(ISongDetail song)
        {
            var levels = song.Levels;
            for (var i = Math.Min(levels.Length, LEVEL_SHORT_NAMES.Length) - 1; i >= 0; i--)
            {
                if (!string.IsNullOrEmpty(levels[i]))
                {
                    return (ChartLevel)i;
                }
            }
            return ChartLevel.Easy;
        }
        static string GetDesigner(ISongDetail song, ChartLevel level)
        {
            var designers = song.Designers;
            var index = (int)level;
            return index < designers.Length && !string.IsNullOrWhiteSpace(designers[index]) ? designers[index] : "—";
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
        /// <summary>
        /// 评级颜色：SSS 段金色，SS 段橙色，S 段黄色，A 段蓝色，其余灰色
        /// </summary>
        static Color GetGradeColor(string grade)
        {
            return grade switch
            {
                "SSS+" or "SSS" => GOLD_COLOR,
                "SS+" or "SS" => ORANGE_COLOR,
                "S+" or "S" => YELLOW_COLOR,
                "AAA" or "AA" or "A" => BLUE_COLOR,
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
        /// <summary>
        /// AP 金色，FC 绿色
        /// </summary>
        static Color GetComboColor(ComboState state)
        {
            return state is ComboState.APPlus or ComboState.AP ? GOLD_COLOR : GREEN_COLOR;
        }
        static string GetDimensionName(string dimension)
        {
            var key = "MAJTEXT_LIST_RADAR_" + dimension;
            return key.Tryi18n(out var text) ? text : dimension;
        }
        static Color GetDifficultyColor(ChartLevel level)
        {
            return level switch
            {
                ChartLevel.Easy => Hex(0x0E9BEE),
                ChartLevel.Basic => Hex(0x22BB5B),
                ChartLevel.Advance => Hex(0xF08A12),
                ChartLevel.Expert => Hex(0xF64861),
                ChartLevel.Master => Hex(0x9E45E2),
                ChartLevel.ReMaster => Hex(0xB15CF0),
                _ => Hex(0xE8379C),
            };
        }
        static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }
        static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
        #endregion

        #region Setup
        static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }
            var obj = new GameObject("SongListBrowserEventSystem", typeof(EventSystem));
            var module = obj.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
        static TMP_FontAsset? FindFont()
        {
            var text = FindFirstObjectByType<TMP_Text>();
            if (text != null && text.font != null)
            {
                return text.font;
            }
            return TMP_Settings.defaultFontAsset;
        }
        /// <summary>
        /// 白色圆角贴图，九宫格拉伸；Image 的 pixelsPerUnitMultiplier 控制圆角大小
        /// </summary>
        static Sprite CreateRoundedSprite()
        {
            const int SIZE = 64;
            const float RADIUS = 24f;
            var texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false)
            {
                name = "SongListRounded",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[SIZE * SIZE];
            for (var y = 0; y < SIZE; y++)
            {
                for (var x = 0; x < SIZE; x++)
                {
                    var cx = Mathf.Clamp(x + 0.5f, RADIUS, SIZE - RADIUS);
                    var cy = Mathf.Clamp(y + 0.5f, RADIUS, SIZE - RADIUS);
                    var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    var alpha = Mathf.Clamp01(RADIUS - distance + 0.5f);
                    pixels[y * SIZE + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                                 new Vector4(RADIUS, RADIUS, RADIUS, RADIUS));
        }
        /// <summary>
        /// 白色爱心贴图（实心或空心），4×4 超采样抗锯齿；不依赖字体里有没有 ♥
        /// </summary>
        static Sprite CreateHeartSprite(bool isFilled)
        {
            const int SIZE = 96;
            const int SAMPLES = 4;
            const float INNER_SCALE = 0.76f;
            const float CENTER_Y = 0.08f;
            var texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false)
            {
                name = isFilled ? "SongListHeartFilled" : "SongListHeartOutline",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[SIZE * SIZE];
            for (var y = 0; y < SIZE; y++)
            {
                for (var x = 0; x < SIZE; x++)
                {
                    var covered = 0;
                    for (var sy = 0; sy < SAMPLES; sy++)
                    {
                        for (var sx = 0; sx < SAMPLES; sx++)
                        {
                            var hx = (x + (sx + 0.5f) / SAMPLES) / SIZE * 2.7f - 1.35f;
                            var hy = (y + (sy + 0.5f) / SAMPLES) / SIZE * 2.7f - 1.25f;
                            if (!IsInHeart(hx, hy))
                            {
                                continue;
                            }
                            if (isFilled || !IsInHeart(hx / INNER_SCALE, (hy - CENTER_Y) / INNER_SCALE + CENTER_Y))
                            {
                                covered++;
                            }
                        }
                    }
                    var alpha = (byte)Mathf.RoundToInt(255f * covered / (SAMPLES * SAMPLES));
                    pixels[y * SIZE + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100f);
        }
        /// <summary>
        /// 心形线 (x² + y² − 1)³ − x²y³ ≤ 0
        /// </summary>
        static bool IsInHeart(float x, float y)
        {
            var a = x * x + y * y - 1f;
            return a * a * a - x * x * y * y * y <= 0f;
        }
        #endregion

        #region UI building
        static RectTransform CreateRect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return (RectTransform)obj.transform;
        }
        Image CreateImage(Transform parent, Color color, float radius)
        {
            var rect = CreateRect("Image", parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (radius > 0f)
            {
                image.sprite = _roundedSprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 24f / radius;
            }
            return image;
        }
        Image CreatePanel(Transform parent, Color color, float radius)
        {
            var image = CreateImage(parent, color, radius);
            image.gameObject.name = "Panel";
            return image;
        }
        /// <summary>
        /// 文字框高度请留到字号的 1.5 倍左右，否则 TMP 会把放不下的整行省略掉
        /// </summary>
        TextMeshProUGUI CreateText(Transform parent, string content, float fontSize, Color color, TextAlignmentOptions alignment, FontStyles style)
        {
            var rect = CreateRect("Text", parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null)
            {
                text.font = _font;
            }
            // 场景字体的材质带描边和阴影，在浅色界面上很难读，换成干净的材质
            CleanTmpMaterial.Apply(text);
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.enableWordWrapping = content.Contains("\n");
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }
        Button CreateButton(Transform parent, string label, Color fill, Color textColor, float fontSize, UnityAction? onClick)
        {
            var background = CreatePanel(parent, fill, 10f);
            background.gameObject.name = "Button";
            background.raycastTarget = true;
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            SetButtonColors(button);
            if (onClick is not null)
            {
                button.onClick.AddListener(onClick);
            }
            var text = CreateText(background.transform, label, fontSize, textColor, TextAlignmentOptions.Center, FontStyles.Normal);
            Stretch(text.rectTransform, 4f);
            return button;
        }
        TMP_InputField CreateInputField(Transform parent, string placeholder)
        {
            var border = CreatePanel(parent, LINE_COLOR, 11f);
            border.gameObject.name = "SearchField";
            border.raycastTarget = true;
            var background = CreatePanel(border.transform, PAPER_COLOR, 10f);
            Stretch(background.rectTransform, 1.5f);
            var textArea = CreateRect("TextArea", border.transform);
            Stretch(textArea, 16f);
            textArea.offsetMin = new Vector2(16f, 6f);
            textArea.offsetMax = new Vector2(-16f, -6f);
            textArea.gameObject.AddComponent<RectMask2D>();
            var placeholderText = CreateText(textArea, placeholder, 18f, MUTED_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            Stretch(placeholderText.rectTransform);
            var text = CreateText(textArea, string.Empty, 18f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            Stretch(text.rectTransform);
            var input = border.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = textArea;
            input.textComponent = text;
            input.placeholder = placeholderText;
            input.targetGraphic = border;
            input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
        }
        /// <summary>
        /// 竖向滚动区域，返回内容节点（宽度跟随视口）
        /// </summary>
        RectTransform CreateScroll(Transform parent, float left, float top, float right, float bottom, out ScrollRect scrollRect)
        {
            var scroll = CreateRect("Scroll", parent);
            scroll.anchorMin = Vector2.zero;
            scroll.anchorMax = Vector2.one;
            scroll.offsetMin = new Vector2(left, bottom);
            scroll.offsetMax = new Vector2(-right, -top);
            scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 30f;
            var viewport = CreateRect("Viewport", scroll);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            // 透明背景接收拖动
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            return content;
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
        /// <summary>
        /// 以父节点左上角为原点摆放固定大小的矩形
        /// </summary>
        static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
        /// <summary>
        /// 以父节点右上角为原点摆放固定大小的矩形（<paramref name="x"/> 为距右边的距离）
        /// </summary>
        static void PlaceTopRight(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
        /// <summary>
        /// 竖直居中、距左边 <paramref name="x"/> 的固定大小矩形
        /// </summary>
        static void PlaceMiddleLeft(RectTransform rect, float x, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, height);
        }
        /// <summary>
        /// 竖直居中、距右边 <paramref name="x"/> 的固定大小矩形
        /// </summary>
        static void PlaceMiddleRight(RectTransform rect, float x, float width, float height)
        {
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-x, 0f);
            rect.sizeDelta = new Vector2(width, height);
        }
        /// <summary>
        /// 横向撑满父节点（左右留边）、距顶部 <paramref name="top"/>、高 <paramref name="height"/> 的一行
        /// </summary>
        static void PlaceRow(RectTransform rect, float left, float top, float right, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }
        #endregion
    }
}
