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
        /// <summary>
        /// 分数的数字高度约等于暂停键“II”的高度
        /// </summary>
        const float SCORE_FONT_SIZE = 64f;
        /// <summary>
        /// 分数下方的达成率为分数的 40% 大，半透明
        /// </summary>
        const float ACHIEVEMENT_FONT_SIZE = SCORE_FONT_SIZE * 0.4f;
        const float ACHIEVEMENT_ALPHA = 0.7f;
        /// <summary>
        /// The share of the bottom edge taken by the title; the difficulty takes the rest.
        /// </summary>
        const float TITLE_SHARE = 0.62f;
        /// <summary>
        /// The gap between the title and the difficulty.
        /// </summary>
        const float TITLE_GAP = 24f;
        /// <summary>
        /// The distance of the title and the difficulty from the bottom of the safe area.
        /// </summary>
        const float BOTTOM_TEXT_Y = 24f;
        /// <summary>
        /// The height of the title and the difficulty.
        /// </summary>
        const float BOTTOM_TEXT_HEIGHT = 60f;
        /// <summary>
        /// The smallest height of the title and the difficulty when the gap below the judge line is small.
        /// </summary>
        const float MIN_BOTTOM_TEXT_HEIGHT = 24f;
        /// <summary>
        /// The space kept between the title and the difficulty and the judge line above them.
        /// </summary>
        const float BOTTOM_TEXT_GAP = 8f;
        /// <summary>
        /// The smallest font size of the title and the difficulty when they shrink to fit.
        /// </summary>
        const float MIN_BOTTOM_FONT_SIZE = 18f;
        /// <summary>
        /// The width of the pause menu: four buttons 300 wide, centered 360 apart.
        /// </summary>
        const float PAUSE_MENU_WIDTH = 360f * 3f + 300f;
        /// <summary>
        /// The height the pause menu needs around the center: "Paused" reaches 240 above it, so twice that.
        /// </summary>
        const float PAUSE_MENU_HEIGHT = 480f;
        /// <summary>
        /// 点亮手指所在列的亮度，以及松开后淡出所需的时间
        /// </summary>
        const float LANE_HIGHLIGHT_ALPHA = 0.22f;
        const float LANE_HIGHLIGHT_FADE_SEC = 0.1f;
        const float DOUBLE_TAP_INTERVAL_SEC = 0.5f;
        const float RESUME_COUNTDOWN_SEC = 3f;

        static readonly Color PAUSE_BUTTON_COLOR = Color.white;
        /// <summary>
        /// The soft shadow behind the pause bars, so they stay visible on bright covers.
        /// </summary>
        static readonly Color PAUSE_BUTTON_SHADOW_COLOR = new Color(0f, 0f, 0f, 0.35f);
        /// <summary>
        /// The sorting layer of the HUD canvas: above everything in the game scene, below the global scene transition.
        /// </summary>
        const string HUD_SORTING_LAYER = "BlackCircle";
        const int HUD_SORTING_ORDER = 100;
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
        /// <summary>
        /// Holds the pause and skip buttons, the score, the title and the difficulty. It covers the screen, pulled in from an edge
        /// only where the safe area reaches further than the margins, at the same size on every screen; see <see cref="LayoutHudArea"/>.
        /// </summary>
        RectTransform _hudArea = null!;
        /// <summary>
        /// The song title at the bottom left, kept below the judge line.
        /// </summary>
        TextMeshProUGUI _titleText = null!;
        /// <summary>
        /// The difficulty at the bottom right, kept below the judge line.
        /// </summary>
        TextMeshProUGUI _difficultyText = null!;
        /// <summary>
        /// The HUD size the area was last laid out for.
        /// </summary>
        Vector2 _lastRootSize;
        /// <summary>
        /// The screen size the area was last laid out for.
        /// </summary>
        Vector2Int _lastScreenSize;
        /// <summary>
        /// The safe area the area was last laid out for.
        /// </summary>
        Rect _lastSafeArea;
        /// <summary>
        /// The height of the judge line above the screen bottom, in pixels, the area was last laid out for.
        /// </summary>
        float _lastJudgeLineScreenY = float.NaN;
        /// <summary>
        /// The 1920×1080 area of the pause overlay holding the menu and the countdown, scaled by <see cref="FitPauseMenu"/>.
        /// </summary>
        RectTransform _overlayArea = null!;
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
            // HUD 用自己的画布排在游戏场景所有画面之上（全局转场仍盖在它上面），不会被场景里的其他元素盖暗
            var hudCanvas = gameObject.AddComponent<Canvas>();
            hudCanvas.overrideSorting = true;
            hudCanvas.sortingLayerName = HUD_SORTING_LAYER;
            hudCanvas.sortingOrder = HUD_SORTING_ORDER;
            gameObject.AddComponent<GraphicRaycaster>();

            // 背景铺满屏幕：在 LateUpdate 中按显示中的封面或视频尺寸缩放
            _background = bgManager.CoverRenderer.rectTransform.parent as RectTransform;

            // 四角的按钮与文字：离屏幕边取原边距与安全区边距中较大的一个，手机上不放大；由 LayoutHudArea 摆放
            _hudArea = CreateRect("HudArea", _root, layer);

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
            _pauseButton = CreateRect("PauseButton", _hudArea, layer);
            SetRect(_pauseButton, new Vector2(0, 1), new Vector2(0.5f, 0.5f),
                    new Vector2(EDGE_MARGIN + BUTTON_SIZE / 2f, -(EDGE_MARGIN + BUTTON_SIZE / 2f)),
                    new Vector2(PAUSE_HIT_SIZE, PAUSE_HIT_SIZE));
            _pauseButtonBars = new Image[2];
            for (var i = 0; i < _pauseButtonBars.Length; i++)
            {
                var shadow = CreateImage($"BarShadow{i}", _pauseButton, layer, PAUSE_BUTTON_SHADOW_COLOR);
                SetRect(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2((i == 0 ? -12f : 12f) + 2f, -2f), new Vector2(18f, 50f));
                var bar = CreateImage($"Bar{i}", _pauseButton, layer, PAUSE_BUTTON_COLOR);
                SetRect(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(i == 0 ? -12f : 12f, 0), new Vector2(14f, 46f));
                _pauseButtonBars[i] = bar;
            }

            // 左上：跳过键（原 SkipBtn，出现时显示在暂停键右侧）
            _skipButton = skipButton.transform as RectTransform;
            if (_skipButton != null)
            {
                _skipButton.SetParent(_hudArea, false);
                _skipButton.localRotation = Quaternion.identity;
                SetRect(_skipButton, new Vector2(0, 1), new Vector2(0.5f, 0.5f),
                        new Vector2(EDGE_MARGIN + BUTTON_SIZE * 1.5f + 20f, -(EDGE_MARGIN + BUTTON_SIZE / 2f)),
                        new Vector2(BUTTON_SIZE, BUTTON_SIZE));
            }

            // 右上：分数（副屏信息；跟随显示设置时内容由“副屏信息显示”决定，否则为从 0% 往上涨的达成率）
            // 与左上的暂停键对称：同样的边距，竖直方向与暂停键居中对齐
            var fallingOptions = MajEnv.Settings.Falling;
            var score = objectCounter.SubScreenInfoDisplayerText;
            var scoreRect = score.rectTransform;
            scoreRect.SetParent(_hudArea, false);
            scoreRect.localScale = Vector3.one;
            scoreRect.localRotation = Quaternion.identity;
            SetRect(scoreRect, new Vector2(1, 1), new Vector2(1, 0.5f),
                    new Vector2(-EDGE_MARGIN, -(EDGE_MARGIN + BUTTON_SIZE / 2f)),
                    new Vector2(800f, BUTTON_SIZE));
            // 按数字（大写字母）高度居中，数字与暂停键的两道竖线水平对齐
            SetHudTextStyle(score, SCORE_FONT_SIZE, TextAlignmentOptions.CaplineRight);
            ApplyHudMaterial(score);
            if (!fallingOptions.FallingInfoFollowDisplay)
            {
                // 分数下方小字：从 101% 往下掉的达成率，紧贴分数数字下方
                var achievement = CreateText("Achievement", _hudArea, layer, score.font, ACHIEVEMENT_FONT_SIZE, Color.white, TextAlignmentOptions.TopRight, "101.0000%");
                SetRect(achievement.rectTransform, new Vector2(1, 1), new Vector2(1, 1),
                        new Vector2(-EDGE_MARGIN, -(EDGE_MARGIN + BUTTON_SIZE / 2f + SCORE_FONT_SIZE * 0.45f)),
                        new Vector2(600f, ACHIEVEMENT_FONT_SIZE * 1.5f));
                SetHudTextStyle(achievement, ACHIEVEMENT_FONT_SIZE, TextAlignmentOptions.TopRight);
                ApplyHudMaterial(achievement);
                // 颜色由 ObjectCounter 按评级设置，透明度单独用 CanvasGroup 控制，互不覆盖
                var achievementGroup = achievement.gameObject.AddComponent<CanvasGroup>();
                achievementGroup.alpha = ACHIEVEMENT_ALPHA;
                achievementGroup.interactable = false;
                achievementGroup.blocksRaycasts = false;
                objectCounter.SetFallingAchievementText(achievement);
            }
            _objectCounter = objectCounter;
            _judgeLineColorOption = fallingOptions.FallingJudgeLineColor;

            // 顶部中间：连击数、第二信息、判定时机条，保留它们之间的相对位置
            var topCenter = CreateRect("TopCenter", _root, layer);
            SetRect(topCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -170f), Vector2.zero);
            topCenter.localScale = new Vector3(0.6f, 0.6f, 1f);
            MoveTo(objectCounter.CenterInfoDisplayerObject, topCenter);
            MoveTo(objectCounter.SecondaryInfoDisplayerObject, topCenter);
            MoveTo(objectCounter.JudgeTimingGaugeObject, topCenter);

            // 左下曲名、右下难度：曲名占底边左侧 62%，难度占右侧，放在判定线下方，放不下时缩小字号
            _titleText = CreateText("Title", _hudArea, layer, font, 40f, Color.white, TextAlignmentOptions.BottomLeft, title);
            SetBottomTextStyle(_titleText);
            _difficultyText = CreateText("Difficulty", _hudArea, layer, font, 40f, fallingOptions.FallingDifficultyColor ? difficultyColor : Color.white, TextAlignmentOptions.BottomRight, difficulty);
            SetBottomTextStyle(_difficultyText);
            LayoutHudArea(true);

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
            _overlayArea = area;

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
            UiSprites.ApplyRounded(button, 24f);
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
            LayoutHudArea(false);
            UpdateJudgeLineColor();
            UpdateLaneHighlights();
        }
        /// <summary>
        /// 点亮手指所在列：按住时亮，松开后很快淡出
        /// </summary>
        void UpdateLaneHighlights()
        {
            var highlights = LaneVisuals.LaneHighlights;
            if (highlights.Length == 0)
            {
                return;
            }
            var fadeStep = Time.unscaledDeltaTime / LANE_HIGHLIGHT_FADE_SEC;
            for (var i = 0; i < highlights.Length; i++)
            {
                var highlight = highlights[i];
                if (highlight == null)
                {
                    continue;
                }
                var color = highlight.color;
                var isOn = !_gpManager.IsPaused && _noteManager.IsLaneHighlightedInThisFrame(i + 1);
                color.a = isOn ? LANE_HIGHLIGHT_ALPHA : Mathf.Max(0f, color.a - LANE_HIGHLIGHT_ALPHA * fadeStep);
                highlight.color = color;
            }
        }
        /// <summary>
        /// 跟随判定：保持 AP 金色，出现 Great 紫色、Good 绿色、Miss 白色；
        /// 跟随达成率：101% 往下掉的达成率不低于 100.5% 金色、不低于 100% 蓝色，否则白色。
        /// 悬空式的第二条判定线用同样的颜色，但更浅
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
                var slideLine = LaneVisuals.SlideJudgeLine;
                if (slideLine != null)
                {
                    slideLine.color = new Color(color.r, color.g, color.b, color.a * LaneVisuals.SLIDE_JUDGE_LINE_ALPHA);
                }
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
            FitPauseMenu();
            _countdownRemaining = 0f;
            _countdownText.gameObject.SetActive(false);
            _menu.SetActive(true);
            _overlay.SetActive(true);
            _isMenuVisible = true;
        }
        void StartResumeCountdown()
        {
            FitPauseMenu();
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

        /// <summary>
        /// Scales the pause menu and the countdown up on phones like the other touch interfaces, as far as the menu still fits in the
        /// safe area, and centers them in the safe area.
        /// </summary>
        void FitPauseMenu()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }
            // 遮罩画布可能还没启用过、尺寸未更新：按 CanvasScaler 的 Expand 规则直接算出画布尺寸
            var canvasScale = Mathf.Min(Screen.width / REFERENCE_WIDTH, Screen.height / REFERENCE_HEIGHT);
            var size = new Vector2(Screen.width / canvasScale, Screen.height / canvasScale);
            var safeArea = Screen.safeArea;
            var safeWidth = size.x * safeArea.width / Screen.width;
            var safeHeight = size.y * safeArea.height / Screen.height;
            var enlargement = UiScaling.GetEnlargement(Screen.width, Screen.height, Screen.dpi);
            var fit = Mathf.Min(safeWidth * 0.95f / PAUSE_MENU_WIDTH, safeHeight * 0.9f / PAUSE_MENU_HEIGHT);
            var scale = Mathf.Max(1f, Mathf.Min(enlargement, fit));
            _overlayArea.localScale = new Vector3(scale, scale, 1f);
            var offset = safeArea.center - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            _overlayArea.anchoredPosition = new Vector2(offset.x * size.x / Screen.width, offset.y * size.y / Screen.height);
        }
        /// <summary>
        /// Lays out the HUD area for the screen, its safe area and the judge line, and does nothing while none of them changed.
        /// </summary>
        /// <remarks>
        /// On each side but the bottom, the corner items keep the larger of their margin and the safe-area inset from the screen
        /// edge, instead of adding the two. The bottom ignores the home indicator, which sits in the middle while the title and
        /// the difficulty sit at the ends; they stay in the gap below the judge line, see <see cref="GetBottomTextBox"/>.
        /// The corner items are not enlarged on phones; only the pause menu is, see <see cref="FitPauseMenu"/>.
        /// </remarks>
        /// <param name="isForced">Whether to lay out even when nothing changed.</param>
        void LayoutHudArea(bool isForced)
        {
            var rootSize = _root.rect.size;
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            var safeArea = Screen.safeArea;
            var judgeLineScreenY = GetJudgeLineScreenY();
            if (!isForced && rootSize == _lastRootSize && screenSize == _lastScreenSize && safeArea == _lastSafeArea &&
                Mathf.Abs(judgeLineScreenY - _lastJudgeLineScreenY) < 0.5f)
            {
                return;
            }
            if (rootSize.x <= 0f || rootSize.y <= 0f || screenSize.x <= 0 || screenSize.y <= 0)
            {
                // 尺寸还没确定：下一帧再试
                _lastJudgeLineScreenY = float.NaN;
                return;
            }
            _lastRootSize = rootSize;
            _lastScreenSize = screenSize;
            _lastSafeArea = safeArea;
            _lastJudgeLineScreenY = judgeLineScreenY;
            var unitsPerPixel = new Vector2(rootSize.x / screenSize.x, rootSize.y / screenSize.y);
            // 安全区比边距更靠里时才往里收，收的量是两者之差
            var insetLeft = Mathf.Max(0f, safeArea.xMin * unitsPerPixel.x - EDGE_MARGIN);
            var insetRight = Mathf.Max(0f, (screenSize.x - safeArea.xMax) * unitsPerPixel.x - EDGE_MARGIN);
            var insetTop = Mathf.Max(0f, (screenSize.y - safeArea.yMax) * unitsPerPixel.y - EDGE_MARGIN);
            _hudArea.anchorMin = new Vector2(insetLeft / rootSize.x, 0f);
            _hudArea.anchorMax = new Vector2(1f - insetRight / rootSize.x, 1f - insetTop / rootSize.y);
            _hudArea.pivot = new Vector2(0.5f, 0.5f);
            _hudArea.localScale = Vector3.one;
            _hudArea.offsetMin = Vector2.zero;
            _hudArea.offsetMax = Vector2.zero;

            // 判定线下方的空隙，换成 HUD 的单位；拿不到相机时按原来的位置摆
            var gap = judgeLineScreenY >= 0f
                ? judgeLineScreenY * unitsPerPixel.y
                : BOTTOM_TEXT_Y + BOTTOM_TEXT_HEIGHT + BOTTOM_TEXT_GAP;
            GetBottomTextBox(gap, out var bottom, out var height);
            SetBottomRow(_titleText, 0f, TITLE_SHARE, EDGE_MARGIN, TITLE_GAP * 0.5f, bottom, height);
            SetBottomRow(_difficultyText, TITLE_SHARE, 1f, TITLE_GAP * 0.5f, EDGE_MARGIN, bottom, height);
        }
        /// <summary>
        /// Gets where the title and the difficulty go in the gap below the judge line: at their usual height when the gap is large
        /// enough (tablets), lower and smaller when it is not (phones in landscape).
        /// </summary>
        /// <param name="gap">The height of the gap between the screen bottom and the judge line, in HUD area units.</param>
        /// <param name="bottom">The distance of the text box from the screen bottom.</param>
        /// <param name="height">The height of the text box.</param>
        internal static void GetBottomTextBox(float gap, out float bottom, out float height)
        {
            bottom = Mathf.Max(0f, Mathf.Min(BOTTOM_TEXT_Y, gap * 0.25f));
            height = Mathf.Clamp(gap - BOTTOM_TEXT_GAP - bottom, MIN_BOTTOM_TEXT_HEIGHT, BOTTOM_TEXT_HEIGHT);
        }
        /// <summary>
        /// Gets the height of the judge line above the screen bottom.
        /// </summary>
        /// <returns>The height in screen pixels, or -1 while the gameplay camera is not available.</returns>
        static float GetJudgeLineScreenY()
        {
            var camera = SceneSwitcher.MainCamera;
            if (camera == null)
            {
                return -1f;
            }
            return camera.WorldToScreenPoint(new Vector3(0f, LaneLayout.JUDGE_LINE_Y, 0f)).y;
        }
        /// <summary>
        /// Lets a bottom text shrink to fit and end with an ellipsis instead of wrapping; called once after the text is created.
        /// </summary>
        /// <param name="text">The text.</param>
        static void SetBottomTextStyle(TextMeshProUGUI text)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = MIN_BOTTOM_FONT_SIZE;
            text.fontSizeMax = text.fontSize;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }
        /// <summary>
        /// Places a text along the bottom of the HUD area between two horizontal shares of its width.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="fromShare">Where the text starts, as a share of the width.</param>
        /// <param name="toShare">Where the text ends, as a share of the width.</param>
        /// <param name="left">The inset from the start.</param>
        /// <param name="right">The inset from the end.</param>
        /// <param name="bottom">The distance of the text from the bottom.</param>
        /// <param name="height">The height of the text.</param>
        static void SetBottomRow(TextMeshProUGUI text, float fromShare, float toShare, float left, float right, float bottom, float height)
        {
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(fromShare, 0f);
            rect.anchorMax = new Vector2(toShare, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
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
        /// <summary>
        /// Gives a HUD text the shared material with a soft dark shadow, independent of the scene font materials.
        /// </summary>
        /// <param name="text">The HUD text.</param>
        static void ApplyHudMaterial(TMP_Text text)
        {
            var material = CleanTmpMaterial.GetHud(text.font);
            if (material is not null)
            {
                text.fontSharedMaterial = material;
            }
        }
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
            ApplyHudMaterial(textComponent);
            return textComponent;
        }
    }
}
