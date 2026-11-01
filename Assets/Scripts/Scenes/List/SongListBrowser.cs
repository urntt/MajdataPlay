using Cysharp.Threading.Tasks;
using LitMotion;
using MajdataPlay.Collections;
using MajdataPlay.Diagnostics;
using MajdataPlay.IO;
using MajdataPlay.Misc.Base;
using MajdataPlay.Scenes.Game;
using MajdataPlay.Settings;
using MajdataPlay.Utils;
using MajdataPlay.Utils.ChartRadar;
using System;
using System.Collections.Generic;
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
    /// <summary>
    /// 列表式选歌：左侧四分之一是分类，右侧每行一张曲目卡片（曲绘、曲名、曲与谱、时长与 BPM、网络谱面的热度、难度、成绩），
    /// 底部浮窗翻页；点卡片进入谱面详情（雷达图、物量分布、分享与导出、游玩）。
    /// 整个界面用代码搭建在一个全屏画布上；打开期间触摸不再模拟圆圈上的按键，由 EventSystem 处理点击、滑动和输入
    /// </summary>
    internal sealed partial class SongListBrowser : MonoBehaviour
    {
        /// <summary>
        /// 列表式选歌是否正在显示：显示期间 ListManager 不处理圆圈按键，封面轮播在后台切歌也不播放预览
        /// </summary>
        public static bool IsOpen { get; set; } = false;

        static readonly string[] LEVEL_SHORT_NAMES = { "EZ", "BAS", "ADV", "EXP", "MAS", "ReM", "UTA" };
        static readonly string[] RADAR_DIMENSIONS = { "note", "peak", "sweep", "slide_tricky", "slide_sequence", "jack" };

        static readonly Color PAGE_COLOR = Hex(0xF5F6F8);
        static readonly Color PAPER_COLOR = Color.white;
        static readonly Color TEXT_COLOR = Hex(0x202630);
        static readonly Color MUTED_COLOR = Hex(0x788391);
        static readonly Color LINE_COLOR = Hex(0xE4E7EC);
        static readonly Color ACCENT_COLOR = Hex(0x586FDD);
        static readonly Color ACCENT_SOFT_COLOR = Hex(0xEEF1FF);
        static readonly Color FAVORITE_COLOR = Hex(0xE0566F);
        static readonly Color GOLD_COLOR = Hex(0xC98A00);
        static readonly Color ORANGE_COLOR = Hex(0xE8680F);
        static readonly Color YELLOW_COLOR = Hex(0xB59F00);
        static readonly Color BLUE_COLOR = Hex(0x2F6FD6);
        static readonly Color GREEN_COLOR = Hex(0x1E9E52);

        const float TOP_PADDING = 20f;
        const float TOPBAR_HEIGHT = 56f;
        const float CAPTION_TOP = 88f;
        const float LIST_TOP = 128f;
        const float MAIN_LEFT_PADDING = 28f;
        const float MAIN_RIGHT_PADDING = 20f;
        const float CARD_HEIGHT = 210f;
        const float CARD_SPACING = 14f;
        const float COVER_SIZE = 180f;
        const float CARD_RIGHT_WIDTH = 270f;
        const float LEVEL_TAG_MIN_WIDTH = 92f;
        const float LEVEL_TAG_PREFERRED_WIDTH = 140f;
        const float LEVEL_TAG_HEIGHT = 38f;
        const float LEVEL_SCORE_HEIGHT = 22f;
        const float PAGER_HEIGHT = 64f;
        const float PAGER_BOTTOM = 52f;
        const float SCROLLBAR_WIDTH = 16f;
        const float TOAST_DURATION_SEC = 2.4f;

        const float LIST_FADE_SEC = 0.2f;
        const float LIST_SLIDE_DISTANCE = 24f;
        const float DETAIL_IN_SEC = 0.25f;
        const float DETAIL_OUT_SEC = 0.2f;
        const float DETAIL_SLIDE_DISTANCE = 96f;
        const float PREVIEW_FADE_IN_SEC = 0.6f;
        const float PREVIEW_FADE_OUT_SEC = 0.4f;
        const float BGM_FADE_IN_SEC = 0.6f;

        /// <summary>
        /// 离开列表时记下的位置：设置返回时恢复分类、搜索、页码和滚动位置；从详情页进游戏时还会在回来后重新打开这首歌的详情
        /// </summary>
        sealed class SavedState
        {
            public required int CollectionIndex { get; init; }
            public required string Query { get; init; }
            public required int Page { get; init; }
            public required float ScrollY { get; init; }
            public string? DetailHash { get; init; }
            public ChartLevel DetailLevel { get; init; }
        }
        static SavedState? _savedState;

        ListManager _listManager = null!;
        CollectionListManager _collections = null!;
        PreviewSoundPlayer? _previewPlayer;
        TMP_FontAsset? _font;
        Sprite _heartFilledSprite = null!;
        Sprite _heartOutlineSprite = null!;

        RectTransform _root = null!;
        /// <summary>
        /// 整个列表页的透明度跟着游戏的转场走：转场合上时淡出，打开时淡入
        /// </summary>
        CanvasGroup _rootGroup = null!;
        /// <summary>
        /// List 场景自己的画布（封面轮播等）：列表页打开期间停止渲染，淡入淡出时不会透出来
        /// </summary>
        readonly List<Canvas> _hiddenSceneCanvases = new();
        bool _isLeaving = false;
        RectTransform _listView = null!;
        RectTransform _sidebarContent = null!;
        RectTransform _listPane = null!;
        CanvasGroup _listPaneGroup = null!;
        RectTransform _listContent = null!;
        ScrollRect _listScroll = null!;
        TextMeshProUGUI _captionText = null!;
        TextMeshProUGUI _difficultyText = null!;
        TextMeshProUGUI _pageText = null!;
        TMP_InputField _searchField = null!;
        Button _previousPageButton = null!;
        Button _nextPageButton = null!;
        RectTransform _pager = null!;
        Button _selectButton = null!;
        TextMeshProUGUI _selectButtonLabel = null!;
        RectTransform _selectionBar = null!;
        TextMeshProUGUI _selectionCountText = null!;
        /// <summary>
        /// 选择模式：点难度标签勾选或取消自选谱面（不切换全局难度）
        /// </summary>
        bool _isSelecting = false;
        bool _isSelectionDirty = false;

        RectTransform _modal = null!;
        RectTransform _toast = null!;
        TextMeshProUGUI _toastText = null!;

        readonly List<ISongDetail> _filteredSongs = new();
        readonly List<CardView> _cards = new();
        readonly Dictionary<string, MajNetSongInteract> _interactCache = new();
        readonly Dictionary<(string Hash, ChartLevel Level), MaidataAnalyzeResult> _chartInfoCache = new();
        int _page = 0;
        string _query = string.Empty;
        float _toastRemaining = 0f;
        bool _isWaitingShare = false;
        bool _isHidden = false;

        CancellationTokenSource _pageCts = new();
        CancellationTokenSource _levelCts = new();
        MotionHandle _listPaneMotion;
        MotionHandle _listPaneAlphaMotion;
        /// <summary>
        /// 列表区域静止时的位置（拉伸布局的 anchoredPosition 不为 0），切换动画以它为基准
        /// </summary>
        Vector2 _listPaneRestPosition;
        float? _pendingScrollY;

        static ChartLevel GlobalLevel => MajEnv.RuntimeConfig.List.SelectedDiff;
        static int PageSize => Mathf.Clamp(MajEnv.Settings.Game.ListPageSize, 5, 100);

        public static SongListBrowser? Create(ListManager listManager, CollectionListManager collections, CoverListManager covers)
        {
            try
            {
                var obj = new GameObject("SongListBrowser");
                var browser = obj.AddComponent<SongListBrowser>();
                browser.Init(listManager, collections, covers);
                return browser;
            }
            catch (Exception e)
            {
                MajDebug.LogError(e);
                IsOpen = false;
                InputManager.SuppressTouchPanelSimulation = false;
                return null;
            }
        }

        #region Lifecycle
        void Init(ListManager listManager, CollectionListManager collections, CoverListManager covers)
        {
            _listManager = listManager;
            _collections = collections;
            _previewPlayer = FindFirstObjectByType<PreviewSoundPlayer>();
            _font = FindFont();
            _heartFilledSprite = CreateHeartSprite(true);
            _heartOutlineSprite = CreateHeartSprite(false);
            EnsureEventSystem();

            var canvasObject = new GameObject("SongListBrowserCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            _root = (RectTransform)canvasObject.transform;
            _rootGroup = canvasObject.AddComponent<CanvasGroup>();
            UpdateTransitionFade();

            BuildListView();
            BuildDetailView();
            BuildModal();
            BuildToast();

            IsOpen = true;
            InputManager.SuppressTouchPanelSimulation = true;
            HideSceneCanvases();
            RestoreState();
        }
        /// <summary>
        /// 打开时：有记下的位置就恢复（从详情页进游戏的还会重新打开详情），否则翻到选中的歌所在的页
        /// </summary>
        void RestoreState()
        {
            var state = _savedState;
            _savedState = null;
            if (state is null || state.CollectionIndex != _collections.SelectedCollectionIndex)
            {
                RefreshList(true);
                return;
            }
            _query = state.Query;
            _searchField.SetTextWithoutNotify(_query);
            _page = state.Page;
            RefreshList(false, false);
            RequestListScroll(state.ScrollY);
            if (string.IsNullOrEmpty(state.DetailHash))
            {
                return;
            }
            var song = FindSong(state.DetailHash!);
            if (song is not null)
            {
                OpenDetail(song, state.DetailLevel, false);
            }
        }
        ISongDetail? FindSong(string hash)
        {
            foreach (var song in _collections.SelectedCollection)
            {
                if (song.Hash == hash)
                {
                    return song;
                }
            }
            return null;
        }
        /// <param name="detailSong">从详情页进游戏时为那首歌，回来后重新打开它的详情</param>
        void SaveState(ISongDetail? detailSong, ChartLevel detailLevel)
        {
            _savedState = new SavedState()
            {
                CollectionIndex = _collections.SelectedCollectionIndex,
                Query = _query,
                Page = _page,
                ScrollY = _listContent.anchoredPosition.y,
                DetailHash = detailSong?.Hash,
                DetailLevel = detailLevel,
            };
        }
        void Update()
        {
            if (_toastRemaining > 0f)
            {
                _toastRemaining -= Time.unscaledDeltaTime;
                if (_toastRemaining <= 0f)
                {
                    _toast.gameObject.SetActive(false);
                }
            }
            if (_isWaitingShare && ScoreExchange.ShareState != ScoreActionState.Running)
            {
                _isWaitingShare = false;
                ShowToast((ScoreExchange.ShareState == ScoreActionState.Done ? "MAJTEXT_LIST_SHARE_DONE" : "MAJTEXT_LIST_SHARE_FAILED").i18n());
            }
        }
        void LateUpdate()
        {
            UpdateTransitionFade();
            if (_pendingScrollY is float scrollY)
            {
                _pendingScrollY = null;
                SetListScroll(scrollY);
            }
            if (_isHidden || _detailSong is null || _isDetailClosing)
            {
                return;
            }
            // 屏幕方向或尺寸变化时按新宽度重新排列详情页
            var width = _detailViewport.rect.width;
            if (width > 0 && Mathf.Abs(width - _lastDetailWidth) > 1f)
            {
                RenderDetail();
            }
        }
        void OnDestroy()
        {
            IsOpen = false;
            InputManager.SuppressTouchPanelSimulation = false;
            _pageCts.Cancel();
            _levelCts.Cancel();
            _detailCts.Cancel();
            _previewCts.Cancel();
            CancelListMotions();
            CancelDetailMotions();
            CancelAudioMotions();
            ResetPreviewVolume();
            SaveSelectionIfNeeded();
            DestroyCreatedTextures();
            DestroySprite(_heartFilledSprite);
            DestroySprite(_heartOutlineSprite);
        }
        static void DestroySprite(Sprite? sprite)
        {
            if (sprite == null)
            {
                return;
            }
            if (sprite.texture != null)
            {
                Destroy(sprite.texture);
            }
            Destroy(sprite);
        }
        void ResetPreviewVolume()
        {
            if (_previewPlayer != null)
            {
                _previewPlayer.VolumeScale = 1f;
            }
        }
        #endregion

        #region List view
        void BuildListView()
        {
            _listView = CreateRect("ListView", _root);
            Stretch(_listView);
            var background = _listView.gameObject.AddComponent<Image>();
            background.color = PAGE_COLOR;

            // 左侧分类：屏宽的四分之一
            var sidebar = CreateRect("Sidebar", _listView);
            sidebar.anchorMin = new Vector2(0f, 0f);
            sidebar.anchorMax = new Vector2(0.25f, 1f);
            sidebar.offsetMin = Vector2.zero;
            sidebar.offsetMax = Vector2.zero;
            sidebar.gameObject.AddComponent<Image>().color = PAPER_COLOR;
            var sidebarLine = CreateImage(sidebar, LINE_COLOR, 0f);
            sidebarLine.rectTransform.anchorMin = new Vector2(1f, 0f);
            sidebarLine.rectTransform.anchorMax = new Vector2(1f, 1f);
            sidebarLine.rectTransform.offsetMin = new Vector2(-1f, 0f);
            sidebarLine.rectTransform.offsetMax = Vector2.zero;
            // 标题栏：应用图标、“MajdataPlay”和“urntt edition”
            var headerHeight = new LightUi(_font).CreateSidebarHeader(sidebar, "MajdataPlay", "urntt edition");
            var heading = CreateText(sidebar, "MAJTEXT_LIST_CATEGORY".i18n(), 18f, MUTED_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            PlaceRow(heading.rectTransform, 30f, headerHeight + 14f, 20f, 30f);
            _sidebarContent = CreateScroll(sidebar, 0f, headerHeight + 52f, 0f, 0f, out _);
            var sidebarLayout = _sidebarContent.gameObject.AddComponent<VerticalLayoutGroup>();
            sidebarLayout.padding = new RectOffset(16, 16, 0, 24);
            sidebarLayout.spacing = 4f;
            sidebarLayout.childControlWidth = true;
            sidebarLayout.childControlHeight = false;
            sidebarLayout.childForceExpandWidth = true;
            sidebarLayout.childForceExpandHeight = false;
            _sidebarContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 主区域：右侧四分之三
            var main = CreateRect("Main", _listView);
            main.anchorMin = new Vector2(0.25f, 0f);
            main.anchorMax = Vector2.one;
            main.offsetMin = new Vector2(MAIN_LEFT_PADDING, 0f);
            main.offsetMax = new Vector2(-MAIN_RIGHT_PADDING, 0f);

            // 顶部：搜索；难度 −/+；设置、封面
            var topRow = CreateRect("TopRow", main);
            PlaceRow(topRow, 0f, TOP_PADDING, 0f, TOPBAR_HEIGHT);
            _searchField = CreateInputField(topRow, "MAJTEXT_LIST_SEARCH_PLACEHOLDER".i18n());
            PlaceTopLeft((RectTransform)_searchField.transform, 0f, 0f, 420f, TOPBAR_HEIGHT);
            _searchField.onSubmit.AddListener(_ => ApplySearch());
            _searchField.onEndEdit.AddListener(_ => ApplySearch());
            var searchButton = CreateButton(topRow, "MAJTEXT_LIST_SEARCH".i18n(), ACCENT_COLOR, Color.white, 19f, ApplySearch);
            PlaceTopLeft((RectTransform)searchButton.transform, 432f, 0f, 96f, TOPBAR_HEIGHT);

            // 右侧从左到右：封面　−　难度　+　设置（详情页右上角是同一组控件，位置相同）
            _difficultyText = BuildTopControls(topRow, SwitchToCover, () => ChangeGlobalLevel(-1), () => ChangeGlobalLevel(1), EnterSetting);

            _captionText = CreateText(main, string.Empty, 17f, MUTED_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            PlaceRow(_captionText.rectTransform, 2f, CAPTION_TOP, 180f, 30f);
            // 自选分享：进入选择模式后点难度标签勾选谱面
            _selectButton = CreateButton(main, "MAJTEXT_LIST_SELECT_MODE".i18n(), PAPER_COLOR, TEXT_COLOR, 16f, ToggleSelectMode);
            PlaceTopRight((RectTransform)_selectButton.transform, SCROLLBAR_WIDTH + 14f, CAPTION_TOP - 4f, 150f, 38f);
            _selectButtonLabel = _selectButton.GetComponentInChildren<TextMeshProUGUI>();

            // 曲目列表：每行一张卡片，右侧可拖动的滚动条，底部给浮窗留出空间
            _listPane = CreateRect("ListPane", main);
            Stretch(_listPane);
            _listPane.offsetMax = new Vector2(0f, -LIST_TOP);
            _listPaneRestPosition = _listPane.anchoredPosition;
            _listPaneGroup = _listPane.gameObject.AddComponent<CanvasGroup>();
            _listContent = CreateScroll(_listPane, 0f, 0f, 0f, 0f, out _listScroll);
            var listLayout = _listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.padding = new RectOffset(0, (int)(SCROLLBAR_WIDTH + 14f), 0, (int)(PAGER_HEIGHT + PAGER_BOTTOM * 2f));
            listLayout.spacing = CARD_SPACING;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            _listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            AddScrollbar(_listScroll);

            // 底部浮窗：翻页与页码
            // 柔和投影与浮窗同一圆角、中心对齐；浮窗本身是白底加一圈等宽描边
            _pager = LightUi.CreateShadowHolder(main, 20f, 14f, 0.12f);
            _pager.anchorMin = new Vector2(0.5f, 0f);
            _pager.anchorMax = new Vector2(0.5f, 0f);
            _pager.pivot = new Vector2(0.5f, 0f);
            _pager.sizeDelta = new Vector2(300f, PAGER_HEIGHT);
            _pager.anchoredPosition = new Vector2(-(SCROLLBAR_WIDTH + 14f) * 0.5f, PAGER_BOTTOM);
            var pagerInner = CreatePanel(_pager, PAPER_COLOR, 20f);
            Stretch(pagerInner.rectTransform);
            LightUi.AddRing(pagerInner.transform, LINE_COLOR, 20f, 1.5f);
            pagerInner.raycastTarget = true;
            _previousPageButton = CreateButton(pagerInner.rectTransform, "<", PAPER_COLOR, TEXT_COLOR, 26f, () => ChangePage(-1));
            PlaceMiddleLeft((RectTransform)_previousPageButton.transform, 8f, 64f, 48f);
            _nextPageButton = CreateButton(pagerInner.rectTransform, ">", PAPER_COLOR, TEXT_COLOR, 26f, () => ChangePage(1));
            PlaceMiddleRight((RectTransform)_nextPageButton.transform, 8f, 64f, 48f);
            _pageText = CreateText(pagerInner.rectTransform, string.Empty, 21f, TEXT_COLOR, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(_pageText.rectTransform);
            _pageText.rectTransform.offsetMin = new Vector2(76f, 0f);
            _pageText.rectTransform.offsetMax = new Vector2(-76f, 0f);

            // 底部右侧浮窗（选择模式）：已选 N 张　清空　分享
            _selectionBar = LightUi.CreateShadowHolder(main, 20f, 14f, 0.12f);
            _selectionBar.anchorMin = new Vector2(1f, 0f);
            _selectionBar.anchorMax = new Vector2(1f, 0f);
            _selectionBar.pivot = new Vector2(1f, 0f);
            _selectionBar.sizeDelta = new Vector2(420f, PAGER_HEIGHT);
            _selectionBar.anchoredPosition = new Vector2(-(SCROLLBAR_WIDTH + 14f), PAGER_BOTTOM);
            var selectionInner = CreatePanel(_selectionBar, PAPER_COLOR, 20f);
            Stretch(selectionInner.rectTransform);
            LightUi.AddRing(selectionInner.transform, ACCENT_COLOR, 20f, 2f);
            selectionInner.raycastTarget = true;
            _selectionCountText = CreateText(selectionInner.rectTransform, string.Empty, 19f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            Stretch(_selectionCountText.rectTransform);
            _selectionCountText.rectTransform.offsetMin = new Vector2(22f, 0f);
            _selectionCountText.rectTransform.offsetMax = new Vector2(-220f, 0f);
            var shareSelected = CreateButton(selectionInner.rectTransform, "MAJTEXT_LIST_SHARE_SELECTED".i18n(), ACCENT_COLOR, Color.white, 18f, ShowSelectionShareModal);
            PlaceMiddleRight((RectTransform)shareSelected.transform, 10f, 96f, 46f);
            var clearSelected = CreateButton(selectionInner.rectTransform, "MAJTEXT_LIST_CLEAR".i18n(), PAGE_COLOR, TEXT_COLOR, 18f, ClearSelection);
            PlaceMiddleRight((RectTransform)clearSelected.transform, 114f, 96f, 46f);
            _selectionBar.gameObject.SetActive(false);
        }
        /// <summary>
        /// 顶部右侧的一组控件：封面　−　难度　+　设置；返回中间显示难度名的文字
        /// </summary>
        TextMeshProUGUI BuildTopControls(RectTransform parent, UnityAction onCover, UnityAction onDown, UnityAction onUp, UnityAction onSetting)
        {
            var settingButton = CreateButton(parent, "MAJTEXT_LIST_SETTING".i18n(), PAPER_COLOR, TEXT_COLOR, 19f, onSetting);
            PlaceTopRight((RectTransform)settingButton.transform, 0f, 0f, 96f, TOPBAR_HEIGHT);
            var upButton = CreateButton(parent, "+", PAPER_COLOR, TEXT_COLOR, 28f, onUp);
            PlaceTopRight((RectTransform)upButton.transform, 108f, 0f, 60f, TOPBAR_HEIGHT);
            var difficultyText = CreateText(parent, string.Empty, 21f, ACCENT_COLOR, TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceTopRight(difficultyText.rectTransform, 174f, 0f, 150f, TOPBAR_HEIGHT);
            difficultyText.enableAutoSizing = true;
            difficultyText.fontSizeMin = 14f;
            difficultyText.fontSizeMax = 21f;
            var downButton = CreateButton(parent, "−", PAPER_COLOR, TEXT_COLOR, 28f, onDown);
            PlaceTopRight((RectTransform)downButton.transform, 330f, 0f, 60f, TOPBAR_HEIGHT);
            var coverButton = CreateButton(parent, "MAJTEXT_LIST_COVER".i18n(), PAPER_COLOR, TEXT_COLOR, 19f, onCover);
            PlaceTopRight((RectTransform)coverButton.transform, 402f, 0f, 96f, TOPBAR_HEIGHT);
            return difficultyText;
        }
        /// <summary>
        /// 顶部右侧控件组的总宽度（封面左缘到设置右缘）
        /// </summary>
        const float TOP_CONTROLS_WIDTH = 498f;
        void AddScrollbar(ScrollRect scrollRect)
        {
            var bar = CreateRect("Scrollbar", scrollRect.transform);
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 0.5f);
            bar.offsetMin = new Vector2(-SCROLLBAR_WIDTH, 8f);
            bar.offsetMax = new Vector2(0f, -4f);
            var track = bar.gameObject.AddComponent<Image>();
            UiSprites.ApplyRounded(track, SCROLLBAR_WIDTH * 0.5f);
            track.color = new Color(LINE_COLOR.r, LINE_COLOR.g, LINE_COLOR.b, 0.7f);
            var slidingArea = CreateRect("SlidingArea", bar);
            Stretch(slidingArea);
            var handle = CreateImage(slidingArea, new Color(MUTED_COLOR.r, MUTED_COLOR.g, MUTED_COLOR.b, 0.65f), SCROLLBAR_WIDTH * 0.5f);
            handle.raycastTarget = true;
            Stretch(handle.rectTransform);
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var colors = scrollbar.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.8f, 0.82f, 0.9f, 1f);
            scrollbar.colors = colors;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }
        /// <summary>
        /// 重新取当前分类下的曲目并按搜索词筛选
        /// </summary>
        /// <param name="isRestoring">打开时：翻到选中的歌所在的页，并滚到那张卡片</param>
        /// <param name="isAnimated">切换分类、翻页、搜索时列表淡入并稍向上滑入</param>
        void RefreshList(bool isRestoring = false, bool isAnimated = true)
        {
            _filteredSongs.Clear();
            var collection = _collections.SelectedCollection;
            foreach (var song in collection)
            {
                if (IsMatch(song, _query))
                {
                    _filteredSongs.Add(song);
                }
            }
            var scrollToIndex = -1;
            if (isRestoring)
            {
                var hash = MajEnv.RuntimeConfig.List.SelectedSongHash;
                var index = string.IsNullOrEmpty(hash) ? -1 : _filteredSongs.FindIndex(x => x.Hash == hash);
                _page = index < 0 ? 0 : index / PageSize;
                scrollToIndex = index < 0 ? -1 : index % PageSize;
            }
            RenderSidebar();
            RenderPage(isAnimated && !isRestoring);
            if (scrollToIndex > 0)
            {
                RequestListScroll(scrollToIndex * (CARD_HEIGHT + CARD_SPACING));
            }
        }
        static bool IsMatch(ISongDetail song, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }
            if (Contains(song.Title, query) || Contains(song.Artist, query) || Contains(song.Hash, query))
            {
                return true;
            }
            if (song is OnlineSongDetail onlineSong && Contains(onlineSong.Id, query))
            {
                return true;
            }
            foreach (var designer in song.Designers)
            {
                if (Contains(designer, query))
                {
                    return true;
                }
            }
            return false;
        }
        static bool Contains(string? text, string query)
        {
            return !string.IsNullOrEmpty(text) && text!.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        void RenderSidebar()
        {
            ClearChildren(_sidebarContent);
            var selected = _collections.SelectedCollectionIndex;
            for (var i = 0; i < _collections.CollectionCount; i++)
            {
                var collection = _collections.GetCollection(i);
                // 段位不是用来逐首挑选的，不列出
                if (collection.DanInfo is not null)
                {
                    continue;
                }
                var index = i;
                var isSelected = i == selected;
                var item = CreateButton(_sidebarContent, string.Empty, isSelected ? ACCENT_SOFT_COLOR : PAPER_COLOR, TEXT_COLOR, 18f, () => SelectCollection(index));
                var itemRect = (RectTransform)item.transform;
                itemRect.sizeDelta = new Vector2(0f, 54f);
                var label = item.GetComponentInChildren<TextMeshProUGUI>();
                label.text = collection.Name;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.color = isSelected ? ACCENT_COLOR : TEXT_COLOR;
                label.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
                label.rectTransform.offsetMin = new Vector2(16f, 0f);
                label.rectTransform.offsetMax = new Vector2(-70f, 0f);
                var count = CreateText(itemRect, collection.Count.ToString(CultureInfo.InvariantCulture), 15f, isSelected ? ACCENT_COLOR : MUTED_COLOR, TextAlignmentOptions.MidlineRight, FontStyles.Normal);
                count.rectTransform.anchorMin = new Vector2(1f, 0f);
                count.rectTransform.anchorMax = new Vector2(1f, 1f);
                count.rectTransform.offsetMin = new Vector2(-66f, 0f);
                count.rectTransform.offsetMax = new Vector2(-14f, 0f);
            }
        }
        void RenderPage(bool isAnimated)
        {
            _pendingScrollY = null;
            _pageCts.Cancel();
            _pageCts = new CancellationTokenSource();
            _levelCts.Cancel();
            _levelCts = new CancellationTokenSource();
            var token = _pageCts.Token;
            ClearChildren(_listContent);
            _cards.Clear();

            var pageCount = Math.Max(1, (_filteredSongs.Count + PageSize - 1) / PageSize);
            _page = Mathf.Clamp(_page, 0, pageCount - 1);
            _pageText.text = $"{_page + 1} / {pageCount}";
            // 到头循环，只有一页时不能翻
            _previousPageButton.interactable = pageCount > 1;
            _nextPageButton.interactable = pageCount > 1;
            _captionText.text = $"{_collections.SelectedCollection.Name} · {"MAJTEXT_LIST_SONG_COUNT".i18n(_filteredSongs.Count)}";
            var level = GlobalLevel;
            UpdateDifficultyText(level);

            if (_filteredSongs.Count == 0)
            {
                var empty = CreatePanel(_listContent, PAPER_COLOR, 14f);
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 160f;
                var emptyText = CreateText(empty.transform, "MAJTEXT_LIST_EMPTY".i18n(), 19f, MUTED_COLOR, TextAlignmentOptions.Center, FontStyles.Normal);
                Stretch(emptyText.rectTransform, 12f);
            }
            else
            {
                var start = _page * PageSize;
                var end = Math.Min(_filteredSongs.Count, start + PageSize);
                var selectedHash = MajEnv.RuntimeConfig.List.SelectedSongHash;
                for (var i = start; i < end; i++)
                {
                    var song = _filteredSongs[i];
                    _cards.Add(CreateCard(song, level, song.Hash == selectedHash, token));
                }
                LoadCardInfoAsync(_cards.ToArray(), level, _levelCts.Token).Forget();
            }
            SetListScroll(0f);
            if (isAnimated)
            {
                PlayListPaneAnimation();
            }
        }
        void UpdateDifficultyText(ChartLevel level)
        {
            _difficultyText.text = ScoreExchange.GetDifficultyName(level);
            _difficultyText.color = GetDifficultyColor(level);
        }
        void SetListScroll(float y)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_listContent);
            _listScroll.StopMovement();
            var viewportHeight = ((RectTransform)_listContent.parent).rect.height;
            var maxY = Mathf.Max(0f, _listContent.rect.height - viewportHeight);
            var position = _listContent.anchoredPosition;
            position.y = Mathf.Clamp(y, 0f, maxY);
            _listContent.anchoredPosition = position;
        }
        /// <summary>
        /// 打开时恢复滚动位置：马上设置一次，下一帧画布尺寸确定后再设置一次
        /// </summary>
        void RequestListScroll(float y)
        {
            SetListScroll(y);
            _pendingScrollY = y;
        }
        void PlayListPaneAnimation()
        {
            CancelListMotions();
            var rest = _listPaneRestPosition;
            _listPaneGroup.alpha = 0f;
            _listPane.anchoredPosition = rest + new Vector2(0f, -LIST_SLIDE_DISTANCE);
            _listPaneAlphaMotion = LMotion.Create(0f, 1f, LIST_FADE_SEC)
                                          .WithEase(Ease.OutQuad)
                                          .Bind(x => _listPaneGroup.alpha = x);
            _listPaneMotion = LMotion.Create(-LIST_SLIDE_DISTANCE, 0f, LIST_FADE_SEC)
                                     .WithEase(Ease.OutCubic)
                                     .Bind(y => _listPane.anchoredPosition = rest + new Vector2(0f, y));
        }
        void CancelListMotions()
        {
            _listPaneMotion.TryCancel();
            _listPaneAlphaMotion.TryCancel();
            if (_listPaneGroup != null)
            {
                _listPaneGroup.alpha = 1f;
                _listPane.anchoredPosition = _listPaneRestPosition;
            }
        }
        void SelectCollection(int index)
        {
            if (index == _collections.SelectedCollectionIndex)
            {
                return;
            }
            _collections.SelectCollection(index);
            _page = 0;
            RefreshList();
        }
        void ChangeGlobalLevel(int delta)
        {
            // 到头循环：EASY 往下是 UTAGE，UTAGE 往上是 EASY
            const int LEVEL_COUNT = (int)ChartLevel.UTAGE + 1;
            var level = (((int)GlobalLevel + delta) % LEVEL_COUNT + LEVEL_COUNT) % LEVEL_COUNT;
            SetGlobalLevel((ChartLevel)level);
        }
        /// <summary>
        /// 切换全局难度：只更新卡片上与难度有关的内容，不重建列表，滚动位置不变
        /// </summary>
        void SetGlobalLevel(ChartLevel level)
        {
            if (level == GlobalLevel)
            {
                return;
            }
            _collections.SetDifficulty(level);
            level = GlobalLevel;
            UpdateDifficultyText(level);
            _levelCts.Cancel();
            _levelCts = new CancellationTokenSource();
            foreach (var card in _cards)
            {
                UpdateCardLevel(card, level);
            }
            LoadCardInfoAsync(_cards.ToArray(), level, _levelCts.Token).Forget();
        }
        void ChangePage(int delta)
        {
            var pageCount = Math.Max(1, (_filteredSongs.Count + PageSize - 1) / PageSize);
            if (pageCount <= 1)
            {
                return;
            }
            // 到头循环：第一页往前是最后一页
            _page = ((_page + delta) % pageCount + pageCount) % pageCount;
            RenderPage(true);
        }
        void ApplySearch()
        {
            var query = _searchField.text.Trim();
            if (query == _query)
            {
                return;
            }
            _query = query;
            _page = 0;
            RefreshList();
        }
        void ToggleFavorite(ISongDetail song)
        {
            if (SongStorage.IsInMyFavorites(song))
            {
                SongStorage.RemoveFromMyFavorites(song);
            }
            else
            {
                SongStorage.AddToMyFavorites(song);
            }
            var isFavorite = SongStorage.IsInMyFavorites(song);
            foreach (var card in _cards)
            {
                if (ReferenceEquals(card.Song, song))
                {
                    SetHeart(card.Heart, isFavorite);
                }
            }
            if (_detailHeart != null && ReferenceEquals(_detailSong, song))
            {
                SetHeart(_detailHeart, isFavorite);
            }
            RenderSidebar();
        }
        #region Selection
        void ToggleSelectMode()
        {
            _isSelecting = !_isSelecting;
            if (!_isSelecting)
            {
                SaveSelectionIfNeeded();
            }
            UpdateSelectionUi();
        }
        /// <summary>
        /// 卡片上的难度标签：选择模式下勾选或取消这张谱面的这个难度，否则切换全局难度
        /// </summary>
        void OnLevelTagClicked(ISongDetail song, ChartLevel level)
        {
            if (!_isSelecting)
            {
                SetGlobalLevel(level);
                return;
            }
            TogglePick(song, level);
        }
        void TogglePick(ISongDetail song, ChartLevel level)
        {
            ScoreExchange.ToggleSelection(song.Hash, level);
            _isSelectionDirty = true;
            var globalLevel = GlobalLevel;
            foreach (var card in _cards)
            {
                if (ReferenceEquals(card.Song, song))
                {
                    UpdateCardLevel(card, globalLevel);
                }
            }
            UpdateSelectionUi();
        }
        void ClearSelection()
        {
            if (ScoreExchange.SelectionCount == 0)
            {
                return;
            }
            ScoreExchange.ClearSelection();
            _isSelectionDirty = true;
            var level = GlobalLevel;
            foreach (var card in _cards)
            {
                UpdateCardLevel(card, level);
            }
            UpdateSelectionUi();
        }
        void UpdateSelectionUi()
        {
            _selectButtonLabel.text = (_isSelecting ? "MAJTEXT_LIST_SELECT_DONE" : "MAJTEXT_LIST_SELECT_MODE").i18n();
            _selectButtonLabel.color = _isSelecting ? Color.white : TEXT_COLOR;
            ((Image)_selectButton.targetGraphic).color = _isSelecting ? ACCENT_COLOR : PAPER_COLOR;
            _selectionBar.gameObject.SetActive(_isSelecting);
            _selectionCountText.text = "MAJTEXT_LIST_SELECTED".i18n(ScoreExchange.SelectionCount);
            UpdateDetailPickButton();
        }
        /// <summary>
        /// 自选列表保存在设置文件里：退出选择模式、分享前或离开选歌时写入
        /// </summary>
        void SaveSelectionIfNeeded()
        {
            if (!_isSelectionDirty)
            {
                return;
            }
            _isSelectionDirty = false;
            GameManager.RequestSave(this);
        }
        #endregion
        void SetHeart(Image heart, bool isFavorite)
        {
            heart.sprite = isFavorite ? _heartFilledSprite : _heartOutlineSprite;
            heart.color = isFavorite ? FAVORITE_COLOR : MUTED_COLOR;
        }
        void EnterSetting()
        {
            // 从详情页进设置：回来后重新打开这首歌的详情
            var isFromDetail = _detailSong is not null && !_isDetailClosing;
            SaveState(isFromDetail ? _detailSong : null, _detailLevel);
            LeaveList();
            _listManager.EnterSetting();
        }
        /// <summary>
        /// 进游戏或设置前：停止动画与预览，恢复预览音量
        /// </summary>
        void LeaveList()
        {
            _isLeaving = true;
            _detailCts.Cancel();
            _previewCts.Cancel();
            CancelDetailMotions();
            CancelAudioMotions();
            ResetPreviewVolume();
        }
        /// <summary>
        /// 本次临时切回封面轮播；重新进入选歌界面时仍按设置显示列表
        /// </summary>
        void SwitchToCover()
        {
            _isHidden = true;
            IsOpen = false;
            InputManager.SuppressTouchPanelSimulation = false;
            _detailCts.Cancel();
            _previewCts.Cancel();
            _pageCts.Cancel();
            _levelCts.Cancel();
            CancelListMotions();
            CancelDetailMotions();
            CancelAudioMotions();
            ResetPreviewVolume();
            _root.gameObject.SetActive(false);
            RestoreSceneCanvases();
            MajInstances.AudioManager.GetSFX("bgm_select.mp3").SetVolume(MajEnv.Settings.Audio.Volume.BGM);
        }
        /// <summary>
        /// 列表页跟着游戏转场淡入淡出（转场画在 GlobalCanvas 上，被 Overlay 画布挡住，只能这样配合）；转场进行中或正在离开时不接受点击
        /// </summary>
        void UpdateTransitionFade()
        {
            var progress = SceneSwitcher.TransitionProgress;
            _rootGroup.alpha = 1f - progress;
            _rootGroup.blocksRaycasts = !_isLeaving && progress < 0.05f;
        }
        void HideSceneCanvases()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.gameObject.scene != gameObject.scene || canvas.transform.IsChildOf(transform) || !canvas.enabled)
                {
                    continue;
                }
                canvas.enabled = false;
                _hiddenSceneCanvases.Add(canvas);
            }
        }
        void RestoreSceneCanvases()
        {
            foreach (var canvas in _hiddenSceneCanvases)
            {
                if (canvas != null)
                {
                    canvas.enabled = true;
                }
            }
            _hiddenSceneCanvases.Clear();
        }
        #endregion

        #region Modal & toast
        void BuildModal()
        {
            _modal = CreateRect("Modal", _root);
            Stretch(_modal);
            var dim = _modal.gameObject.AddComponent<Image>();
            dim.color = new Color(0.09f, 0.125f, 0.2f, 0.4f);
            var dimButton = _modal.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(HideModal);
            _modal.gameObject.SetActive(false);
        }
        /// <param name="isExport">导出只有 CSV 和 JSON</param>
        void ShowShareModal(bool isExport)
        {
            var song = _detailSong;
            if (song is null)
            {
                return;
            }
            var level = _detailLevel;
            var formats = isExport
                ? new[] { ScoreShareFormatOption.Csv, ScoreShareFormatOption.Json }
                : new[] { ScoreShareFormatOption.Image, ScoreShareFormatOption.Text, ScoreShareFormatOption.Csv, ScoreShareFormatOption.Json };
            ShowFormatModal(isExport ? "MAJTEXT_LIST_CHOOSE_EXPORT" : "MAJTEXT_LIST_CHOOSE_SHARE", formats,
                            format => ScoreExchange.StartShareChart(song, level, format));
        }
        /// <summary>
        /// 选择模式的“分享”：按设置里的“自选排序”分享已选谱面
        /// </summary>
        void ShowSelectionShareModal()
        {
            if (ScoreExchange.SelectionCount == 0)
            {
                return;
            }
            SaveSelectionIfNeeded();
            var formats = new[] { ScoreShareFormatOption.Image, ScoreShareFormatOption.Text, ScoreShareFormatOption.Csv, ScoreShareFormatOption.Json };
            ShowFormatModal("MAJTEXT_LIST_CHOOSE_SHARE", formats, ScoreExchange.StartShareSelection);
        }
        /// <summary>
        /// 选择分享或导出格式的弹窗
        /// </summary>
        void ShowFormatModal(string titleKey, ScoreShareFormatOption[] formats, Action<ScoreShareFormatOption> onPick)
        {
            ClearChildren(_modal);
            var height = 100f + formats.Length * 64f + 74f;
            var panel = CreatePanel(_modal, PAPER_COLOR, 18f);
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(480f, height);
            // 拦截面板上的点击，免得冒泡到背后的遮罩把弹窗关掉
            panel.raycastTarget = true;
            panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            var title = CreateText(panelRect, titleKey.i18n(), 22f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            PlaceRow(title.rectTransform, 28f, 24f, 28f, 38f);
            for (var i = 0; i < formats.Length; i++)
            {
                var format = formats[i];
                var option = CreateButton(panelRect, $"MAJSETTING_PROPERTY_ShareFormat_OPTION_{format}".i18n(), PAGE_COLOR, TEXT_COLOR, 19f, () =>
                {
                    HideModal();
                    _isWaitingShare = true;
                    onPick(format);
                });
                PlaceRow((RectTransform)option.transform, 28f, 86f + i * 64f, 28f, 54f);
            }
            var cancel = CreateButton(panelRect, "MAJTEXT_LIST_CANCEL".i18n(), PAPER_COLOR, MUTED_COLOR, 18f, HideModal);
            PlaceRow((RectTransform)cancel.transform, 28f, height - 74f, 28f, 50f);
            _modal.gameObject.SetActive(true);
            _modal.SetAsLastSibling();
        }
        void HideModal()
        {
            _modal.gameObject.SetActive(false);
        }
        void BuildToast()
        {
            var toast = CreatePanel(_root, Hex(0x343F56), 12f);
            _toast = toast.rectTransform;
            _toast.anchorMin = new Vector2(0.5f, 1f);
            _toast.anchorMax = new Vector2(0.5f, 1f);
            _toast.pivot = new Vector2(0.5f, 1f);
            _toast.sizeDelta = new Vector2(440f, 58f);
            _toast.anchoredPosition = new Vector2(0f, -24f);
            toast.raycastTarget = false;
            _toastText = CreateText(_toast, string.Empty, 18f, Color.white, TextAlignmentOptions.Center, FontStyles.Normal);
            Stretch(_toastText.rectTransform, 10f);
            _toast.gameObject.SetActive(false);
        }
        void ShowToast(string text)
        {
            _toastText.text = text;
            _toast.gameObject.SetActive(true);
            _toast.SetAsLastSibling();
            _toastRemaining = TOAST_DURATION_SEC;
        }
        #endregion
    }
}
