using Cysharp.Threading.Tasks;
using MajdataPlay.Scenes.Game.Notes.Controllers;
using MajdataPlay.Settings;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
#nullable enable
namespace MajdataPlay.Scenes.Game.Lane
{
    /// <summary>
    /// 下落式的 Phigros 式 HUD：左上暂停键（双击暂停）与跳过键、右上分数（下方小字为达成率）、左下曲名、右下难度、顶部进度条，
    /// 连击数等移到顶部中间；判定线颜色按设置随成绩变化。暂停菜单与恢复倒计时放在单独的覆盖层画布上，盖在音符之上。
    /// <para>不依赖 EventSystem，按钮由本组件自行判断触摸与鼠标点击。</para>
    /// </summary>
    internal sealed class LaneHud : MonoBehaviour
    {
        const float REFERENCE_WIDTH = 1920f;
        const float REFERENCE_HEIGHT = 1080f;
        const float EDGE_MARGIN = 40f;
        const float PROGRESS_BAR_HEIGHT = 6f;
        const float BUTTON_SIZE = 90f;
        /// <summary>
        /// 暂停键的点击区比图形大一些，手指更容易点中
        /// </summary>
        const float PAUSE_HIT_SIZE = 120f;
        const float SCORE_FONT_SIZE = 46f;
        const float ACHIEVEMENT_FONT_SIZE = 28f;
        const float DOUBLE_TAP_INTERVAL_SEC = 0.5f;
        const float RESUME_COUNTDOWN_SEC = 3f;

        static readonly Color PAUSE_BUTTON_COLOR = new Color(1f, 1f, 1f, 0.9f);
        static readonly Color PAUSE_BUTTON_ARMED_COLOR = new Color(0.45f, 0.85f, 1f, 1f);
        static readonly Color JUDGE_LINE_WHITE = new Color(1f, 1f, 1f, 0.9f);
        static readonly Color JUDGE_LINE_GOLD = new Color(1f, 0.82f, 0.3f, 0.9f);
        static readonly Color JUDGE_LINE_PURPLE = new Color(0.78f, 0.45f, 1f, 0.9f);
        static readonly Color JUDGE_LINE_GREEN = new Color(0.4f, 0.95f, 0.45f, 0.9f);
        static readonly Color JUDGE_LINE_BLUE = new Color(0.4f, 0.75f, 1f, 0.9f);

        static LaneHud? _instance;

        GamePlayManager _gpManager = null!;
        NoteManager _noteManager = null!;
        BGManager _bgManager = null!;
        ObjectCounter _objectCounter = null!;
        FallingJudgeLineColorOption _judgeLineColorOption = FallingJudgeLineColorOption.White;
        Canvas? _canvas;

        RectTransform _root = null!;
        RectTransform? _background;
        RectTransform _progressFill = null!;
        RectTransform _pauseButton = null!;
        Image[] _pauseButtonBars = Array.Empty<Image>();
        RectTransform? _skipButton;

        GameObject _overlay = null!;
        GameObject _menu = null!;
        RectTransform _resumeButton = null!;
        RectTransform _retryButton = null!;
        RectTransform _practiceButton = null!;
        RectTransform _exitButton = null!;
        TextMeshProUGUI _countdownText = null!;

        float _pauseArmedUntil = -1f;
        float _countdownRemaining = 0f;
        bool _isMenuVisible = false;
        bool _isLeaving = false;
        bool _isSkipTriggered = false;

        /// <summary>
        /// 在游戏画布内创建 HUD，并把现有的分数、连击数、跳过键等挪到对应位置
        /// </summary>
        public static LaneHud Create(GamePlayManager gpManager,
                                     NoteManager noteManager,
                                     BGManager bgManager,
                                     ObjectCounter objectCounter,
                                     RectTransform canvasRoot,
                                     GameObject skipButton,
                                     TMP_FontAsset? font,
                                     string title,
                                     string difficulty,
                                     Color difficultyColor)
        {
            var obj = new GameObject("LaneHud", typeof(RectTransform));
            var hud = obj.AddComponent<LaneHud>();
            hud.Build(gpManager, noteManager, bgManager, objectCounter, canvasRoot, skipButton, font, title, difficulty, difficultyColor);
            _instance = hud;
            return hud;
        }
        /// <summary>
        /// 例如 “MASTER Lv.13+”
        /// </summary>
        public static string FormatDifficulty(ChartLevel level, string levelText)
        {
            var name = level switch
            {
                ChartLevel.Easy => "EASY",
                ChartLevel.Basic => "BASIC",
                ChartLevel.Advance => "ADVANCED",
                ChartLevel.Expert => "EXPERT",
                ChartLevel.Master => "MASTER",
                ChartLevel.ReMaster => "Re:MASTER",
                ChartLevel.UTAGE => "UTAGE",
                _ => level.ToString().ToUpperInvariant(),
            };
            return string.IsNullOrEmpty(levelText) ? name : $"{name} Lv.{levelText}";
        }
        /// <summary>
        /// 该位置的手指不参与轨道判定：落在暂停键、跳过键上，或正处于暂停中
        /// </summary>
        public static bool IsPointerBlocked(Vector2 screenPosition)
        {
            var hud = _instance;
            if (hud == null)
            {
                return false;
            }
            else if (hud._isMenuVisible || hud._countdownRemaining > 0 || hud._gpManager.IsPaused)
            {
                return true;
            }
            var camera = hud.CanvasCamera;
            if (RectTransformUtility.RectangleContainsScreenPoint(hud._pauseButton, screenPosition, camera))
            {
                return true;
            }
            return hud.IsSkipButtonAvailable &&
                   RectTransformUtility.RectangleContainsScreenPoint(hud._skipButton!, screenPosition, camera);
        }

        bool IsSkipButtonAvailable => _skipButton != null && _skipButton.gameObject.activeInHierarchy && !_isSkipTriggered;
        /// <summary>
        /// 游戏画布当前用于渲染的相机。CanvasConfigurator 会在之后的 Start 中替换画布相机，因此每次判定时实时读取
        /// </summary>
        Camera? CanvasCamera => _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

        void Build(GamePlayManager gpManager,
                   NoteManager noteManager,
                   BGManager bgManager,
                   ObjectCounter objectCounter,
                   RectTransform canvasRoot,
                   GameObject skipButton,
                   TMP_FontAsset? font,
                   string title,
                   string difficulty,
                   Color difficultyColor)
        {
            _gpManager = gpManager;
            _noteManager = noteManager;
            _bgManager = bgManager;
            var canvas = canvasRoot.GetComponentInParent<Canvas>();
            _canvas = canvas != null ? canvas.rootCanvas : null;
            var layer = canvasRoot.gameObject.layer;

            // 铺满整个画布（即整个屏幕），HUD 元素贴在屏幕四角和边上
            _root = (RectTransform)transform;
            gameObject.layer = layer;
            _root.SetParent(canvasRoot, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = Vector2.zero;
            _root.anchoredPosition = Vector2.zero;
            _root.SetAsLastSibling();

            // 背景铺满屏幕：在 LateUpdate 中按显示中的封面或视频尺寸缩放
            _background = bgManager.CoverRenderer.rectTransform.parent as RectTransform;

            // 进度条
            var progressBack = CreateImage("ProgressBack", _root, layer, new Color(1f, 1f, 1f, 0.2f));
            progressBack.rectTransform.anchorMin = new Vector2(0, 1);
            progressBack.rectTransform.anchorMax = new Vector2(1, 1);
            progressBack.rectTransform.pivot = new Vector2(0.5f, 1);
            progressBack.rectTransform.sizeDelta = new Vector2(0, PROGRESS_BAR_HEIGHT);
            progressBack.rectTransform.anchoredPosition = Vector2.zero;
            var progressFill = CreateImage("ProgressFill", _root, layer, new Color(1f, 1f, 1f, 0.85f));
            _progressFill = progressFill.rectTransform;
            _progressFill.anchorMin = new Vector2(0, 1);
            _progressFill.anchorMax = new Vector2(0, 1);
            _progressFill.pivot = new Vector2(0, 1);
            _progressFill.sizeDelta = new Vector2(0, PROGRESS_BAR_HEIGHT);
            _progressFill.anchoredPosition = Vector2.zero;

            // 左上：暂停键（“II”）
            _pauseButton = CreateRect("PauseButton", _root, layer);
            SetRect(_pauseButton, new Vector2(0, 1), new Vector2(0.5f, 0.5f),
                    new Vector2(EDGE_MARGIN + BUTTON_SIZE / 2f, -(EDGE_MARGIN + BUTTON_SIZE / 2f)),
                    new Vector2(PAUSE_HIT_SIZE, PAUSE_HIT_SIZE));
            _pauseButtonBars = new Image[2];
            for (var i = 0; i < _pauseButtonBars.Length; i++)
            {
                var bar = CreateImage($"Bar{i}", _pauseButton, layer, PAUSE_BUTTON_COLOR);
                SetRect(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(i == 0 ? -12f : 12f, 0), new Vector2(14f, 46f));
                _pauseButtonBars[i] = bar;
            }

            // 左上：跳过键（原 SkipBtn，出现时显示在暂停键右侧）
            _skipButton = skipButton.transform as RectTransform;
            if (_skipButton != null)
            {
                _skipButton.SetParent(_root, false);
                _skipButton.localRotation = Quaternion.identity;
                SetRect(_skipButton, new Vector2(0, 1), new Vector2(0.5f, 0.5f),
                        new Vector2(EDGE_MARGIN + BUTTON_SIZE * 1.5f + 20f, -(EDGE_MARGIN + BUTTON_SIZE / 2f)),
                        new Vector2(BUTTON_SIZE, BUTTON_SIZE));
            }

            // 右上：分数（副屏信息；跟随显示设置时内容由“副屏信息显示”决定，否则为从 0% 往上涨的达成率）
            // 与左上的暂停键对称：同样的边距，竖直方向与暂停键居中对齐
            var gameOptions = MajEnv.Settings.Game;
            var score = objectCounter.SubScreenInfoDisplayerText;
            var scoreRect = score.rectTransform;
            scoreRect.SetParent(_root, false);
            scoreRect.localScale = Vector3.one;
            scoreRect.localRotation = Quaternion.identity;
            SetRect(scoreRect, new Vector2(1, 1), new Vector2(1, 0.5f),
                    new Vector2(-EDGE_MARGIN, -(EDGE_MARGIN + BUTTON_SIZE / 2f)),
                    new Vector2(800f, BUTTON_SIZE));
            SetHudTextStyle(score, SCORE_FONT_SIZE, TextAlignmentOptions.Right);
            if (!gameOptions.FallingInfoFollowDisplay)
            {
                // 分数下方小字：从 101% 往下掉的达成率
                var achievement = CreateText("Achievement", _root, layer, score.font, ACHIEVEMENT_FONT_SIZE, Color.white, TextAlignmentOptions.TopRight, "101.0000%");
                SetRect(achievement.rectTransform, new Vector2(1, 1), new Vector2(1, 1),
                        new Vector2(-EDGE_MARGIN, -(EDGE_MARGIN + BUTTON_SIZE)),
                        new Vector2(600f, ACHIEVEMENT_FONT_SIZE * 1.5f));
                SetHudTextStyle(achievement, ACHIEVEMENT_FONT_SIZE, TextAlignmentOptions.TopRight);
                objectCounter.SetFallingAchievementText(achievement);
            }
            _objectCounter = objectCounter;
            _judgeLineColorOption = gameOptions.FallingJudgeLineColor;

            // 顶部中间：连击数、第二信息、判定时机条，保留它们之间的相对位置
            var topCenter = CreateRect("TopCenter", _root, layer);
            SetRect(topCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -170f), Vector2.zero);
            topCenter.localScale = new Vector3(0.6f, 0.6f, 1f);
            MoveTo(objectCounter.CenterInfoDisplayerObject, topCenter);
            MoveTo(objectCounter.SecondaryInfoDisplayerObject, topCenter);
            MoveTo(objectCounter.JudgeTimingGaugeObject, topCenter);

            // 左下曲名、右下难度
            var titleText = CreateText("Title", _root, layer, font, 40f, Color.white, TextAlignmentOptions.BottomLeft, title);
            SetRect(titleText.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(EDGE_MARGIN, 24f), new Vector2(1100f, 60f));
            var difficultyText = CreateText("Difficulty", _root, layer, font, 40f, gameOptions.FallingDifficultyColor ? difficultyColor : Color.white, TextAlignmentOptions.BottomRight, difficulty);
            SetRect(difficultyText.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-EDGE_MARGIN, 24f), new Vector2(600f, 60f));

            BuildPauseOverlay(font);

            GameManager.OnAppPause += OnAppPause;
        }
        void BuildPauseOverlay(TMP_FontAsset? font)
        {
            // Screen Space Overlay：盖在音符和所有游戏画面之上
            _overlay = new GameObject("LanePauseOverlay", typeof(RectTransform));
            var canvas = _overlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = _overlay.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(REFERENCE_WIDTH, REFERENCE_HEIGHT);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var layer = _overlay.layer;
            var overlayRoot = (RectTransform)_overlay.transform;

            var dim = CreateImage("Dim", overlayRoot, layer, new Color(0f, 0f, 0f, 0.6f));
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.sizeDelta = Vector2.zero;
            dim.rectTransform.anchoredPosition = Vector2.zero;

            var area = CreateRect("PlayArea", overlayRoot, layer);
            SetRect(area, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(REFERENCE_WIDTH, REFERENCE_HEIGHT));

            var menu = CreateRect("Menu", area, layer);
            SetRect(menu, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(REFERENCE_WIDTH, REFERENCE_HEIGHT));
            _menu = menu.gameObject;
            var pausedText = CreateText("Paused", menu, layer, font, 72f, Color.white, TextAlignmentOptions.Center, "MAJTEXT_GAME_PAUSED".i18n());
            SetRect(pausedText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 180f), new Vector2(800f, 120f));
            _resumeButton = CreateMenuButton("Resume", menu, layer, font, "MAJTEXT_GAME_PAUSE_RESUME".i18n(), -540f);
            _retryButton = CreateMenuButton("Retry", menu, layer, font, "MAJTEXT_GAME_PAUSE_RETRY".i18n(), -180f);
            _practiceButton = CreateMenuButton("Practice", menu, layer, font, "MAJTEXT_GAME_PAUSE_PRACTICE".i18n(), 180f);
            _exitButton = CreateMenuButton("Exit", menu, layer, font, "MAJTEXT_GAME_PAUSE_EXIT".i18n(), 540f);

            _countdownText = CreateText("Countdown", area, layer, font, 220f, Color.white, TextAlignmentOptions.Center, string.Empty);
            SetRect(_countdownText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 300f));
            _countdownText.gameObject.SetActive(false);

            _overlay.SetActive(false);
        }
        static RectTransform CreateMenuButton(string name, RectTransform parent, int layer, TMP_FontAsset? font, string label, float x)
        {
            var button = CreateImage(name, parent, layer, new Color(1f, 1f, 1f, 0.15f));
            SetRect(button.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -40f), new Vector2(300f, 120f));
            var text = CreateText("Label", button.rectTransform, layer, font, 48f, Color.white, TextAlignmentOptions.Center, label);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.sizeDelta = Vector2.zero;
            text.rectTransform.anchoredPosition = Vector2.zero;
            return button.rectTransform;
        }

        void Update()
        {
            if (_isLeaving)
            {
                return;
            }
            UpdateCountdown();
            var touches = Touch.activeTouches;
            if (touches.Count > 0)
            {
                for (var i = 0; i < touches.Count; i++)
                {
                    var touch = touches[i];
                    if (touch.valid && touch.phase == TouchPhase.Began)
                    {
                        HandlePress(touch.screenPosition);
                    }
                }
            }
#if UNITY_STANDALONE || UNITY_EDITOR
            else if (Mouse.current is Mouse mouse && mouse.leftButton.wasPressedThisFrame)
            {
                HandlePress(mouse.position.value);
            }
            if (Keyboard.current is Keyboard keyboard && keyboard.escapeKey.wasPressedThisFrame)
            {
                // Esc：暂停 / 从菜单继续
                if (_isMenuVisible)
                {
                    StartResumeCountdown();
                }
                else if (_countdownRemaining <= 0)
                {
                    RequestPause();
                }
            }
#endif
            if (_pauseArmedUntil >= 0 && Time.unscaledTime > _pauseArmedUntil)
            {
                DisarmPauseButton();
            }
        }
        void LateUpdate()
        {
            var length = _gpManager.AudioLength;
            var progress = length > 0 ? Mathf.Clamp01(_gpManager.ThisFrameSec / length) : 0f;
            _progressFill.anchorMax = new Vector2(progress, 1);
            FitBackground();
            UpdateJudgeLineColor();
        }
        /// <summary>
        /// maimai 式：保持 AP 金色，出现 Great 紫色、Good 绿色、Miss 白色；
        /// Phigros 式：101% 往下掉的达成率不低于 100.5% 金色、不低于 100% 蓝色，否则白色
        /// </summary>
        void UpdateJudgeLineColor()
        {
            var judgeLine = LaneVisuals.JudgeLine;
            if (judgeLine == null)
            {
                return;
            }
            Color color;
            switch (_judgeLineColorOption)
            {
                case FallingJudgeLineColorOption.Maimai:
                    {
                        ref readonly var stats = ref _objectCounter.JudgeStats;
                        color = stats.TotalMissCount != 0 ? JUDGE_LINE_WHITE
                              : stats.TotalGoodCount != 0 ? JUDGE_LINE_GREEN
                              : stats.TotalGreatCount != 0 ? JUDGE_LINE_PURPLE
                              : JUDGE_LINE_GOLD;
                    }
                    break;
                case FallingJudgeLineColorOption.Phigros:
                    {
                        var achievement = _objectCounter.Achievement101;
                        color = achievement >= 100.5 ? JUDGE_LINE_GOLD
                              : achievement >= 100 ? JUDGE_LINE_BLUE
                              : JUDGE_LINE_WHITE;
                    }
                    break;
                default:
                    color = JUDGE_LINE_WHITE;
                    break;
            }
            if (judgeLine.color != color)
            {
                judgeLine.color = color;
            }
        }
        void OnDestroy()
        {
            GameManager.OnAppPause -= OnAppPause;
            if (_instance == this)
            {
                _instance = null;
            }
            if (_overlay != null)
            {
                Destroy(_overlay);
            }
        }
        void HandlePress(Vector2 screenPosition)
        {
            if (_countdownRemaining > 0)
            {
                return;
            }
            if (_isMenuVisible)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(_resumeButton, screenPosition, null))
                {
                    StartResumeCountdown();
                }
                else if (RectTransformUtility.RectangleContainsScreenPoint(_retryButton, screenPosition, null))
                {
                    Leave(_gpManager.PauseMenuRetry);
                }
                else if (RectTransformUtility.RectangleContainsScreenPoint(_practiceButton, screenPosition, null))
                {
                    Leave(_gpManager.PauseMenuPractice);
                }
                else if (RectTransformUtility.RectangleContainsScreenPoint(_exitButton, screenPosition, null))
                {
                    Leave(_gpManager.PauseMenuExit);
                }
                return;
            }
            var camera = CanvasCamera;
            if (RectTransformUtility.RectangleContainsScreenPoint(_pauseButton, screenPosition, camera))
            {
                // 像 Phigros 一样双击暂停：第一次点击高亮，短时间内再点一次才暂停
                if (_pauseArmedUntil >= 0 && Time.unscaledTime <= _pauseArmedUntil)
                {
                    DisarmPauseButton();
                    RequestPause();
                }
                else
                {
                    _pauseArmedUntil = Time.unscaledTime + DOUBLE_TAP_INTERVAL_SEC;
                    SetPauseButtonColor(PAUSE_BUTTON_ARMED_COLOR);
                }
                return;
            }
            if (IsSkipButtonAvailable && RectTransformUtility.RectangleContainsScreenPoint(_skipButton!, screenPosition, camera))
            {
                // 与 SkipButton.OnAreaDown 一致
                _isSkipTriggered = true;
                _gpManager.EndGame().Forget();
            }
        }
        void RequestPause()
        {
            if (_gpManager.Pause())
            {
                ShowMenu();
            }
        }
        void ShowMenu()
        {
            _countdownRemaining = 0f;
            _countdownText.gameObject.SetActive(false);
            _menu.SetActive(true);
            _overlay.SetActive(true);
            _isMenuVisible = true;
        }
        void StartResumeCountdown()
        {
            _isMenuVisible = false;
            _menu.SetActive(false);
            _countdownRemaining = RESUME_COUNTDOWN_SEC;
            _countdownText.text = Mathf.CeilToInt(_countdownRemaining).ToString();
            _countdownText.gameObject.SetActive(true);
        }
        void UpdateCountdown()
        {
            if (_countdownRemaining <= 0)
            {
                return;
            }
            _countdownRemaining -= Time.unscaledDeltaTime;
            if (_countdownRemaining > 0)
            {
                _countdownText.text = Mathf.CeilToInt(_countdownRemaining).ToString();
                return;
            }
            _countdownRemaining = 0f;
            _countdownText.gameObject.SetActive(false);
            _overlay.SetActive(false);
            // 恢复时仍按在屏幕上的手指不算新的点击
            _noteManager.IgnoreActiveLanePointers();
            _gpManager.Resume();
        }
        void Leave(Action action)
        {
            _isLeaving = true;
            action();
        }
        void OnAppPause(object? sender, bool isPaused)
        {
            // 切到后台时自动暂停；倒计时中切走则回到暂停菜单
            if (!isPaused || _isLeaving)
            {
                return;
            }
            if (_countdownRemaining > 0)
            {
                ShowMenu();
            }
            else if (!_isMenuVisible)
            {
                RequestPause();
            }
        }
        void DisarmPauseButton()
        {
            _pauseArmedUntil = -1f;
            SetPauseButtonColor(PAUSE_BUTTON_COLOR);
        }
        void SetPauseButtonColor(Color color)
        {
            foreach (var bar in _pauseButtonBars)
            {
                bar.color = color;
            }
        }
        /// <summary>
        /// 背景（封面或视频）宽度按现有逻辑为 1080，等比放大到盖满整个屏幕（HUD 根节点即整个画布）
        /// </summary>
        void FitBackground()
        {
            if (_background == null)
            {
                return;
            }
            var video = _bgManager.VideoRenderer;
            var cover = _bgManager.CoverRenderer;
            RectTransform target;
            if (video.enabled)
            {
                target = video.rectTransform;
            }
            else if (cover.enabled)
            {
                target = cover.rectTransform;
            }
            else
            {
                return;
            }
            var size = target.rect.size;
            var scale = target.localScale;
            var width = Mathf.Abs(size.x * scale.x);
            var height = Mathf.Abs(size.y * scale.y);
            if (width <= 0 || height <= 0)
            {
                return;
            }
            var screenSize = _root.rect.size;
            var fit = Mathf.Max(screenSize.x / width, screenSize.y / height);
            _background.localScale = new Vector3(fit, fit, 1f);
        }

        static void MoveTo(GameObject? obj, RectTransform parent)
        {
            if (obj == null)
            {
                return;
            }
            obj.transform.SetParent(parent, false);
        }
        static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
        static RectTransform CreateRect(string name, Transform parent, int layer)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.layer = layer;
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            return rect;
        }
        static Image CreateImage(string name, Transform parent, int layer, Color color)
        {
            var rect = CreateRect(name, parent, layer);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
        /// <summary>
        /// 固定字号、不自动缩放、不换行，避免沿用副屏文字原有的字号与边距
        /// </summary>
        static void SetHudTextStyle(TextMeshProUGUI text, float fontSize, TextAlignmentOptions alignment)
        {
            text.enableAutoSizing = false;
            text.fontSize = fontSize;
            text.margin = Vector4.zero;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = alignment;
        }
        static TextMeshProUGUI CreateText(string name, Transform parent, int layer, TMP_FontAsset? font, float fontSize, Color color, TextAlignmentOptions alignment, string text)
        {
            var rect = CreateRect(name, parent, layer);
            var textComponent = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                textComponent.font = font;
            }
            textComponent.fontSize = fontSize;
            textComponent.color = color;
            textComponent.alignment = alignment;
            textComponent.raycastTarget = false;
            textComponent.text = text;
            return textComponent;
        }
    }
}
