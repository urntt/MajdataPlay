using Cysharp.Threading.Tasks;
using MajdataPlay.Collections;
using MajdataPlay.Diagnostics;
using MajdataPlay.IO;
using MajdataPlay.Scenes.Game;
using MajdataPlay.Settings;
using MajdataPlay.Utils;
using MajdataPlay.Utils.ChartRadar;
using MajSimai;
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
    /// 列表式选歌：左侧分类、顶部搜索与全局难度、分页的曲目卡片，点卡片进入谱面详情（雷达图、物量分布、分享与导出、游玩）。
    /// 整个界面用代码搭建在一个全屏画布上；打开期间触摸不再模拟圆圈上的按键，由 EventSystem 处理点击、滑动和输入
    /// </summary>
    internal sealed class SongListBrowser : MonoBehaviour
    {
        /// <summary>
        /// 列表式选歌是否正在显示：显示期间 ListManager 不处理圆圈按键，封面轮播在后台切歌也不播放预览
        /// </summary>
        public static bool IsOpen { get; set; } = false;

        static readonly int[] PAGE_SIZES = { 12, 24, 48 };
        static readonly string[] LEVEL_SHORT_NAMES = { "EZ", "BAS", "ADV", "EXP", "MAS", "ReM", "UTA" };

        static readonly Color PAGE_COLOR = Hex(0xF7F8FA);
        static readonly Color PAPER_COLOR = Color.white;
        static readonly Color TEXT_COLOR = Hex(0x202630);
        static readonly Color MUTED_COLOR = Hex(0x788391);
        static readonly Color LINE_COLOR = Hex(0xE7E9EE);
        static readonly Color ACCENT_COLOR = Hex(0x586FDD);
        static readonly Color ACCENT_SOFT_COLOR = Hex(0xEEF1FF);
        static readonly Color FAVORITE_COLOR = Hex(0xD8677C);
        static readonly Color SPECIAL_COLOR = Hex(0x8A659B);
        static readonly Color SPECIAL_SOFT_COLOR = Hex(0xF4EFF8);

        const float SIDEBAR_WIDTH = 260f;
        const float TOPBAR_HEIGHT = 76f;
        const float TOOLBAR_HEIGHT = 60f;
        const float MAIN_PADDING = 28f;
        const float CARD_HEIGHT = 236f;
        const float CARD_MIN_WIDTH = 420f;
        const float CARD_GAP = 14f;
        const int MAX_COLUMNS = 4;
        const float DETAIL_HEIGHT = 940f;
        const float TOAST_DURATION_SEC = 2.4f;

        ListManager _listManager = null!;
        CollectionListManager _collections = null!;
        PreviewSoundPlayer? _previewPlayer;
        TMP_FontAsset? _font;
        Sprite _roundedSprite = null!;

        RectTransform _root = null!;
        RectTransform _listView = null!;
        RectTransform _sidebarContent = null!;
        RectTransform _gridViewport = null!;
        RectTransform _gridContent = null!;
        ScrollRect _gridScroll = null!;
        GridLayoutGroup _gridLayout = null!;
        TextMeshProUGUI _captionText = null!;
        TextMeshProUGUI _difficultyText = null!;
        TextMeshProUGUI _pageText = null!;
        TextMeshProUGUI _pageSizeText = null!;
        TMP_InputField _searchField = null!;
        Button _previousPageButton = null!;
        Button _nextPageButton = null!;

        RectTransform _detailView = null!;
        RectTransform _detailContent = null!;
        ScrollRect _detailScroll = null!;
        RectTransform _modal = null!;
        RectTransform _toast = null!;
        TextMeshProUGUI _toastText = null!;

        readonly List<ISongDetail> _filteredSongs = new();
        readonly List<Texture> _createdTextures = new();
        int _page = 0;
        int _pageSizeIndex = 1;
        string _query = string.Empty;
        float _lastGridWidth = -1f;
        float _toastRemaining = 0f;
        bool _isWaitingShare = false;
        bool _isHidden = false;

        CancellationTokenSource _pageCts = new();
        CancellationTokenSource _detailCts = new();
        ISongDetail? _detailSong;
        ChartLevel _detailLevel;

        static ChartLevel GlobalLevel => MajEnv.RuntimeConfig.List.SelectedDiff;
        int PageSize => PAGE_SIZES[_pageSizeIndex];

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
            _roundedSprite = CreateRoundedSprite();
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

            BuildListView();
            BuildDetailView();
            BuildModal();
            BuildToast();

            IsOpen = true;
            InputManager.SuppressTouchPanelSimulation = true;
            RefreshList(true);
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
            if (_isHidden || !_listView.gameObject.activeSelf)
            {
                return;
            }
            // 屏幕方向或尺寸变化时按新宽度重新排列卡片
            var width = _gridViewport.rect.width;
            if (width > 0 && Mathf.Abs(width - _lastGridWidth) > 1f)
            {
                RenderPage();
            }
        }
        void OnDestroy()
        {
            IsOpen = false;
            InputManager.SuppressTouchPanelSimulation = false;
            _pageCts.Cancel();
            _detailCts.Cancel();
            DestroyCreatedTextures();
        }
        #endregion

        #region List view
        void BuildListView()
        {
            _listView = CreateRect("ListView", _root);
            Stretch(_listView);
            var background = _listView.gameObject.AddComponent<Image>();
            background.color = PAGE_COLOR;

            // 左侧分类
            var sidebar = CreateRect("Sidebar", _listView);
            sidebar.anchorMin = new Vector2(0f, 0f);
            sidebar.anchorMax = new Vector2(0f, 1f);
            sidebar.pivot = new Vector2(0f, 0.5f);
            sidebar.offsetMin = Vector2.zero;
            sidebar.offsetMax = new Vector2(SIDEBAR_WIDTH, 0f);
            sidebar.gameObject.AddComponent<Image>().color = PAPER_COLOR;
            var sidebarLine = CreateImage(sidebar, LINE_COLOR, 0f);
            sidebarLine.rectTransform.anchorMin = new Vector2(1f, 0f);
            sidebarLine.rectTransform.anchorMax = new Vector2(1f, 1f);
            sidebarLine.rectTransform.offsetMin = new Vector2(-1f, 0f);
            sidebarLine.rectTransform.offsetMax = Vector2.zero;
            var heading = CreateText(sidebar, "MAJTEXT_LIST_CATEGORY".i18n(), 18f, TEXT_COLOR, TextAlignmentOptions.Left, FontStyles.Bold);
            PlaceRow(heading.rectTransform, 28f, 28f, 20f, 30f);
            _sidebarContent = CreateScroll(sidebar, 0f, 72f, 0f, 0f, out _);
            var sidebarLayout = _sidebarContent.gameObject.AddComponent<VerticalLayoutGroup>();
            sidebarLayout.padding = new RectOffset(14, 14, 0, 24);
            sidebarLayout.spacing = 4f;
            sidebarLayout.childControlWidth = true;
            sidebarLayout.childControlHeight = false;
            sidebarLayout.childForceExpandWidth = true;
            sidebarLayout.childForceExpandHeight = false;
            _sidebarContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 主区域
            var main = CreateRect("Main", _listView);
            main.anchorMin = Vector2.zero;
            main.anchorMax = Vector2.one;
            main.offsetMin = new Vector2(SIDEBAR_WIDTH + MAIN_PADDING, MAIN_PADDING);
            main.offsetMax = new Vector2(-MAIN_PADDING, -MAIN_PADDING * 0.8f);

            // 顶部：搜索、难度、设置、封面
            var topRow = CreateRect("TopRow", main);
            PlaceRow(topRow, 0f, 0f, 0f, TOPBAR_HEIGHT - 20f);
            _searchField = CreateInputField(topRow, "MAJTEXT_LIST_SEARCH_PLACEHOLDER".i18n());
            PlaceTopLeft((RectTransform)_searchField.transform, 0f, 0f, 460f, 52f);
            _searchField.onSubmit.AddListener(_ => ApplySearch());
            _searchField.onEndEdit.AddListener(_ => ApplySearch());
            var searchButton = CreateButton(topRow, "MAJTEXT_LIST_SEARCH".i18n(), ACCENT_COLOR, Color.white, 18f, ApplySearch);
            PlaceTopLeft((RectTransform)searchButton.transform, 472f, 0f, 96f, 52f);

            var coverButton = CreateButton(topRow, "MAJTEXT_LIST_COVER".i18n(), PAPER_COLOR, TEXT_COLOR, 18f, SwitchToCover);
            PlaceTopRight((RectTransform)coverButton.transform, 0f, 0f, 96f, 52f);
            var settingButton = CreateButton(topRow, "MAJTEXT_LIST_SETTING".i18n(), PAPER_COLOR, TEXT_COLOR, 18f, () => _listManager.EnterSetting());
            PlaceTopRight((RectTransform)settingButton.transform, 108f, 0f, 96f, 52f);
            var upButton = CreateButton(topRow, "MAJTEXT_LIST_DIFF_UP".i18n(), PAPER_COLOR, TEXT_COLOR, 18f, () => ChangeGlobalLevel(1));
            PlaceTopRight((RectTransform)upButton.transform, 228f, 0f, 108f, 52f);
            _difficultyText = CreateText(topRow, string.Empty, 20f, ACCENT_COLOR, TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceTopRight(_difficultyText.rectTransform, 342f, 0f, 130f, 52f);
            var downButton = CreateButton(topRow, "MAJTEXT_LIST_DIFF_DOWN".i18n(), PAPER_COLOR, TEXT_COLOR, 18f, () => ChangeGlobalLevel(-1));
            PlaceTopRight((RectTransform)downButton.transform, 478f, 0f, 108f, 52f);

            // 工具栏：曲目数、每页数量、翻页
            var toolbar = CreateRect("Toolbar", main);
            PlaceRow(toolbar, 0f, TOPBAR_HEIGHT, 0f, TOOLBAR_HEIGHT - 12f);
            _captionText = CreateText(toolbar, string.Empty, 17f, MUTED_COLOR, TextAlignmentOptions.Left, FontStyles.Normal);
            PlaceRow(_captionText.rectTransform, 0f, 0f, 420f, 48f);
            _nextPageButton = CreateButton(toolbar, ">", PAPER_COLOR, TEXT_COLOR, 20f, () => ChangePage(1));
            PlaceTopRight((RectTransform)_nextPageButton.transform, 0f, 0f, 52f, 48f);
            _pageText = CreateText(toolbar, string.Empty, 17f, MUTED_COLOR, TextAlignmentOptions.Center, FontStyles.Normal);
            PlaceTopRight(_pageText.rectTransform, 58f, 0f, 96f, 48f);
            _previousPageButton = CreateButton(toolbar, "<", PAPER_COLOR, TEXT_COLOR, 20f, () => ChangePage(-1));
            PlaceTopRight((RectTransform)_previousPageButton.transform, 160f, 0f, 52f, 48f);
            var pageSizeButton = CreateButton(toolbar, string.Empty, PAPER_COLOR, TEXT_COLOR, 17f, CyclePageSize);
            PlaceTopRight((RectTransform)pageSizeButton.transform, 224f, 0f, 120f, 48f);
            _pageSizeText = pageSizeButton.GetComponentInChildren<TextMeshProUGUI>();
            var toolbarLine = CreateImage(main, LINE_COLOR, 0f);
            PlaceRow(toolbarLine.rectTransform, 0f, TOPBAR_HEIGHT + TOOLBAR_HEIGHT - 4f, 0f, 1f);

            // 曲目卡片
            _gridContent = CreateScroll(main, 0f, TOPBAR_HEIGHT + TOOLBAR_HEIGHT + 12f, 0f, 0f, out _gridScroll);
            _gridViewport = (RectTransform)_gridContent.parent;
            _gridLayout = _gridContent.gameObject.AddComponent<GridLayoutGroup>();
            _gridLayout.spacing = new Vector2(CARD_GAP, CARD_GAP);
            _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _gridLayout.padding = new RectOffset(0, 0, 0, 24);
            _gridContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        /// <summary>
        /// 重新取当前分类、全局难度下的曲目并按搜索词筛选
        /// </summary>
        /// <param name="isRestoring">打开时：翻到选中的歌所在的页</param>
        void RefreshList(bool isRestoring = false)
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
            if (isRestoring)
            {
                var hash = MajEnv.RuntimeConfig.List.SelectedSongHash;
                var index = string.IsNullOrEmpty(hash) ? -1 : _filteredSongs.FindIndex(x => x.Hash == hash);
                _page = index < 0 ? 0 : index / PageSize;
            }
            RenderSidebar();
            RenderPage();
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
                var item = CreateButton(_sidebarContent, string.Empty, isSelected ? ACCENT_SOFT_COLOR : PAPER_COLOR, TEXT_COLOR, 17f, () => SelectCollection(index));
                var itemRect = (RectTransform)item.transform;
                itemRect.sizeDelta = new Vector2(0f, 50f);
                var label = item.GetComponentInChildren<TextMeshProUGUI>();
                label.text = collection.Name;
                label.alignment = TextAlignmentOptions.Left;
                label.color = isSelected ? ACCENT_COLOR : TEXT_COLOR;
                label.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
                label.rectTransform.offsetMin = new Vector2(16f, 0f);
                label.rectTransform.offsetMax = new Vector2(-64f, 0f);
                var count = CreateText(itemRect, collection.Count.ToString(CultureInfo.InvariantCulture), 14f, isSelected ? ACCENT_COLOR : MUTED_COLOR, TextAlignmentOptions.Right, FontStyles.Normal);
                count.rectTransform.anchorMin = new Vector2(1f, 0f);
                count.rectTransform.anchorMax = new Vector2(1f, 1f);
                count.rectTransform.offsetMin = new Vector2(-60f, 0f);
                count.rectTransform.offsetMax = new Vector2(-14f, 0f);
            }
        }
        void RenderPage()
        {
            _pageCts.Cancel();
            _pageCts = new CancellationTokenSource();
            var token = _pageCts.Token;
            ClearChildren(_gridContent);

            var width = _gridViewport.rect.width;
            _lastGridWidth = width;
            var columns = Mathf.Clamp(Mathf.FloorToInt((width + CARD_GAP) / (CARD_MIN_WIDTH + CARD_GAP)), 1, MAX_COLUMNS);
            var cardWidth = Mathf.Max(200f, (width - CARD_GAP * (columns - 1)) / columns);
            _gridLayout.constraintCount = columns;
            _gridLayout.cellSize = new Vector2(cardWidth, CARD_HEIGHT);

            var pageCount = Math.Max(1, (_filteredSongs.Count + PageSize - 1) / PageSize);
            _page = Mathf.Clamp(_page, 0, pageCount - 1);
            _pageText.text = $"{_page + 1} / {pageCount}";
            _pageSizeText.text = "MAJTEXT_LIST_PAGE_SIZE".i18n(PageSize);
            _previousPageButton.interactable = _page > 0;
            _nextPageButton.interactable = _page < pageCount - 1;
            _captionText.text = $"{_collections.SelectedCollection.Name} · {"MAJTEXT_LIST_SONG_COUNT".i18n(_filteredSongs.Count)}";
            var level = GlobalLevel;
            _difficultyText.text = ScoreExchange.GetDifficultyName(level);
            _difficultyText.color = GetDifficultyColor(level);

            if (_filteredSongs.Count == 0)
            {
                _gridLayout.constraintCount = 1;
                _gridLayout.cellSize = new Vector2(width, 160f);
                var empty = CreatePanel(_gridContent, PAPER_COLOR, 12f);
                var emptyText = CreateText(empty.transform, "MAJTEXT_LIST_EMPTY".i18n(), 18f, MUTED_COLOR, TextAlignmentOptions.Center, FontStyles.Normal);
                Stretch(emptyText.rectTransform);
                return;
            }
            var start = _page * PageSize;
            var end = Math.Min(_filteredSongs.Count, start + PageSize);
            var selectedHash = MajEnv.RuntimeConfig.List.SelectedSongHash;
            for (var i = start; i < end; i++)
            {
                CreateCard(_filteredSongs[i], cardWidth, level, _filteredSongs[i].Hash == selectedHash, token);
            }
            _gridScroll.verticalNormalizedPosition = 1f;
        }
        void CreateCard(ISongDetail song, float cardWidth, ChartLevel level, bool isSelected, CancellationToken token)
        {
            var card = CreatePanel(_gridContent, isSelected ? ACCENT_COLOR : LINE_COLOR, 14f);
            var inner = CreatePanel(card.transform, PAPER_COLOR, 13f);
            Stretch(inner.rectTransform, 2f);
            // 点在卡片空白处时由它接收，再交给卡片上的 Button
            inner.raycastTarget = true;
            var cardButton = card.gameObject.AddComponent<Button>();
            cardButton.targetGraphic = inner;
            cardButton.onClick.AddListener(() => OpenDetail(song));
            var root = inner.rectTransform;

            var cover = CreateImage(root, LINE_COLOR, 10f);
            cover.preserveAspect = true;
            PlaceTopLeft(cover.rectTransform, 12f, 12f, 160f, 160f);
            LoadCoverAsync(song, cover, true, token).Forget();

            const float LEFT = 186f;
            var title = CreateText(root, song.Title, 22f, TEXT_COLOR, TextAlignmentOptions.Left, FontStyles.Bold);
            PlaceRow(title.rectTransform, LEFT, 12f, 56f, 32f);
            var favorite = CreateButton(root, SongStorage.IsInMyFavorites(song) ? "★" : "☆", PAPER_COLOR, FAVORITE_COLOR, 24f, null);
            PlaceTopRight((RectTransform)favorite.transform, 12f, 10f, 40f, 36f);
            favorite.onClick.AddListener(() => ToggleFavorite(song, favorite));

            var designer = GetDesigner(song, level);
            var credits = CreateText(root, $"{"MAJTEXT_LIST_ARTIST".i18n(song.Artist)}\n{"MAJTEXT_LIST_DESIGNER".i18n(designer)}", 15f, MUTED_COLOR, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            PlaceRow(credits.rectTransform, LEFT, 46f, 12f, 44f);

            var levels = CreateRect("Levels", root);
            PlaceRow(levels, LEFT, 96f, 12f, 30f);
            var levelLayout = levels.gameObject.AddComponent<HorizontalLayoutGroup>();
            levelLayout.spacing = 5f;
            levelLayout.childControlWidth = true;
            levelLayout.childControlHeight = true;
            levelLayout.childForceExpandWidth = true;
            levelLayout.childForceExpandHeight = true;
            var songLevels = song.Levels;
            for (var i = 0; i < songLevels.Length && i < LEVEL_SHORT_NAMES.Length; i++)
            {
                if (string.IsNullOrEmpty(songLevels[i]))
                {
                    continue;
                }
                var target = (ChartLevel)i;
                var isCurrent = target == level;
                CreateButton(levels, $"{LEVEL_SHORT_NAMES[i]} {songLevels[i]}", isCurrent ? ACCENT_SOFT_COLOR : PAGE_COLOR, isCurrent ? ACCENT_COLOR : MUTED_COLOR, 13f,
                             () => SetGlobalLevel(target));
            }

            var score = HasLevel(song, level) ? ScoreManager.GetScore(song, level) : null;
            CreateScoreLine(root, score, LEFT, 136f, 12f, 40f, 28f);

            var line = CreateImage(root, LINE_COLOR, 0f);
            PlaceRow(line.rectTransform, 12f, 186f, 12f, 1f);
            var footerText = score is not null && score.PlayCount > 0
                ? $"{"MAJTEXT_LIST_PLAY_COUNT".i18n(score.PlayCount)} · {"MAJTEXT_LIST_LAST_PLAYED".i18n(score.Timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}"
                : "MAJTEXT_SCORE_UNPLAYED".i18n();
            var footer = CreateText(root, footerText, 14f, MUTED_COLOR, TextAlignmentOptions.Left, FontStyles.Normal);
            PlaceRow(footer.rectTransform, 12f, 192f, 160f, 32f);
            var hash = CreateText(root, $"Hash {ShortHash(song.Hash)}", 14f, MUTED_COLOR, TextAlignmentOptions.Right, FontStyles.Normal);
            PlaceTopRight(hash.rectTransform, 12f, 192f, 150f, 32f);
        }
        /// <summary>
        /// 成绩行：评级、达成率、DX 分、FC/AP
        /// </summary>
        void CreateScoreLine(RectTransform parent, MaiScore? score, float left, float top, float right, float height, float scoreFontSize)
        {
            var row = CreateRect("ScoreLine", parent);
            PlaceRow(row, left, top, right, height);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            if (score is null || score.PlayCount <= 0)
            {
                AddFittedText(row, "—", scoreFontSize, MUTED_COLOR, FontStyles.Bold);
                return;
            }
            var achievement = score.Acc.DX;
            AddBadge(row, GetGrade(achievement), ACCENT_SOFT_COLOR, ACCENT_COLOR, scoreFontSize * 0.5f);
            AddFittedText(row, achievement.ToString("0.0000", CultureInfo.InvariantCulture) + "%", scoreFontSize, TEXT_COLOR, FontStyles.Bold);
            AddFittedText(row, $"DX {score.DXScore}", scoreFontSize * 0.5f, MUTED_COLOR, FontStyles.Normal);
            var combo = GetComboText(score.ComboState);
            if (!string.IsNullOrEmpty(combo))
            {
                AddBadge(row, combo, SPECIAL_SOFT_COLOR, SPECIAL_COLOR, scoreFontSize * 0.5f);
            }
        }
        void AddBadge(RectTransform parent, string text, Color fill, Color textColor, float fontSize)
        {
            var badge = CreatePanel(parent, fill, 6f);
            var label = CreateText(badge.transform, text, fontSize, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(label.rectTransform);
            var element = badge.gameObject.AddComponent<LayoutElement>();
            label.ForceMeshUpdate();
            element.preferredWidth = label.preferredWidth + 14f;
            element.preferredHeight = fontSize + 10f;
        }
        void AddFittedText(RectTransform parent, string text, float fontSize, Color color, FontStyles style)
        {
            // 宽度由所在的 HorizontalLayoutGroup 按文字的首选宽度决定
            var label = CreateText(parent, text, fontSize, color, TextAlignmentOptions.Left, style);
            label.overflowMode = TextOverflowModes.Overflow;
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
            var level = Mathf.Clamp((int)GlobalLevel + delta, 0, (int)ChartLevel.UTAGE);
            SetGlobalLevel((ChartLevel)level);
        }
        void SetGlobalLevel(ChartLevel level)
        {
            if (level == GlobalLevel)
            {
                return;
            }
            _collections.SetDifficulty(level);
            RefreshList();
        }
        void ChangePage(int delta)
        {
            _page += delta;
            RenderPage();
        }
        void CyclePageSize()
        {
            var firstIndex = _page * PageSize;
            _pageSizeIndex = (_pageSizeIndex + 1) % PAGE_SIZES.Length;
            _page = firstIndex / PageSize;
            RenderPage();
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
        void ToggleFavorite(ISongDetail song, Button button)
        {
            if (SongStorage.IsInMyFavorites(song))
            {
                SongStorage.RemoveFromMyFavorites(song);
            }
            else
            {
                SongStorage.AddToMyFavorites(song);
            }
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = SongStorage.IsInMyFavorites(song) ? "★" : "☆";
            }
            RenderSidebar();
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
            _pageCts.Cancel();
            _root.gameObject.SetActive(false);
            RestoreSelectBgm();
        }
        #endregion

        #region Detail view
        void BuildDetailView()
        {
            _detailView = CreateRect("DetailView", _root);
            Stretch(_detailView);
            _detailView.gameObject.AddComponent<Image>().color = PAGE_COLOR;
            _detailContent = CreateScroll(_detailView, 0f, 0f, 0f, 0f, out _detailScroll);
            _detailView.gameObject.SetActive(false);
        }
        void OpenDetail(ISongDetail song)
        {
            _detailSong = song;
            _detailLevel = HasLevel(song, GlobalLevel) ? GlobalLevel : GetHighestLevel(song);
            _listView.gameObject.SetActive(false);
            _detailView.gameObject.SetActive(true);
            _detailCts.Cancel();
            _detailCts = new CancellationTokenSource();
            _previewPlayer?.PlayPreviewSound(song, 0, _detailCts.Token, true);
            RenderDetail();
        }
        void CloseDetail()
        {
            _detailCts.Cancel();
            _detailSong = null;
            HideModal();
            _detailView.gameObject.SetActive(false);
            _listView.gameObject.SetActive(true);
            DestroyCreatedTextures();
            RestoreSelectBgm();
            // 收藏状态可能变了
            RefreshList();
        }
        void SetDetailLevel(ChartLevel level)
        {
            if (level == _detailLevel)
            {
                return;
            }
            _detailLevel = level;
            RenderDetail();
        }
        void RenderDetail()
        {
            var song = _detailSong;
            if (song is null)
            {
                return;
            }
            ClearChildren(_detailContent);
            DestroyCreatedTextures();
            var token = _detailCts.Token;
            var level = _detailLevel;
            _detailContent.sizeDelta = new Vector2(0f, DETAIL_HEIGHT);
            _detailScroll.verticalNormalizedPosition = 1f;

            var back = CreateButton(_detailContent, "<  " + "MAJTEXT_LIST_BACK".i18n(), PAGE_COLOR, TEXT_COLOR, 19f, CloseDetail);
            PlaceTopLeft((RectTransform)back.transform, 28f, 20f, 160f, 48f);

            // 曲目信息
            var hero = CreatePanel(_detailContent, PAPER_COLOR, 16f);
            PlaceRow(hero.rectTransform, 28f, 84f, 28f, 440f);
            var heroRoot = hero.rectTransform;
            var cover = CreateImage(heroRoot, LINE_COLOR, 12f);
            cover.preserveAspect = true;
            PlaceTopLeft(cover.rectTransform, 24f, 24f, 392f, 392f);
            LoadCoverAsync(song, cover, false, token).Forget();

            const float LEFT = 444f;
            var title = CreateText(heroRoot, song.Title, 36f, TEXT_COLOR, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            PlaceRow(title.rectTransform, LEFT, 24f, 24f, 50f);
            var credits = CreateText(heroRoot, $"{"MAJTEXT_LIST_ARTIST".i18n(song.Artist)}    {"MAJTEXT_LIST_DESIGNER".i18n(GetDesigner(song, level))}", 18f, MUTED_COLOR, TextAlignmentOptions.Left, FontStyles.Normal);
            PlaceRow(credits.rectTransform, LEFT, 80f, 24f, 30f);
            var meta = CreateText(heroRoot, $"Hash {ShortHash(song.Hash)}", 15f, MUTED_COLOR, TextAlignmentOptions.Left, FontStyles.Normal);
            PlaceRow(meta.rectTransform, LEFT, 116f, 24f, 26f);

            var levels = CreateRect("Levels", heroRoot);
            PlaceRow(levels, LEFT, 152f, 24f, 40f);
            var levelLayout = levels.gameObject.AddComponent<HorizontalLayoutGroup>();
            levelLayout.spacing = 8f;
            levelLayout.childControlWidth = true;
            levelLayout.childControlHeight = true;
            levelLayout.childForceExpandWidth = false;
            levelLayout.childForceExpandHeight = true;
            var songLevels = song.Levels;
            for (var i = 0; i < songLevels.Length && i < LEVEL_SHORT_NAMES.Length; i++)
            {
                if (string.IsNullOrEmpty(songLevels[i]))
                {
                    continue;
                }
                var target = (ChartLevel)i;
                var isCurrent = target == level;
                var levelButton = CreateButton(levels, $"{LEVEL_SHORT_NAMES[i]} {songLevels[i]}", isCurrent ? ACCENT_SOFT_COLOR : PAGE_COLOR, isCurrent ? ACCENT_COLOR : MUTED_COLOR, 16f,
                                               () => SetDetailLevel(target));
                levelButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 110f;
            }

            var bestLabel = CreateText(heroRoot, "MAJTEXT_LIST_BEST_SCORE".i18n(), 15f, MUTED_COLOR, TextAlignmentOptions.Left, FontStyles.Normal);
            PlaceRow(bestLabel.rectTransform, LEFT, 214f, 24f, 24f);
            var score = HasLevel(song, level) ? ScoreManager.GetScore(song, level) : null;
            CreateScoreLine(heroRoot, score, LEFT, 240f, 24f, 56f, 40f);
            var stats = score is not null && score.PlayCount > 0
                ? $"{"MAJTEXT_LIST_PLAY_COUNT".i18n(score.PlayCount)} · {"MAJTEXT_LIST_LAST_PLAYED".i18n(score.Timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}"
                : "MAJTEXT_SCORE_UNPLAYED".i18n();
            var statsText = CreateText(heroRoot, stats, 15f, MUTED_COLOR, TextAlignmentOptions.Left, FontStyles.Normal);
            PlaceRow(statsText.rectTransform, LEFT, 300f, 24f, 26f);

            // 操作按钮
            var actions = CreateRect("Actions", heroRoot);
            PlaceRow(actions, LEFT, 352f, 24f, 60f);
            var actionLayout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 10f;
            actionLayout.childAlignment = TextAnchor.MiddleLeft;
            actionLayout.childControlWidth = true;
            actionLayout.childControlHeight = true;
            actionLayout.childForceExpandWidth = false;
            actionLayout.childForceExpandHeight = true;
            Button? favoriteButton = null;
            favoriteButton = CreateButton(actions, SongStorage.IsInMyFavorites(song) ? "★" : "☆", PAPER_COLOR, FAVORITE_COLOR, 28f, () => ToggleFavorite(song, favoriteButton!));
            favoriteButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 64f;
            var shareButton = CreateButton(actions, "MAJTEXT_LIST_SHARE".i18n(), PAPER_COLOR, TEXT_COLOR, 18f, () => ShowShareModal(false));
            shareButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 150f;
            var exportButton = CreateButton(actions, "MAJTEXT_LIST_EXPORT".i18n(), PAPER_COLOR, TEXT_COLOR, 18f, () => ShowShareModal(true));
            exportButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 150f;
            var playButton = CreateButton(actions, "▶  " + "MAJTEXT_LIST_PLAY".i18n(), ACCENT_COLOR, Color.white, 20f, PlaySelected);
            playButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 180f;
            playButton.interactable = HasLevel(song, level);

            // 谱面雷达与物量分布
            var radarPanel = CreatePanel(_detailContent, PAPER_COLOR, 16f);
            radarPanel.rectTransform.anchorMin = new Vector2(0f, 1f);
            radarPanel.rectTransform.anchorMax = new Vector2(0.4f, 1f);
            radarPanel.rectTransform.pivot = new Vector2(0f, 1f);
            radarPanel.rectTransform.offsetMin = new Vector2(28f, -544f - 380f);
            radarPanel.rectTransform.offsetMax = new Vector2(-8f, -544f);
            var radarTitle = CreateText(radarPanel.transform, "MAJTEXT_LIST_RADAR".i18n(), 17f, TEXT_COLOR, TextAlignmentOptions.Left, FontStyles.Bold);
            PlaceRow(radarTitle.rectTransform, 20f, 16f, 20f, 28f);
            var constantText = CreateText(radarPanel.transform, string.Empty, 15f, MUTED_COLOR, TextAlignmentOptions.Right, FontStyles.Normal);
            PlaceRow(constantText.rectTransform, 20f, 16f, 20f, 28f);
            var radarRect = CreateRect("Radar", radarPanel.transform);
            radarRect.anchorMin = new Vector2(0.5f, 0.5f);
            radarRect.anchorMax = new Vector2(0.5f, 0.5f);
            radarRect.pivot = new Vector2(0.5f, 0.5f);
            radarRect.sizeDelta = new Vector2(230f, 230f);
            radarRect.anchoredPosition = new Vector2(0f, -18f);
            var radar = radarRect.gameObject.AddComponent<ListRadarGraphic>();
            radar.raycastTarget = false;

            var densityPanel = CreatePanel(_detailContent, PAPER_COLOR, 16f);
            densityPanel.rectTransform.anchorMin = new Vector2(0.4f, 1f);
            densityPanel.rectTransform.anchorMax = new Vector2(1f, 1f);
            densityPanel.rectTransform.pivot = new Vector2(0f, 1f);
            densityPanel.rectTransform.offsetMin = new Vector2(8f, -544f - 380f);
            densityPanel.rectTransform.offsetMax = new Vector2(-28f, -544f);
            var densityTitle = CreateText(densityPanel.transform, "MAJTEXT_LIST_DENSITY".i18n(), 17f, TEXT_COLOR, TextAlignmentOptions.Left, FontStyles.Bold);
            PlaceRow(densityTitle.rectTransform, 20f, 16f, 20f, 28f);
            var densityInfo = CreateText(densityPanel.transform, string.Empty, 15f, MUTED_COLOR, TextAlignmentOptions.Right, FontStyles.Normal);
            PlaceRow(densityInfo.rectTransform, 20f, 16f, 20f, 28f);
            var densityImage = CreateRect("Density", densityPanel.transform).gameObject.AddComponent<RawImage>();
            densityImage.raycastTarget = false;
            densityImage.color = new Color(1f, 1f, 1f, 0f);
            var densityRect = densityImage.rectTransform;
            densityRect.anchorMin = Vector2.zero;
            densityRect.anchorMax = Vector2.one;
            densityRect.offsetMin = new Vector2(20f, 20f);
            densityRect.offsetMax = new Vector2(-20f, -56f);

            if (HasLevel(song, level))
            {
                LoadChartAsync(song, level, radar, radarPanel.rectTransform, constantText, densityImage, densityInfo, token).Forget();
            }
            else
            {
                densityInfo.text = "MAJTEXT_LIST_NO_CHART".i18n();
            }
        }
        async UniTaskVoid LoadChartAsync(ISongDetail song, ChartLevel level, ListRadarGraphic radar, RectTransform radarPanel, TextMeshProUGUI constantText,
                                         RawImage densityImage, TextMeshProUGUI densityInfo, CancellationToken token)
        {
            try
            {
                densityInfo.text = "MAJTEXT_LIST_LOADING".i18n();
                var simaiFile = await song.GetMaidataAsync(false, token: token);
                await UniTask.SwitchToMainThread();
                if (token.IsCancellationRequested)
                {
                    return;
                }
                var chart = simaiFile.Charts[(int)level];
                var rect = densityImage.rectTransform.rect;
                var graphResult = ChartAnalyzer.AnalyzeMaidataWithGraph(chart, Math.Max(64, (int)rect.height), Math.Max(64, (int)rect.width));
                if (graphResult.LineGraph != null)
                {
                    _createdTextures.Add(graphResult.LineGraph);
                    densityImage.texture = graphResult.LineGraph;
                    densityImage.color = Color.white;
                    var length = graphResult.Length;
                    var bpm = Mathf.Approximately(graphResult.MinBPM, graphResult.MaxBPM)
                        ? graphResult.MaxBPM.ToString("0.#", CultureInfo.InvariantCulture)
                        : $"{graphResult.MinBPM.ToString("0.#", CultureInfo.InvariantCulture)}-{graphResult.MaxBPM.ToString("0.#", CultureInfo.InvariantCulture)}";
                    densityInfo.text = $"{"MAJTEXT_LIST_LENGTH".i18n($"{(int)length.TotalMinutes}:{length.Seconds:00}")}    BPM {bpm}";
                }
                else
                {
                    densityInfo.text = string.Empty;
                }

                var radarResult = await ChartRadarService.AnalyzeAsync(chart, token);
                await UniTask.SwitchToMainThread();
                if (token.IsCancellationRequested || radar == null || !radarResult.IsSuccess)
                {
                    return;
                }
                var dimensions = radarResult.DimensionOrder;
                var values = new float[dimensions.Count];
                for (var i = 0; i < dimensions.Count; i++)
                {
                    radarResult.Scores.TryGetValue(dimensions[i], out var value);
                    values[i] = Mathf.Clamp01((float)((value ?? 0d) / 250d));
                }
                radar.SetValues(values);
                var radius = radar.Radius;
                for (var i = 0; i < dimensions.Count; i++)
                {
                    var label = CreateText(radarPanel, GetDimensionName(dimensions[i]), 13f, MUTED_COLOR, TextAlignmentOptions.Center, FontStyles.Normal);
                    var labelRect = label.rectTransform;
                    labelRect.anchorMin = new Vector2(0.5f, 0.5f);
                    labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                    labelRect.pivot = new Vector2(0.5f, 0.5f);
                    labelRect.sizeDelta = new Vector2(120f, 22f);
                    labelRect.anchoredPosition = radar.rectTransform.anchoredPosition + radar.GetDirection(i) * (radius + 22f);
                }
                if (radarResult.FittedConstant is double constant)
                {
                    constantText.text = "MAJTEXT_LIST_CONSTANT".i18n(constant.ToString("0.0", CultureInfo.InvariantCulture));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                MajDebug.LogError(e);
                if (!token.IsCancellationRequested && densityInfo != null)
                {
                    densityInfo.text = string.Empty;
                }
            }
        }
        void PlaySelected()
        {
            var song = _detailSong;
            if (song is null || !HasLevel(song, _detailLevel))
            {
                return;
            }
            _detailCts.Cancel();
            _listManager.EnterGame(song, _detailLevel);
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
            ClearChildren(_modal);
            var formats = isExport
                ? new[] { ScoreShareFormatOption.Csv, ScoreShareFormatOption.Json }
                : new[] { ScoreShareFormatOption.Image, ScoreShareFormatOption.Text, ScoreShareFormatOption.Csv, ScoreShareFormatOption.Json };
            var height = 96f + formats.Length * 62f + 70f;
            var panel = CreatePanel(_modal, PAPER_COLOR, 16f);
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(480f, height);
            // 拦截面板上的点击，免得冒泡到背后的遮罩把弹窗关掉
            panel.raycastTarget = true;
            panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            var title = CreateText(panelRect, (isExport ? "MAJTEXT_LIST_CHOOSE_EXPORT" : "MAJTEXT_LIST_CHOOSE_SHARE").i18n(), 21f, TEXT_COLOR, TextAlignmentOptions.Left, FontStyles.Bold);
            PlaceRow(title.rectTransform, 26f, 24f, 26f, 36f);
            var level = _detailLevel;
            for (var i = 0; i < formats.Length; i++)
            {
                var format = formats[i];
                var option = CreateButton(panelRect, $"MAJSETTING_PROPERTY_ShareFormat_OPTION_{format}".i18n(), PAGE_COLOR, TEXT_COLOR, 18f, () =>
                {
                    HideModal();
                    _isWaitingShare = true;
                    ScoreExchange.StartShareChart(song, level, format);
                });
                PlaceRow((RectTransform)option.transform, 26f, 84f + i * 62f, 26f, 52f);
            }
            var cancel = CreateButton(panelRect, "MAJTEXT_LIST_CANCEL".i18n(), PAPER_COLOR, MUTED_COLOR, 17f, HideModal);
            PlaceRow((RectTransform)cancel.transform, 26f, height - 70f, 26f, 48f);
            _modal.gameObject.SetActive(true);
            _modal.SetAsLastSibling();
        }
        void HideModal()
        {
            _modal.gameObject.SetActive(false);
        }
        void BuildToast()
        {
            var toast = CreatePanel(_root, Hex(0x343F56), 10f);
            _toast = toast.rectTransform;
            _toast.anchorMin = new Vector2(1f, 0f);
            _toast.anchorMax = new Vector2(1f, 0f);
            _toast.pivot = new Vector2(1f, 0f);
            _toast.sizeDelta = new Vector2(420f, 56f);
            _toast.anchoredPosition = new Vector2(-28f, 28f);
            toast.raycastTarget = false;
            _toastText = CreateText(_toast, string.Empty, 17f, Color.white, TextAlignmentOptions.Center, FontStyles.Normal);
            Stretch(_toastText.rectTransform, 12f);
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

        #region Helpers
        void RestoreSelectBgm()
        {
            var selectSound = MajInstances.AudioManager.GetSFX("bgm_select.mp3");
            selectSound.SetVolume(MajEnv.Settings.Audio.Volume.BGM);
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
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.color = Color.white;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                MajDebug.LogWarning($"[SongListBrowser] Failed to load cover of {song.Title}: {e.Message}");
            }
        }
        void DestroyCreatedTextures()
        {
            foreach (var texture in _createdTextures)
            {
                if (texture != null)
                {
                    Destroy(texture);
                }
            }
            _createdTextures.Clear();
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
            for (var i = Math.Min(levels.Length, 7) - 1; i >= 0; i--)
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
            return index < designers.Length && !string.IsNullOrEmpty(designers[index]) ? designers[index] : "—";
        }
        static string ShortHash(string hash)
        {
            return hash.Length > 8 ? hash.Substring(0, 8) : hash;
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
                ChartLevel.Advance => Hex(0xFB9C2D),
                ChartLevel.Expert => Hex(0xF64861),
                ChartLevel.Master => Hex(0x9E45E2),
                ChartLevel.ReMaster => Hex(0xBA67F8),
                _ => Hex(0xE8379C),
            };
        }
        static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
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
        TextMeshProUGUI CreateText(Transform parent, string content, float fontSize, Color color, TextAlignmentOptions alignment, FontStyles style)
        {
            var rect = CreateRect("Text", parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null)
            {
                text.font = _font;
            }
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
            var background = CreatePanel(parent, fill, 9f);
            background.gameObject.name = "Button";
            background.raycastTarget = true;
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.96f, 0.97f, 1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.88f, 0.98f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            button.colors = colors;
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
            var background = CreatePanel(parent, PAPER_COLOR, 9f);
            background.gameObject.name = "SearchField";
            background.raycastTarget = true;
            var textArea = CreateRect("TextArea", background.transform);
            Stretch(textArea, 14f);
            textArea.gameObject.AddComponent<RectMask2D>();
            var placeholderText = CreateText(textArea, placeholder, 17f, MUTED_COLOR, TextAlignmentOptions.Left, FontStyles.Normal);
            Stretch(placeholderText.rectTransform);
            var text = CreateText(textArea, string.Empty, 17f, TEXT_COLOR, TextAlignmentOptions.Left, FontStyles.Normal);
            Stretch(text.rectTransform);
            var input = background.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = textArea;
            input.textComponent = text;
            input.placeholder = placeholderText;
            if (_font != null)
            {
                input.fontAsset = _font;
            }
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
