#nullable enable
using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace MajdataPlay
{
    /// <summary>
    /// Lays out the category sidebar of a touch page: docked on the left of wide screens, and on narrow screens (phones in
    /// portrait) a drawer that slides in over the page from the left behind a dimming layer.
    /// </summary>
    /// <remarks>
    /// The page calls <see cref="Apply"/> with its width whenever its size changes, and toggles the drawer with
    /// <see cref="Toggle"/> from a menu button that it shows only while <see cref="IsCompact"/> is true.
    /// </remarks>
    internal sealed class SidebarDrawer
    {
        /// <summary>
        /// Pages narrower than this many canvas units use the drawer.
        /// </summary>
        public const float COMPACT_WIDTH = 1100f;

        /// <summary>
        /// The duration of the slide in and out, in seconds.
        /// </summary>
        const float SLIDE_SEC = 0.2f;

        /// <summary>
        /// The opacity of the dimming layer while the drawer is open.
        /// </summary>
        const float DIM_ALPHA = 0.35f;

        readonly RectTransform _sidebar;
        readonly RectTransform _main;
        readonly Image _dim;
        readonly float _mainLeftPadding;
        float _sidebarWidth;
        float _openProgress;
        MotionHandle _motion;

        /// <summary>
        /// Initializes a new instance of the <see cref="SidebarDrawer"/> class and moves the sidebar above the page content.
        /// </summary>
        /// <param name="page">The parent of the sidebar and the main area.</param>
        /// <param name="sidebar">The sidebar; its anchors are taken over.</param>
        /// <param name="main">The main area right of the docked sidebar; its left offset is taken over.</param>
        /// <param name="mainLeftPadding">The gap between the sidebar (or the left edge) and the main area.</param>
        public SidebarDrawer(RectTransform page, RectTransform sidebar, RectTransform main, float mainLeftPadding)
        {
            _sidebar = sidebar;
            _main = main;
            _mainLeftPadding = mainLeftPadding;
            _dim = LightUi.CreateImage(page, new Color(0.09f, 0.125f, 0.2f, 0f), 0f);
            _dim.gameObject.name = "SidebarDim";
            LightUi.Stretch(_dim.rectTransform);
            _dim.raycastTarget = true;
            var dimButton = _dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(Close);
            _dim.gameObject.SetActive(false);
            _dim.transform.SetAsLastSibling();
            _sidebar.SetAsLastSibling();
            _sidebar.anchorMin = new Vector2(0f, 0f);
            _sidebar.anchorMax = new Vector2(0f, 1f);
            _sidebar.pivot = new Vector2(0f, 0.5f);
        }

        /// <summary>
        /// Gets a value indicating whether the page is narrow and the sidebar is a drawer.
        /// </summary>
        public bool IsCompact { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the drawer is open or opening.
        /// </summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// Pages lower than this many canvas units (phones in landscape) keep the docked sidebar between 260 and 360 units wide.
        /// </summary>
        public const float SHORT_HEIGHT = 800f;

        /// <summary>
        /// Gets the width of the docked sidebar: a quarter of the page, kept between 260 and 360 units on short pages so phones in
        /// landscape leave enough room for the list.
        /// </summary>
        /// <param name="pageWidth">The width of the page.</param>
        /// <param name="pageHeight">The height of the page.</param>
        /// <returns>The sidebar width.</returns>
        public static float GetDockedWidth(float pageWidth, float pageHeight)
        {
            var width = pageWidth * 0.25f;
            return pageHeight < SHORT_HEIGHT ? Mathf.Clamp(width, 260f, 360f) : width;
        }

        /// <summary>
        /// Docks the sidebar or turns it into a drawer for the page size, keeping the drawer closed when it becomes one.
        /// </summary>
        /// <param name="pageWidth">The width of the page.</param>
        /// <param name="pageHeight">The height of the page.</param>
        public void Apply(float pageWidth, float pageHeight)
        {
            var isCompact = pageWidth < COMPACT_WIDTH;
            if (isCompact != IsCompact)
            {
                _motion.TryCancel();
                IsOpen = false;
                _openProgress = 0f;
            }
            IsCompact = isCompact;
            _sidebarWidth = isCompact ? Mathf.Min(360f, pageWidth * 0.85f) : GetDockedWidth(pageWidth, pageHeight);
            _sidebar.sizeDelta = new Vector2(_sidebarWidth, 0f);
            _main.offsetMin = new Vector2((isCompact ? 0f : _sidebarWidth) + _mainLeftPadding, _main.offsetMin.y);
            ApplyProgress();
        }

        /// <summary>
        /// Opens the drawer when it is closed and closes it when it is open; does nothing while the sidebar is docked.
        /// </summary>
        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        /// <summary>
        /// Slides the drawer in; does nothing while the sidebar is docked.
        /// </summary>
        public void Open()
        {
            if (!IsCompact || IsOpen)
            {
                return;
            }
            IsOpen = true;
            AnimateTo(1f);
        }

        /// <summary>
        /// Slides the drawer out; does nothing while the sidebar is docked.
        /// </summary>
        public void Close()
        {
            if (!IsCompact || !IsOpen)
            {
                return;
            }
            IsOpen = false;
            AnimateTo(0f);
        }

        /// <summary>
        /// Stops the slide animation, for example when the page is destroyed.
        /// </summary>
        public void Cancel()
        {
            _motion.TryCancel();
        }

        /// <summary>
        /// Animates the drawer to the given openness.
        /// </summary>
        /// <param name="target">1 for open, 0 for closed.</param>
        void AnimateTo(float target)
        {
            _motion.TryCancel();
            _motion = LMotion.Create(_openProgress, target, SLIDE_SEC)
                             .WithEase(Ease.OutCubic)
                             .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                             .Bind(progress =>
                             {
                                 _openProgress = progress;
                                 ApplyProgress();
                             });
        }

        /// <summary>
        /// Places the sidebar and the dimming layer for the current openness.
        /// </summary>
        void ApplyProgress()
        {
            if (!IsCompact)
            {
                _sidebar.gameObject.SetActive(true);
                _sidebar.anchoredPosition = Vector2.zero;
                _dim.gameObject.SetActive(false);
                return;
            }
            _sidebar.anchoredPosition = new Vector2(-_sidebarWidth * (1f - _openProgress), 0f);
            // 收起时整个隐藏，免得横屏时伸进左侧刘海的区域
            var isVisible = _openProgress > 0.001f;
            _sidebar.gameObject.SetActive(isVisible);
            _dim.gameObject.SetActive(isVisible);
            _dim.color = new Color(0.09f, 0.125f, 0.2f, DIM_ALPHA * _openProgress);
        }
    }
}
