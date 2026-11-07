#nullable enable
using LitMotion;
using TMPro;
using UnityEngine;

namespace MajdataPlay.Scenes.List
{
    internal sealed partial class SongListBrowser
    {
        /// <summary>
        /// The width of the Categories button that opens the sidebar drawer on narrow screens.
        /// </summary>
        const float MENU_BUTTON_WIDTH = 96f;

        /// <summary>
        /// The gap between the controls of a top bar.
        /// </summary>
        const float TOPBAR_GAP = 12f;

        /// <summary>
        /// The smallest scale of the top bar controls when they are shrunk to fit a narrow page.
        /// </summary>
        const float MIN_TOP_CONTROLS_SCALE = 0.6f;

        /// <summary>
        /// Detail pages narrower than this put the top bar controls on a second row below Back and the avatar.
        /// </summary>
        const float DETAIL_STACKED_TOP_WIDTH = 820f;

        /// <summary>
        /// The narrowest selection bar that still fits right of the pager; a narrower space stacks it above the pager.
        /// </summary>
        const float SELECTION_BAR_MIN_WIDTH = 300f;

        /// <summary>
        /// The narrowest search bar left of the pager; a narrower space makes the search bar cover the whole floating row.
        /// </summary>
        const float SEARCH_MIN_WIDTH = 260f;

        /// <summary>
        /// How the song cards are laid out.
        /// </summary>
        enum CardLayout
        {
            /// <summary>
            /// Cover on the left, text and difficulties in the middle, favorite and score on the right.
            /// </summary>
            Normal,

            /// <summary>
            /// Like <see cref="Normal"/> but lower, for pages lower than <see cref="SidebarDrawer.SHORT_HEIGHT"/> (phones in landscape).
            /// </summary>
            Short,

            /// <summary>
            /// Two rows for narrow pages: a small cover with the text and the score, then the difficulties across the card.
            /// </summary>
            Compact,
        }

        /// <summary>
        /// The positions and sizes of the parts of a song card, in canvas units from the top left of the card.
        /// </summary>
        sealed class CardMetrics
        {
            /// <summary>
            /// The layout for wide and tall pages.
            /// </summary>
            public static readonly CardMetrics Normal = new()
            {
                Height = CARD_HEIGHT,
                CoverSize = COVER_SIZE,
                CoverLeft = 15f,
                CoverTop = 15f,
                TextLeft = 15f + COVER_SIZE + 24f,
                TextRight = CARD_RIGHT_WIDTH + 28f,
                TitleTop = 12f,
                TitleHeight = 44f,
                TitleFontSize = 28f,
                CreditsTop = 56f,
                CreditsHeight = 29f,
                CreditsFontSize = 18f,
                InfoTop = 86f,
                InfoHeight = 27f,
                InfoFontSize = 17f,
                LevelsLeft = 15f + COVER_SIZE + 24f,
                LevelsTop = 120f,
                LevelsRight = CARD_RIGHT_WIDTH + 28f,
                LevelMinWidth = LEVEL_TAG_MIN_WIDTH,
                LevelFontSizeMin = 11f,
                HeartSize = 54f,
                HeartTop = 10f,
                ScoreTop = 70f,
                IsScoreInline = false,
            };

            /// <summary>
            /// The lower layout for short pages (phones in landscape).
            /// </summary>
            public static readonly CardMetrics Short = new()
            {
                Height = 176f,
                CoverSize = 146f,
                CoverLeft = 15f,
                CoverTop = 15f,
                TextLeft = 15f + 146f + 20f,
                TextRight = CARD_RIGHT_WIDTH + 28f,
                TitleTop = 8f,
                TitleHeight = 40f,
                TitleFontSize = 26f,
                CreditsTop = 48f,
                CreditsHeight = 26f,
                CreditsFontSize = 17f,
                InfoTop = 74f,
                InfoHeight = 24f,
                InfoFontSize = 16f,
                LevelsLeft = 15f + 146f + 20f,
                LevelsTop = 102f,
                LevelsRight = CARD_RIGHT_WIDTH + 28f,
                LevelMinWidth = LEVEL_TAG_MIN_WIDTH,
                LevelFontSizeMin = 11f,
                HeartSize = 44f,
                HeartTop = 4f,
                ScoreTop = 50f,
                IsScoreInline = false,
            };

            /// <summary>
            /// The two-row layout for narrow pages (phones in portrait).
            /// </summary>
            public static readonly CardMetrics Compact = new()
            {
                Height = 222f,
                CoverSize = 120f,
                CoverLeft = 12f,
                CoverTop = 12f,
                TextLeft = 12f + 120f + 16f,
                TextRight = 64f,
                TitleTop = 8f,
                TitleHeight = 36f,
                TitleFontSize = 24f,
                CreditsTop = 44f,
                CreditsHeight = 24f,
                CreditsFontSize = 15f,
                InfoTop = 68f,
                InfoHeight = 22f,
                InfoFontSize = 14f,
                LevelsLeft = 12f,
                LevelsTop = 144f,
                LevelsRight = 12f,
                LevelMinWidth = 72f,
                LevelFontSizeMin = 9f,
                HeartSize = 44f,
                HeartTop = 6f,
                ScoreTop = 94f,
                IsScoreInline = true,
            };

            /// <summary>
            /// Gets the height of the card.
            /// </summary>
            public required float Height { get; init; }

            /// <summary>
            /// Gets the side length of the square cover.
            /// </summary>
            public required float CoverSize { get; init; }

            /// <summary>
            /// Gets the distance of the cover from the left edge.
            /// </summary>
            public required float CoverLeft { get; init; }

            /// <summary>
            /// Gets the distance of the cover from the top edge.
            /// </summary>
            public required float CoverTop { get; init; }

            /// <summary>
            /// Gets the distance of the title, credits and info lines from the left edge.
            /// </summary>
            public required float TextLeft { get; init; }

            /// <summary>
            /// Gets the distance of the title, credits and info lines from the right edge.
            /// </summary>
            public required float TextRight { get; init; }

            /// <summary>
            /// Gets the top of the title.
            /// </summary>
            public required float TitleTop { get; init; }

            /// <summary>
            /// Gets the height of the title.
            /// </summary>
            public required float TitleHeight { get; init; }

            /// <summary>
            /// Gets the font size of the title.
            /// </summary>
            public required float TitleFontSize { get; init; }

            /// <summary>
            /// Gets the top of the artist and designer line.
            /// </summary>
            public required float CreditsTop { get; init; }

            /// <summary>
            /// Gets the height of the artist and designer line.
            /// </summary>
            public required float CreditsHeight { get; init; }

            /// <summary>
            /// Gets the font size of the artist and designer line.
            /// </summary>
            public required float CreditsFontSize { get; init; }

            /// <summary>
            /// Gets the top of the length, BPM and interaction line.
            /// </summary>
            public required float InfoTop { get; init; }

            /// <summary>
            /// Gets the height of the length, BPM and interaction line.
            /// </summary>
            public required float InfoHeight { get; init; }

            /// <summary>
            /// Gets the font size of the length, BPM and interaction line.
            /// </summary>
            public required float InfoFontSize { get; init; }

            /// <summary>
            /// Gets the distance of the difficulty row from the left edge.
            /// </summary>
            public required float LevelsLeft { get; init; }

            /// <summary>
            /// Gets the top of the difficulty row.
            /// </summary>
            public required float LevelsTop { get; init; }

            /// <summary>
            /// Gets the distance of the difficulty row from the right edge.
            /// </summary>
            public required float LevelsRight { get; init; }

            /// <summary>
            /// Gets the narrowest a difficulty column may become.
            /// </summary>
            public required float LevelMinWidth { get; init; }

            /// <summary>
            /// Gets the smallest font size of a difficulty tag.
            /// </summary>
            public required float LevelFontSizeMin { get; init; }

            /// <summary>
            /// Gets the size of the favorite button.
            /// </summary>
            public required float HeartSize { get; init; }

            /// <summary>
            /// Gets the top of the favorite button.
            /// </summary>
            public required float HeartTop { get; init; }

            /// <summary>
            /// Gets the top of the score: in the right column, or below the info line when <see cref="IsScoreInline"/> is set.
            /// </summary>
            public required float ScoreTop { get; init; }

            /// <summary>
            /// Gets a value indicating whether the score is a single line in the text column instead of a right column.
            /// </summary>
            public required bool IsScoreInline { get; init; }

            /// <summary>
            /// Gets the metrics of a card layout.
            /// </summary>
            /// <param name="layout">The card layout.</param>
            /// <returns>The metrics.</returns>
            public static CardMetrics For(CardLayout layout)
            {
                return layout switch
                {
                    CardLayout.Short => Short,
                    CardLayout.Compact => Compact,
                    _ => Normal,
                };
            }
        }

        /// <summary>
        /// The current layout of the song cards, chosen from the page size by <see cref="UpdateListLayout"/>.
        /// </summary>
        CardLayout _cardLayout = CardLayout.Normal;

        /// <summary>
        /// The size of the list page the layout was last applied for.
        /// </summary>
        Vector2 _lastListSize;

        /// <summary>
        /// Docks the category sidebar on wide pages and makes it a drawer on narrow ones.
        /// </summary>
        SidebarDrawer _drawer = null!;

        /// <summary>
        /// The Categories button at the left of the list top bar, shown while the sidebar is a drawer.
        /// </summary>
        RectTransform _menuButton = null!;

        /// <summary>
        /// The top bar of the list page.
        /// </summary>
        RectTransform _listTopRow = null!;

        /// <summary>
        /// The holder of Cover, −, the difficulty, + and Settings on the list page, scaled down on narrow pages.
        /// </summary>
        RectTransform _listTopControls = null!;

        /// <summary>
        /// The player nickname and avatar on the list page; only the avatar on narrow pages.
        /// </summary>
        RectTransform _listProfile = null!;

        /// <summary>
        /// Whether the selection bar is stacked above the pager because the space right of it is too narrow.
        /// </summary>
        bool _isSelectionBarStacked = false;

        /// <summary>
        /// Whether the expanded search bar covers the whole floating row because the space left of the pager is too narrow.
        /// </summary>
        bool _isSearchCovering = false;

        /// <summary>
        /// Gets the metrics of the current card layout.
        /// </summary>
        CardMetrics CurrentCardMetrics => CardMetrics.For(_cardLayout);

        /// <summary>
        /// Lays the list page out for its current size: docks the sidebar or turns it into a drawer, fits the top bar, the selection
        /// bar and the search bar, and rebuilds the cards when the card layout changes. Does nothing while the size is unchanged.
        /// </summary>
        void UpdateListLayout()
        {
            var size = _listView.rect.size;
            if (size.x <= 0f || (Mathf.Abs(size.x - _lastListSize.x) <= 1f && Mathf.Abs(size.y - _lastListSize.y) <= 1f))
            {
                return;
            }
            _lastListSize = size;
            _drawer.Apply(size.x, size.y);
            var isCompact = _drawer.IsCompact;
            _menuButton.gameObject.SetActive(isCompact);
            var mainWidth = ((RectTransform)_listTopRow.parent).rect.width;
            var controlsLeft = isCompact ? MENU_BUTTON_WIDTH + TOPBAR_GAP : 0f;
            var isProfileFull = mainWidth - controlsLeft - TOP_CONTROLS_WIDTH - TOPBAR_GAP >= PROFILE_WIDTH;
            var profileWidth = SetProfileNameVisible(_listProfile, isProfileFull);
            PlaceTopRight(_listProfile, 0f, 0f, profileWidth, TOPBAR_HEIGHT);
            FitTopControls(_listTopControls, controlsLeft, 0f, mainWidth - controlsLeft - profileWidth - TOPBAR_GAP);

            if (_selectionBar.gameObject.activeSelf)
            {
                FitSelectionBar();
            }
            if (_isSearchExpanded)
            {
                _searchMotion.TryCancel();
                GetSearchBarTarget(true, out var searchX, out var searchWidth, out var isCovering);
                SetSearchCovering(isCovering);
                _searchHolder.sizeDelta = new Vector2(searchWidth, PAGER_HEIGHT);
                _searchHolder.anchoredPosition = new Vector2(searchX, _searchHolder.anchoredPosition.y);
            }

            var cardLayout = isCompact ? CardLayout.Compact : size.y < SidebarDrawer.SHORT_HEIGHT ? CardLayout.Short : CardLayout.Normal;
            if (cardLayout != _cardLayout)
            {
                _cardLayout = cardLayout;
                if (_cards.Count > 0)
                {
                    RebuildCards();
                }
            }
        }

        /// <summary>
        /// Builds the cards of the current page again in the current card layout, keeping the scroll position.
        /// </summary>
        void RebuildCards()
        {
            var scrollY = _pendingScrollY ?? _listContent.anchoredPosition.y;
            RenderPage(false);
            RequestListScroll(scrollY);
        }

        /// <summary>
        /// Scales the top bar controls (Cover, −, the difficulty, + and Settings) down to fit the available width, centered in the bar.
        /// </summary>
        /// <param name="host">The holder of the controls, <see cref="TOP_CONTROLS_WIDTH"/> wide.</param>
        /// <param name="left">The distance of the controls from the left edge of the top bar's parent.</param>
        /// <param name="top">The distance of the top bar from the top edge of its parent.</param>
        /// <param name="available">The width the controls may take.</param>
        static void FitTopControls(RectTransform host, float left, float top, float available)
        {
            var scale = Mathf.Clamp(available / TOP_CONTROLS_WIDTH, MIN_TOP_CONTROLS_SCALE, 1f);
            host.localScale = new Vector3(scale, scale, 1f);
            PlaceTopLeft(host, left, top + TOPBAR_HEIGHT * (1f - scale) * 0.5f, TOP_CONTROLS_WIDTH, TOPBAR_HEIGHT);
        }

        /// <summary>
        /// Shows the player nickname beside the avatar, or only the avatar on narrow pages.
        /// </summary>
        /// <param name="profile">The profile created by <see cref="LightUi.CreatePlayerProfile"/>.</param>
        /// <param name="isVisible">Whether to show the nickname.</param>
        /// <returns>The width the profile needs: <see cref="PROFILE_WIDTH"/> with the nickname, the avatar size without it.</returns>
        static float SetProfileNameVisible(RectTransform profile, bool isVisible)
        {
            var nameText = profile.GetComponentInChildren<TextMeshProUGUI>(true);
            if (nameText != null)
            {
                nameText.gameObject.SetActive(isVisible);
            }
            return isVisible ? PROFILE_WIDTH : TOPBAR_HEIGHT;
        }

        /// <summary>
        /// Gets where the search bar goes: collapsed, it is a square left of the pager; expanded, it grows to the left, or covers the
        /// whole floating row when the space left of the pager is too narrow.
        /// </summary>
        /// <param name="isExpanded">Whether the search bar is expanded.</param>
        /// <param name="x">The position of the right edge of the search bar, relative to the center of the main area.</param>
        /// <param name="width">The width of the search bar.</param>
        /// <param name="isCovering">Whether the search bar covers the pager and the share button.</param>
        void GetSearchBarTarget(bool isExpanded, out float x, out float width, out bool isCovering)
        {
            var mainWidth = ((RectTransform)_searchHolder.parent).rect.width;
            var restX = PagerCenterX - PAGER_WIDTH * 0.5f - FLOAT_GAP;
            var leftSpace = mainWidth * 0.5f + restX;
            x = restX;
            width = PAGER_HEIGHT;
            isCovering = false;
            if (!isExpanded)
            {
                return;
            }
            if (leftSpace < SEARCH_MIN_WIDTH)
            {
                // 翻页左边太窄（竖屏手机）：搜索条盖住整排浮窗，收起后翻页与分享再出现
                isCovering = true;
                x = mainWidth * 0.5f - (SCROLLBAR_WIDTH + 14f);
                width = mainWidth - (SCROLLBAR_WIDTH + 14f);
                return;
            }
            width = Mathf.Clamp(leftSpace, PAGER_HEIGHT, SEARCH_EXPANDED_WIDTH);
        }

        /// <summary>
        /// Hides the pager, the share button and the selection bar while the search bar covers the floating row, and shows them
        /// again afterwards.
        /// </summary>
        /// <param name="isCovering">Whether the search bar covers the floating row.</param>
        void SetSearchCovering(bool isCovering)
        {
            _isSearchCovering = isCovering;
            _pager.gameObject.SetActive(!isCovering);
            _shareHolder.gameObject.SetActive(!isCovering && !_isSelecting);
            if (_isSelecting && !isCovering && !_selectionBar.gameObject.activeSelf)
            {
                FitSelectionBar();
            }
            _selectionBar.gameObject.SetActive(!isCovering && _isSelecting);
        }
    }
}
