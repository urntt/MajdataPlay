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
        /// <summary>
        /// The top of the song list: below the top bar with a small gap.
        /// </summary>
        const float LIST_TOP = TOP_PADDING + TOPBAR_HEIGHT + 16f;
        /// <summary>
        /// The width of the player nickname and avatar at the right end of the top bar.
        /// </summary>
        const float PROFILE_WIDTH = 300f;
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
        const float PAGER_WIDTH = 300f;
        /// <summary>
        /// The gap between the pager and the search and share buttons beside it.
        /// </summary>
        const float FLOAT_GAP = 12f;
        /// <summary>
        /// The width of the expanded search bar, limited by the space left of the pager.
        /// </summary>
        const float SEARCH_EXPANDED_WIDTH = 420f;
        const float SEARCH_ANIMATION_SEC = 0.2f;
        /// <summary>
        /// The width of the selection bar, limited by the space right of the pager.
        /// </summary>
        const float SELECTION_BAR_MAX_WIDTH = 380f;
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
        TextMeshProUGUI _difficultyText = null!;
        TextMeshProUGUI _pageText = null!;
        TMP_InputField _searchField = null!;
        /// <summary>
        /// The search button left of the pager, which expands into the search bar.
        /// </summary>
        RectTransform _searchHolder = null!;
        Image _searchPanel = null!;
        Image _searchRing = null!;
        Image _searchIcon = null!;
        RectTransform _searchExpandedArea = null!;
        Button _searchClearButton = null!;
        bool _isSearchExpanded = false;
        MotionHandle _searchMotion;
        /// <summary>
        /// The share button right of the pager, which enters selection mode.
        /// </summary>
        RectTransform _shareHolder = null!;
        Button _previousPageButton = null!;
        Button _nextPageButton = null!;
        RectTransform _pager = null!;
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
        static int PageSize => Mathf.Clamp(MajEnv.Settings.Display.ListPageSize, 5, 100);

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
            UpdateSearchBarStyle();
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
            ReloadEvictedCovers();
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
            _searchMotion.TryCancel();
            ResetPreviewVolume();
            SaveSelectionIfNeeded();
            SaveShareSettingsIfNeeded();
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

            // 顶部：左边“封面　−　难度　+　设置”，右边玩家昵称与头像
            var topRow = CreateRect("TopRow", main);
            PlaceRow(topRow, 0f, TOP_PADDING, 0f, TOPBAR_HEIGHT);
            _difficultyText = BuildTopControls(topRow, SwitchToCover, () => ChangeGlobalLevel(-1), () => ChangeGlobalLevel(1), EnterSetting);
            var profile = new LightUi(_font).CreatePlayerProfile(topRow, TOPBAR_HEIGHT, PAGE_COLOR);
            PlaceTopRight(profile, 0f, 0f, PROFILE_WIDTH, TOPBAR_HEIGHT);

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

            // 底部浮窗：中间翻页与页码，左边搜索（点击展开搜索条），右边分享（进入选择模式）
            // 柔和投影与浮窗同一圆角、中心对齐；浮窗本身是白底加一圈等宽描边
            _pager = CreateFloatingHolder(main, new Vector2(0.5f, 0f), PagerCenterX);
            _pager.sizeDelta = new Vector2(PAGER_WIDTH, PAGER_HEIGHT);
            var pagerInner = CreateFloatingPanel(_pager, out _);
            _previousPageButton = CreateButton(pagerInner.rectTransform, "<", PAPER_COLOR, TEXT_COLOR, 26f, () => ChangePage(-1));
            PlaceMiddleLeft((RectTransform)_previousPageButton.transform, 8f, 64f, 48f);
            _nextPageButton = CreateButton(pagerInner.rectTransform, ">", PAPER_COLOR, TEXT_COLOR, 26f, () => ChangePage(1));
            PlaceMiddleRight((RectTransform)_nextPageButton.transform, 8f, 64f, 48f);
            _pageText = CreateText(pagerInner.rectTransform, string.Empty, 21f, TEXT_COLOR, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(_pageText.rectTransform);
            _pageText.rectTransform.offsetMin = new Vector2(76f, 0f);
            _pageText.rectTransform.offsetMax = new Vector2(-76f, 0f);

            BuildSearchBar(main);
            BuildShareButton(main);
            BuildSelectionBar(main);
        }
        /// <summary>
        /// The horizontal center of the floating bars: the middle of the list, not counting the scrollbar.
        /// </summary>
        static float PagerCenterX => -(SCROLLBAR_WIDTH + 14f) * 0.5f;
        /// <summary>
        /// Creates a holder with a soft shadow for a floating bar at the bottom of the list.
        /// </summary>
        /// <param name="parent">The main area of the list.</param>
        /// <param name="pivot">The pivot of the holder; its x decides which edge stays at <paramref name="x"/>.</param>
        /// <param name="x">The horizontal position of the pivot, relative to the center of the main area.</param>
        /// <returns>The holder; set its size afterwards.</returns>
        static RectTransform CreateFloatingHolder(RectTransform parent, Vector2 pivot, float x)
        {
            var holder = LightUi.CreateShadowHolder(parent, 20f, 14f, 0.12f);
            holder.anchorMin = new Vector2(0.5f, 0f);
            holder.anchorMax = new Vector2(0.5f, 0f);
            holder.pivot = pivot;
            holder.sizeDelta = new Vector2(PAGER_HEIGHT, PAGER_HEIGHT);
            holder.anchoredPosition = new Vector2(x, PAGER_BOTTOM);
            return holder;
        }
        /// <summary>
        /// Creates the white rounded panel with a thin outline that fills a floating holder.
        /// </summary>
        /// <param name="holder">The floating holder.</param>
        /// <param name="ring">The outline of the panel.</param>
        /// <returns>The panel, which receives touches so they do not reach the list below.</returns>
        Image CreateFloatingPanel(RectTransform holder, out Image ring)
        {
            var panel = CreatePanel(holder, PAPER_COLOR, 20f);
            Stretch(panel.rectTransform);
            ring = LightUi.AddRing(panel.transform, LINE_COLOR, 20f, 1.5f);
            panel.raycastTarget = true;
            return panel;
        }
        /// <summary>
        /// Creates a transparent button covering <paramref name="parent"/> with a centered white icon.
        /// </summary>
        /// <param name="parent">The area the button covers.</param>
        /// <param name="icon">The icon sprite.</param>
        /// <param name="iconSize">The size of the icon.</param>
        /// <param name="onClick">The action run when the button is tapped.</param>
        /// <param name="iconImage">The icon image, tinted afterwards.</param>
        /// <returns>The button.</returns>
        Button CreateIconButton(RectTransform parent, Sprite icon, float iconSize, UnityAction onClick, out Image iconImage)
        {
            var hit = CreateImage(parent, new Color(0f, 0f, 0f, 0f), 0f);
            hit.gameObject.name = "IconButton";
            hit.raycastTarget = true;
            var button = hit.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);
            iconImage = CreateImage(hit.transform, TEXT_COLOR, 0f);
            iconImage.gameObject.name = "Icon";
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            var iconRect = iconImage.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            iconRect.anchoredPosition = Vector2.zero;
            return button;
        }
        #region Search bar
        /// <summary>
        /// Builds the search button left of the pager. Its right edge stays next to the pager, so it grows to the left
        /// into a search bar with the text field and a clear button.
        /// </summary>
        /// <param name="main">The main area of the list.</param>
        void BuildSearchBar(RectTransform main)
        {
            _searchHolder = CreateFloatingHolder(main, new Vector2(1f, 0f), PagerCenterX - PAGER_WIDTH * 0.5f - FLOAT_GAP);
            _searchPanel = CreateFloatingPanel(_searchHolder, out _searchRing);
            var iconArea = CreateRect("SearchButton", _searchPanel.transform);
            iconArea.anchorMin = new Vector2(1f, 0f);
            iconArea.anchorMax = new Vector2(1f, 1f);
            iconArea.pivot = new Vector2(1f, 0.5f);
            iconArea.sizeDelta = new Vector2(PAGER_HEIGHT, 0f);
            iconArea.anchoredPosition = Vector2.zero;
            CreateIconButton(iconArea, UiSprites.Magnifier, 30f, OnSearchButtonClicked, out _searchIcon);

            _searchExpandedArea = CreateRect("Expanded", _searchPanel.transform);
            Stretch(_searchExpandedArea);
            _searchExpandedArea.offsetMin = new Vector2(10f, 0f);
            _searchExpandedArea.offsetMax = new Vector2(-PAGER_HEIGHT, 0f);
            var clearArea = CreateRect("ClearButton", _searchExpandedArea);
            clearArea.anchorMin = new Vector2(1f, 0.5f);
            clearArea.anchorMax = new Vector2(1f, 0.5f);
            clearArea.pivot = new Vector2(1f, 0.5f);
            clearArea.sizeDelta = new Vector2(44f, 44f);
            clearArea.anchoredPosition = Vector2.zero;
            _searchClearButton = CreateButton(clearArea, "×", PAGE_COLOR, MUTED_COLOR, 24f, ClearSearch);
            Stretch((RectTransform)_searchClearButton.transform);
            _searchField = CreateInputField(_searchExpandedArea, "MAJTEXT_LIST_SEARCH_PLACEHOLDER".i18n());
            var fieldRect = (RectTransform)_searchField.transform;
            Stretch(fieldRect);
            fieldRect.offsetMax = new Vector2(-52f, 0f);
            _searchField.onSubmit.AddListener(_ => OnSearchSubmitted());
            _searchField.onEndEdit.AddListener(_ => ApplySearch());
            _searchField.onValueChanged.AddListener(_ => UpdateSearchBarStyle());
            _searchExpandedArea.gameObject.SetActive(false);
            UpdateSearchBarStyle();
        }
        /// <summary>
        /// Expands the search bar, or applies the search and collapses it when it is already expanded.
        /// </summary>
        void OnSearchButtonClicked()
        {
            if (_isSearchExpanded)
            {
                ApplySearch();
                SetSearchExpanded(false);
                return;
            }
            SetSearchExpanded(true);
            _searchField.ActivateInputField();
        }
        /// <summary>
        /// Applies the search and collapses the search bar when the keyboard submits the text.
        /// </summary>
        void OnSearchSubmitted()
        {
            ApplySearch();
            SetSearchExpanded(false);
        }
        /// <summary>
        /// Empties the search text and shows the whole category again; the search bar stays expanded.
        /// </summary>
        void ClearSearch()
        {
            _searchField.text = string.Empty;
            ApplySearch();
        }
        /// <summary>
        /// Animates the search button between the collapsed square and the expanded search bar.
        /// </summary>
        /// <param name="isExpanded">Whether to show the search bar.</param>
        void SetSearchExpanded(bool isExpanded)
        {
            if (_isSearchExpanded == isExpanded)
            {
                return;
            }
            _isSearchExpanded = isExpanded;
            _searchMotion.TryCancel();
            var main = (RectTransform)_searchHolder.parent;
            // 搜索条从翻页浮窗左边向左展开，不超出主区域
            var available = main.rect.width * 0.5f + _searchHolder.anchoredPosition.x;
            var expandedWidth = Mathf.Clamp(available, PAGER_HEIGHT, SEARCH_EXPANDED_WIDTH);
            var targetWidth = isExpanded ? expandedWidth : PAGER_HEIGHT;
            if (isExpanded)
            {
                _searchExpandedArea.gameObject.SetActive(true);
            }
            _searchMotion = LMotion.Create(_searchHolder.sizeDelta.x, targetWidth, SEARCH_ANIMATION_SEC)
                                   .WithEase(Ease.OutCubic)
                                   .WithOnComplete(() =>
                                   {
                                       if (!_isSearchExpanded && _searchExpandedArea != null)
                                       {
                                           _searchExpandedArea.gameObject.SetActive(false);
                                       }
                                   })
                                   .Bind(width => _searchHolder.sizeDelta = new Vector2(width, PAGER_HEIGHT));
            UpdateSearchBarStyle();
        }
        /// <summary>
        /// Shows whether a search is active: the collapsed button turns blue with a white icon while the list is filtered.
        /// </summary>
        void UpdateSearchBarStyle()
        {
            var isFiltering = !_isSearchExpanded && !string.IsNullOrEmpty(_query);
            _searchPanel.color = isFiltering ? ACCENT_COLOR : PAPER_COLOR;
            _searchRing.enabled = !isFiltering;
            _searchIcon.color = isFiltering ? Color.white : TEXT_COLOR;
            _searchClearButton.gameObject.SetActive(!string.IsNullOrEmpty(_searchField.text));
        }
        #endregion
        #region Share button and selection bar
        /// <summary>
        /// Builds the share button right of the pager, which enters selection mode.
        /// </summary>
        /// <param name="main">The main area of the list.</param>
        void BuildShareButton(RectTransform main)
        {
            _shareHolder = CreateFloatingHolder(main, new Vector2(0f, 0f), SideButtonsRightX);
            var panel = CreateFloatingPanel(_shareHolder, out _);
            CreateIconButton(panel.rectTransform, UiSprites.Share, 30f, ShowShareDialog, out _);
        }
        /// <summary>
        /// The left edge of the share button and the selection bar: right of the pager.
        /// </summary>
        static float SideButtonsRightX => PagerCenterX + PAGER_WIDTH * 0.5f + FLOAT_GAP;
        /// <summary>
        /// Builds the selection bar shown in selection mode in place of the share button:
        /// the number of selected charts, then Clear and Done.
        /// </summary>
        /// <param name="main">The main area of the list.</param>
        void BuildSelectionBar(RectTransform main)
        {
            const float BUTTON_WIDTH = 84f;
            const float BUTTON_GAP = 8f;
            const float EDGE = 10f;
            _selectionBar = CreateFloatingHolder(main, new Vector2(0f, 0f), SideButtonsRightX);
            _selectionBar.sizeDelta = new Vector2(SELECTION_BAR_MAX_WIDTH, PAGER_HEIGHT);
            var selectionInner = CreatePanel(_selectionBar, PAPER_COLOR, 20f);
            Stretch(selectionInner.rectTransform);
            LightUi.AddRing(selectionInner.transform, ACCENT_COLOR, 20f, 2f);
            selectionInner.raycastTarget = true;
            var done = CreateButton(selectionInner.rectTransform, "MAJTEXT_LIST_SELECT_DONE".i18n(), ACCENT_COLOR, Color.white, 18f, ExitSelectMode);
            PlaceMiddleRight((RectTransform)done.transform, EDGE, BUTTON_WIDTH, 46f);
            var clearSelected = CreateButton(selectionInner.rectTransform, "MAJTEXT_LIST_CLEAR".i18n(), PAGE_COLOR, TEXT_COLOR, 18f, ClearSelection);
            PlaceMiddleRight((RectTransform)clearSelected.transform, EDGE + BUTTON_WIDTH + BUTTON_GAP, BUTTON_WIDTH, 46f);
            _selectionCountText = CreateText(selectionInner.rectTransform, string.Empty, 19f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            Stretch(_selectionCountText.rectTransform);
            _selectionCountText.rectTransform.offsetMin = new Vector2(20f, 0f);
            _selectionCountText.rectTransform.offsetMax = new Vector2(-(EDGE + (BUTTON_WIDTH + BUTTON_GAP) * 2f), 0f);
            _selectionCountText.enableAutoSizing = true;
            _selectionCountText.fontSizeMin = 13f;
            _selectionCountText.fontSizeMax = 19f;
            _selectionBar.gameObject.SetActive(false);
        }
        /// <summary>
        /// Fits the selection bar into the space right of the pager.
        /// </summary>
        void FitSelectionBar()
        {
            var main = (RectTransform)_selectionBar.parent;
            var available = main.rect.width * 0.5f - SideButtonsRightX - (SCROLLBAR_WIDTH + 14f);
            var width = available > 0f ? Mathf.Min(SELECTION_BAR_MAX_WIDTH, available) : SELECTION_BAR_MAX_WIDTH;
            _selectionBar.sizeDelta = new Vector2(width, PAGER_HEIGHT);
        }
        #endregion
        /// <summary>
        /// Builds the top bar controls from the left: Cover, −, the difficulty name, + and Settings.
        /// </summary>
        /// <param name="parent">The top bar; the controls start at its left edge.</param>
        /// <param name="onCover">Switches back to the cover carousel.</param>
        /// <param name="onDown">Selects the previous difficulty.</param>
        /// <param name="onUp">Selects the next difficulty.</param>
        /// <param name="onSetting">Opens the settings.</param>
        /// <returns>The text showing the difficulty name.</returns>
        TextMeshProUGUI BuildTopControls(RectTransform parent, UnityAction onCover, UnityAction onDown, UnityAction onUp, UnityAction onSetting)
        {
            var coverButton = CreateButton(parent, "MAJTEXT_LIST_COVER".i18n(), PAPER_COLOR, TEXT_COLOR, 19f, onCover);
            PlaceTopLeft((RectTransform)coverButton.transform, 0f, 0f, 96f, TOPBAR_HEIGHT);
            var downButton = CreateButton(parent, "−", PAPER_COLOR, TEXT_COLOR, 28f, onDown);
            PlaceTopLeft((RectTransform)downButton.transform, 108f, 0f, 60f, TOPBAR_HEIGHT);
            var difficultyText = CreateText(parent, string.Empty, 21f, ACCENT_COLOR, TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceTopLeft(difficultyText.rectTransform, 174f, 0f, 150f, TOPBAR_HEIGHT);
            difficultyText.enableAutoSizing = true;
            difficultyText.fontSizeMin = 14f;
            difficultyText.fontSizeMax = 21f;
            var upButton = CreateButton(parent, "+", PAPER_COLOR, TEXT_COLOR, 28f, onUp);
            PlaceTopLeft((RectTransform)upButton.transform, 330f, 0f, 60f, TOPBAR_HEIGHT);
            var settingButton = CreateButton(parent, "MAJTEXT_LIST_SETTING".i18n(), PAPER_COLOR, TEXT_COLOR, 19f, onSetting);
            PlaceTopLeft((RectTransform)settingButton.transform, 402f, 0f, 96f, TOPBAR_HEIGHT);
            return difficultyText;
        }
        /// <summary>
        /// The total width of the top bar controls, from the left edge of Cover to the right edge of Settings.
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
                var id = MajEnv.RuntimeConfig.List.SelectedSongId;
                var index = string.IsNullOrEmpty(id) ? -1 : _filteredSongs.FindIndex(x => x.Id == id);
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
                var selectedId = MajEnv.RuntimeConfig.List.SelectedSongId;
                for (var i = start; i < end; i++)
                {
                    var song = _filteredSongs[i];
                    _cards.Add(CreateCard(song, level, !string.IsNullOrEmpty(selectedId) && song.Id == selectedId, token));
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
                UpdateSearchBarStyle();
                return;
            }
            _query = query;
            _page = 0;
            UpdateSearchBarStyle();
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
        /// <summary>
        /// Enters selection mode: tapping difficulty tags then picks or unpicks charts to share.
        /// </summary>
        void EnterSelectMode()
        {
            SetSelectMode(true);
        }
        /// <summary>
        /// Leaves selection mode from the Done button, keeping and saving the picked charts.
        /// Returns to the share dialog when the charts were picked from it.
        /// </summary>
        void ExitSelectMode()
        {
            SetSelectMode(false);
            if (_isPickingForShare)
            {
                _isPickingForShare = false;
                ShowShareDialog();
            }
        }
        /// <summary>
        /// Switches selection mode and rebuilds the difficulty tags on this page, which show "+" or "−" while selecting.
        /// </summary>
        /// <param name="isSelecting">Whether selection mode is on.</param>
        void SetSelectMode(bool isSelecting)
        {
            if (_isSelecting == isSelecting)
            {
                return;
            }
            _isSelecting = isSelecting;
            if (!_isSelecting)
            {
                SaveSelectionIfNeeded();
            }
            var level = GlobalLevel;
            foreach (var card in _cards)
            {
                UpdateCardLevel(card, level);
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
            // 选择模式下分享键让位给选择浮窗：已选 N 张　清空　分享　完成
            _shareHolder.gameObject.SetActive(!_isSelecting);
            if (_isSelecting && !_selectionBar.gameObject.activeSelf)
            {
                FitSelectionBar();
            }
            _selectionBar.gameObject.SetActive(_isSelecting);
            _selectionCountText.text = "MAJTEXT_LIST_SELECTED".i18n(ScoreExchange.SelectionCount);
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
        #region Share dialog
        /// <summary>
        /// The width of the share dialog.
        /// </summary>
        const float SHARE_DIALOG_WIDTH = 560f;
        /// <summary>
        /// The height of one option row in the share dialog.
        /// </summary>
        const float SHARE_ROW_HEIGHT = 58f;
        /// <summary>
        /// The share scopes offered by the dialog, in cycling order.
        /// </summary>
        static readonly ScoreShareScopeOption[] SHARE_DIALOG_SCOPES = { ScoreShareScopeOption.Best, ScoreShareScopeOption.All, ScoreShareScopeOption.Custom };
        /// <summary>
        /// Whether the Done button of selection mode reopens the share dialog.
        /// </summary>
        bool _isPickingForShare = false;
        /// <summary>
        /// Whether the share options were changed in the dialog since they were last saved.
        /// </summary>
        bool _isShareSettingsDirty = false;
        /// <summary>
        /// Whether the share dialog is the content of the modal.
        /// </summary>
        bool _isShareDialogShown = false;

        /// <summary>
        /// Shows the share dialog: the share options, which are saved with the settings, then Cancel and Share.
        /// The rows follow the share scope, so changing the scope rebuilds the dialog.
        /// </summary>
        void ShowShareDialog()
        {
            var options = MajEnv.Settings.Score;
            if (Array.IndexOf(SHARE_DIALOG_SCOPES, options.ShareScope) < 0)
            {
                // “当前谱面”改由详情页的“分享成绩”负责
                options.ShareScope = ScoreShareScopeOption.Best;
                _isShareSettingsDirty = true;
            }
            var scope = options.ShareScope;
            var rowCount = scope switch
            {
                ScoreShareScopeOption.Best => 5,
                ScoreShareScopeOption.All => 4,
                _ => 5,
            };
            var height = 24f + 50f + rowCount * SHARE_ROW_HEIGHT + 20f + 56f + 24f;
            var panel = CreateModalPanel(SHARE_DIALOG_WIDTH, height);
            var title = CreateText(panel, "MAJTEXT_LIST_SHARE_DIALOG".i18n(), 22f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            PlaceRow(title.rectTransform, 28f, 24f, 28f, 38f);

            var y = 74f;
            AddCycleRow(panel, ref y, "ShareScope", SHARE_DIALOG_SCOPES, () => options.ShareScope, value =>
            {
                options.ShareScope = value;
                ShowShareDialog();
            });
            if (scope == ScoreShareScopeOption.Best)
            {
                AddCountRow(panel, ref y, options);
            }
            if (scope is ScoreShareScopeOption.Best or ScoreShareScopeOption.All)
            {
                AddCycleRow(panel, ref y, "ShareSort", (ScoreShareSortOption[])Enum.GetValues(typeof(ScoreShareSortOption)),
                            () => options.ShareSort, value => options.ShareSort = value);
            }
            if (scope == ScoreShareScopeOption.Custom)
            {
                AddPickRow(panel, ref y);
                AddCycleRow(panel, ref y, "ShareCustomSort", (ScoreShareCustomSortOption[])Enum.GetValues(typeof(ScoreShareCustomSortOption)),
                            () => options.ShareCustomSort, value => options.ShareCustomSort = value);
            }
            AddCycleRow(panel, ref y, "ShareFormat", (ScoreShareFormatOption[])Enum.GetValues(typeof(ScoreShareFormatOption)),
                        () => options.ShareFormat, value => options.ShareFormat = value);
            AddCycleRow(panel, ref y, "ScoreStore", (ScoreStoreOption[])Enum.GetValues(typeof(ScoreStoreOption)),
                        () => options.ScoreStore, value => options.ScoreStore = value);

            var buttonWidth = (SHARE_DIALOG_WIDTH - 28f * 2f - 12f) * 0.5f;
            var cancel = CreateButton(panel, "MAJTEXT_LIST_CANCEL".i18n(), PAGE_COLOR, TEXT_COLOR, 19f, HideModal);
            PlaceTopLeft((RectTransform)cancel.transform, 28f, height - 24f - 56f, buttonWidth, 56f);
            var confirm = CreateButton(panel, "MAJTEXT_LIST_CONFIRM_SHARE".i18n(), ACCENT_COLOR, Color.white, 19f, ConfirmShare);
            PlaceTopLeft((RectTransform)confirm.transform, 28f + buttonWidth + 12f, height - 24f - 56f, buttonWidth, 56f);
            confirm.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            // 自选但一张都没选时不能分享
            confirm.interactable = scope != ScoreShareScopeOption.Custom || ScoreExchange.SelectionCount > 0;
            _isShareDialogShown = true;
        }
        /// <summary>
        /// Clears the modal and creates a centered white panel that keeps taps from closing the modal.
        /// </summary>
        /// <param name="width">The width of the panel.</param>
        /// <param name="height">The height of the panel.</param>
        /// <returns>The panel rectangle; place the content inside it.</returns>
        RectTransform CreateModalPanel(float width, float height)
        {
            ClearChildren(_modal);
            _isShareDialogShown = false;
            _feedbackSession = null;
            var panel = CreatePanel(_modal, PAPER_COLOR, 18f);
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(width, height);
            // 拦截面板上的点击，免得冒泡到背后的遮罩把弹窗关掉
            panel.raycastTarget = true;
            panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            _modal.gameObject.SetActive(true);
            _modal.SetAsLastSibling();
            return panelRect;
        }
        /// <summary>
        /// Adds an option row with its name on the left and "&lt; value &gt;" on the right; the arrows cycle through the values.
        /// </summary>
        /// <typeparam name="T">The option type.</typeparam>
        /// <param name="panel">The dialog panel.</param>
        /// <param name="y">The top of the row; moved below the row afterwards.</param>
        /// <param name="property">The setting property name, used for the translation keys of the name and the values.</param>
        /// <param name="values">The values to cycle through.</param>
        /// <param name="getValue">Reads the current value.</param>
        /// <param name="setValue">Stores a new value.</param>
        void AddCycleRow<T>(RectTransform panel, ref float y, string property, T[] values, Func<T> getValue, Action<T> setValue) where T : struct, Enum
        {
            var valueText = AddRowControls(panel, y, $"MAJSETTING_PROPERTY_{property}".i18n(), out var previous, out var next);
            void Show()
            {
                valueText.text = $"MAJSETTING_PROPERTY_{property}_OPTION_{getValue()}".i18n();
            }
            void Step(int direction)
            {
                var index = Array.IndexOf(values, getValue());
                index = ((index < 0 ? 0 : index + direction) % values.Length + values.Length) % values.Length;
                _isShareSettingsDirty = true;
                setValue(values[index]);
                if (valueText != null)
                {
                    Show();
                }
            }
            previous.onClick.AddListener(() => Step(-1));
            next.onClick.AddListener(() => Step(1));
            Show();
            y += SHARE_ROW_HEIGHT;
        }
        /// <summary>
        /// Adds the row of the number of best charts to share, with "−" and "+" that repeat while held.
        /// </summary>
        /// <param name="panel">The dialog panel.</param>
        /// <param name="y">The top of the row; moved below the row afterwards.</param>
        /// <param name="options">The score options holding the number.</param>
        void AddCountRow(RectTransform panel, ref float y, ScoreOptions options)
        {
            const int MIN = 5;
            const int MAX = 200;
            const int STEP = 5;
            var valueText = AddRowControls(panel, y, "MAJSETTING_PROPERTY_ShareBestCount".i18n(), out var minus, out var plus);
            minus.GetComponentInChildren<TextMeshProUGUI>().text = "−";
            plus.GetComponentInChildren<TextMeshProUGUI>().text = "+";
            void Show()
            {
                valueText.text = options.ShareBestCount.ToString(CultureInfo.InvariantCulture);
            }
            void Step(int direction)
            {
                options.ShareBestCount = Mathf.Clamp(options.ShareBestCount + direction * STEP, MIN, MAX);
                _isShareSettingsDirty = true;
                Show();
            }
            // 按下即调整，按住连续调整
            minus.gameObject.AddComponent<RepeatButton>().OnRepeat = () => Step(-1);
            plus.gameObject.AddComponent<RepeatButton>().OnRepeat = () => Step(1);
            Show();
            y += SHARE_ROW_HEIGHT;
        }
        /// <summary>
        /// Adds the row that tells the player to tap difficulties, with how many charts are picked, and the button that enters
        /// selection mode: "Start Picking" while nothing is picked, otherwise "Edit Picks".
        /// </summary>
        /// <param name="panel">The dialog panel.</param>
        /// <param name="y">The top of the row; moved below the row afterwards.</param>
        void AddPickRow(RectTransform panel, ref float y)
        {
            const float BUTTON_WIDTH = 140f;
            const float GAP = 12f;
            var selectionCount = ScoreExchange.SelectionCount;
            var hint = CreateText(panel, "MAJTEXT_LIST_PICK_HINT".i18n(selectionCount), 19f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 13f;
            hint.fontSizeMax = 19f;
            // 左边与上下各行的项目名对齐，右边留给按钮
            PlaceTopLeft(hint.rectTransform, 28f, y, SHARE_DIALOG_WIDTH - 28f * 2f - BUTTON_WIDTH - GAP, SHARE_ROW_HEIGHT - 8f);
            var buttonKey = selectionCount > 0 ? "MAJTEXT_LIST_EDIT_PICK" : "MAJTEXT_LIST_START_PICK";
            var pick = CreateButton(panel, buttonKey.i18n(), ACCENT_SOFT_COLOR, ACCENT_COLOR, 18f, PickChartsForShare);
            pick.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            // 右边与上下各行的“< 值 >”对齐
            PlaceTopRight((RectTransform)pick.transform, 28f, y + 2f, BUTTON_WIDTH, SHARE_ROW_HEIGHT - 12f);
            y += SHARE_ROW_HEIGHT;
        }
        /// <summary>
        /// Creates the name and "&lt; value &gt;" controls of one dialog row.
        /// </summary>
        /// <param name="panel">The dialog panel.</param>
        /// <param name="y">The top of the row.</param>
        /// <param name="name">The option name shown on the left.</param>
        /// <param name="previous">The left button.</param>
        /// <param name="next">The right button.</param>
        /// <returns>The text showing the value between the buttons.</returns>
        TextMeshProUGUI AddRowControls(RectTransform panel, float y, string name, out Button previous, out Button next)
        {
            const float BUTTON_WIDTH = 52f;
            const float VALUE_WIDTH = 196f;
            var height = SHARE_ROW_HEIGHT - 12f;
            var label = CreateText(panel, name, 19f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            PlaceTopLeft(label.rectTransform, 28f, y, SHARE_DIALOG_WIDTH - 28f * 2f - BUTTON_WIDTH * 2f - VALUE_WIDTH, SHARE_ROW_HEIGHT - 8f);
            next = CreateButton(panel, ">", PAGE_COLOR, TEXT_COLOR, 22f, null);
            PlaceTopRight((RectTransform)next.transform, 28f, y + 2f, BUTTON_WIDTH, height);
            var value = CreateText(panel, string.Empty, 18f, ACCENT_COLOR, TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceTopRight(value.rectTransform, 28f + BUTTON_WIDTH, y + 2f, VALUE_WIDTH, height);
            value.enableAutoSizing = true;
            value.fontSizeMin = 13f;
            value.fontSizeMax = 18f;
            previous = CreateButton(panel, "<", PAGE_COLOR, TEXT_COLOR, 22f, null);
            PlaceTopRight((RectTransform)previous.transform, 28f + BUTTON_WIDTH + VALUE_WIDTH, y + 2f, BUTTON_WIDTH, height);
            return value;
        }
        /// <summary>
        /// Closes the share dialog and enters selection mode; Done then brings the dialog back.
        /// </summary>
        void PickChartsForShare()
        {
            _isPickingForShare = true;
            HideModal();
            EnterSelectMode();
        }
        /// <summary>
        /// Saves the share options and shares the scores with them.
        /// </summary>
        void ConfirmShare()
        {
            var options = MajEnv.Settings.Score;
            if (options.ShareScope == ScoreShareScopeOption.Custom && ScoreExchange.SelectionCount == 0)
            {
                return;
            }
            HideModal();
            SaveSelectionIfNeeded();
            _isWaitingShare = true;
            ScoreExchange.StartShare(options);
        }
        /// <summary>
        /// Saves the settings when the share options were changed in the dialog.
        /// </summary>
        void SaveShareSettingsIfNeeded()
        {
            if (!_isShareSettingsDirty)
            {
                return;
            }
            _isShareSettingsDirty = false;
            GameManager.RequestSave(this);
        }
        #endregion
        /// <summary>
        /// 选择分享或导出格式的弹窗
        /// </summary>
        void ShowFormatModal(string titleKey, ScoreShareFormatOption[] formats, Action<ScoreShareFormatOption> onPick)
        {
            var height = 100f + formats.Length * 64f + 74f;
            var panelRect = CreateModalPanel(480f, height);
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
        }
        /// <summary>
        /// Closes the modal; the share options changed in the share dialog are kept and saved.
        /// </summary>
        void HideModal()
        {
            _modal.gameObject.SetActive(false);
            _feedbackSession = null;
            if (_isShareDialogShown)
            {
                _isShareDialogShown = false;
                SaveShareSettingsIfNeeded();
            }
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
