#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace MajdataPlay
{
    /// <summary>
    /// 横屏布局下常驻画布（GlobalCanvas）上的 EdgeCanvas 与圆形遮罩。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 横屏：EdgeCanvas 的四块黑板遮住主屏方框外的区域（右侧整列、方框上方、方框下方、左侧副屏底边以下），
    /// 与竖屏时只露出圆形主屏和副屏的观感一致。
    /// </para>
    /// <para>
    /// 下落式：画面铺满屏幕，平时关掉 EdgeCanvas 和圆形遮罩；转场动画只在中间的圆里，转场期间按进度渐隐渐显遮住四周。
    /// 黑板和遮罩平时使用不透明材质，渐变期间换成默认的透明 UI 材质。
    /// </para>
    /// <para>回到竖屏时全部还原。</para>
    /// </remarks>
    internal sealed class LandscapeEdgeMask
    {
        private const string EdgeCanvasName = "EdgeCanvas";
        private const string CircleMaskName = "Mask";
        private const int EdgePanelUp = 0;
        private const int EdgePanelDown = 1;
        private const int EdgePanelLeft = 2;
        private const int EdgePanelRight = 3;
        private static readonly string[] EdgePanelNames = { "UpCanvas", "DownCanvas", "LeftCanvas", "RightCanvas" };

        private readonly GameObject _edgeCanvas;
        private readonly RectTransform _edgeCanvasRect;
        private readonly bool _isEdgeCanvasActive;
        private readonly Vector2 _originalEdgeCanvasPosition;
        private readonly RectTransform?[] _edgePanels = new RectTransform?[4];
        private readonly Image?[] _edgePanelImages = new Image?[4];
        private readonly Vector2[] _originalPanelPositions = new Vector2[4];
        private readonly Vector2[] _originalPanelSizes = new Vector2[4];
        private readonly bool[] _isPanelActive = new bool[4];
        private readonly Material?[] _originalPanelMaterials = new Material?[4];

        private readonly Image? _circleMask;
        private readonly bool _isCircleMaskEnabled;
        private readonly Color _originalCircleMaskColor;
        private readonly Material? _originalCircleMaskMaterial;

        private CanvasGroup? _edgeCanvasGroup;
        private ScreenPosLayoutMode _mode = ScreenPosLayoutMode.Portrait;
        private float _lastTransitionProgress = float.NaN;
        private bool _isTransparent;

        private LandscapeEdgeMask(GameObject edgeCanvas, RectTransform edgeCanvasRect, Image? circleMask)
        {
            _edgeCanvas = edgeCanvas;
            _edgeCanvasRect = edgeCanvasRect;
            _isEdgeCanvasActive = edgeCanvas.activeSelf;
            _originalEdgeCanvasPosition = edgeCanvasRect.anchoredPosition;
            for (var i = 0; i < EdgePanelNames.Length; i++)
            {
                if (edgeCanvasRect.Find(EdgePanelNames[i]) is RectTransform panel)
                {
                    _edgePanels[i] = panel;
                    _originalPanelPositions[i] = panel.anchoredPosition;
                    _originalPanelSizes[i] = panel.sizeDelta;
                    _isPanelActive[i] = panel.gameObject.activeSelf;
                    if (panel.TryGetComponent<Image>(out var image))
                    {
                        _edgePanelImages[i] = image;
                        _originalPanelMaterials[i] = image.material == image.defaultMaterial ? null : image.material;
                    }
                }
            }
            _circleMask = circleMask;
            if (circleMask != null)
            {
                _isCircleMaskEnabled = circleMask.enabled;
                _originalCircleMaskColor = circleMask.color;
                _originalCircleMaskMaterial = circleMask.material == circleMask.defaultMaterial ? null : circleMask.material;
            }
        }

        /// <summary>
        /// 只有带 EdgeCanvas 的画布（常驻的 GlobalCanvas）才需要处理，其余画布返回 null
        /// </summary>
        /// <param name="root">画布根节点</param>
        /// <param name="mainDisplay">主屏，圆形遮罩是它的子节点 Mask 或它自身的 Image</param>
        public static LandscapeEdgeMask? TryCreate(Transform root, RectTransform? mainDisplay)
        {
            if (root.Find(EdgeCanvasName) is not RectTransform edgeCanvas)
            {
                return null;
            }
            Image? circleMask = null;
            if (mainDisplay != null)
            {
                var maskTransform = mainDisplay.Find(CircleMaskName);
                if (maskTransform == null || !maskTransform.TryGetComponent(out circleMask))
                {
                    mainDisplay.TryGetComponent(out circleMask);
                }
            }
            return new LandscapeEdgeMask(edgeCanvas.gameObject, edgeCanvas, circleMask);
        }

        /// <summary>
        /// 按新的布局摆放黑板、切换遮罩
        /// </summary>
        public void Apply(in ScreenPosCanvasLayout layout)
        {
            var previousMode = _mode;
            _mode = layout.Mode;
            switch (layout.Mode)
            {
                case ScreenPosLayoutMode.Portrait:
                    if (previousMode != ScreenPosLayoutMode.Portrait)
                    {
                        Restore();
                    }
                    return;
                case ScreenPosLayoutMode.Landscape:
                    SetTransparent(false);
                    PlacePanels(layout);
                    _edgeCanvas.SetActive(true);
                    if (_circleMask != null)
                    {
                        _circleMask.enabled = _isCircleMaskEnabled;
                    }
                    return;
                case ScreenPosLayoutMode.FallingPlayArea:
                    PlacePanels(layout);
                    SetTransparent(true);
                    UpdateFallingTransition(true);
                    return;
            }
        }

        /// <summary>
        /// 下落式：按转场进度显示圆形遮罩与 EdgeCanvas。合上时四周逐渐变黑，打开时逐渐显出，完全打开后全部关掉
        /// </summary>
        /// <param name="force">进度没变时也刷新</param>
        public void UpdateFallingTransition(bool force)
        {
            var progress = SceneSwitcher.TransitionProgress;
            if (!force && progress == _lastTransitionProgress)
            {
                return;
            }
            _lastTransitionProgress = progress;
            var isVisible = progress > 0f;
            if (_circleMask != null)
            {
                _circleMask.enabled = _isCircleMaskEnabled && isVisible;
                var color = _originalCircleMaskColor;
                color.a *= progress;
                _circleMask.color = color;
            }
            _edgeCanvas.SetActive(isVisible);
            if (isVisible)
            {
                if (_edgeCanvasGroup == null)
                {
                    _edgeCanvasGroup = _edgeCanvas.AddComponent<CanvasGroup>();
                    _edgeCanvasGroup.interactable = false;
                    _edgeCanvasGroup.blocksRaycasts = false;
                }
                _edgeCanvasGroup.alpha = progress;
            }
        }

        /// <summary>
        /// 把布局里的四块黑板区域换算到 EdgeCanvas 的局部坐标
        /// </summary>
        private void PlacePanels(in ScreenPosCanvasLayout layout)
        {
            // EdgeCanvas 的轴心放在画布中心；布局坐标以画布底边中点为原点
            _edgeCanvasRect.anchoredPosition = ScreenPosCanvasLayout.ToAnchoredPosition(
                new Vector2(0f, layout.CanvasSize.y / 2f),
                _edgeCanvasRect.anchorMin,
                _edgeCanvasRect.anchorMax,
                _edgeCanvasRect.pivot,
                layout.CanvasSize);
            var origin = new Vector2(0f, layout.CanvasSize.y / 2f);
            PlacePanel(EdgePanelUp, layout.EdgeUp, origin);
            PlacePanel(EdgePanelDown, layout.EdgeDown, origin);
            PlacePanel(EdgePanelLeft, layout.EdgeLeft, origin);
            PlacePanel(EdgePanelRight, layout.EdgeRight, origin);
        }

        private void PlacePanel(int index, Rect area, Vector2 origin)
        {
            var panel = _edgePanels[index];
            if (panel == null)
            {
                return;
            }
            panel.gameObject.SetActive(true);
            var parentRect = _edgeCanvasRect.rect;
            var anchorReference = new Vector2(
                parentRect.xMin + (Mathf.Lerp(panel.anchorMin.x, panel.anchorMax.x, panel.pivot.x) * parentRect.width),
                parentRect.yMin + (Mathf.Lerp(panel.anchorMin.y, panel.anchorMax.y, panel.pivot.y) * parentRect.height));
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, area.width);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, area.height);
            var pivotPosition = new Vector2(area.xMin + (area.width * panel.pivot.x),
                                            area.yMin + (area.height * panel.pivot.y)) - origin;
            panel.anchoredPosition = pivotPosition - anchorReference;
        }

        /// <summary>
        /// 渐变期间换成透明 UI 材质（不透明材质会忽略透明度），其余时候用原本的材质
        /// </summary>
        private void SetTransparent(bool transparent)
        {
            if (transparent == _isTransparent)
            {
                return;
            }
            _isTransparent = transparent;
            for (var i = 0; i < _edgePanelImages.Length; i++)
            {
                var image = _edgePanelImages[i];
                if (image != null)
                {
                    image.material = transparent ? null : _originalPanelMaterials[i];
                }
            }
            if (_circleMask != null)
            {
                _circleMask.material = transparent ? null : _originalCircleMaskMaterial;
            }
        }

        /// <summary>
        /// 还原到竖屏时的状态
        /// </summary>
        private void Restore()
        {
            SetTransparent(false);
            _lastTransitionProgress = float.NaN;
            if (_edgeCanvasGroup != null)
            {
                _edgeCanvasGroup.alpha = 1f;
            }
            _edgeCanvasRect.anchoredPosition = _originalEdgeCanvasPosition;
            for (var i = 0; i < _edgePanels.Length; i++)
            {
                var panel = _edgePanels[i];
                if (panel == null)
                {
                    continue;
                }
                panel.anchoredPosition = _originalPanelPositions[i];
                panel.sizeDelta = _originalPanelSizes[i];
                panel.gameObject.SetActive(_isPanelActive[i]);
            }
            _edgeCanvas.SetActive(_isEdgeCanvasActive);
            if (_circleMask != null)
            {
                _circleMask.color = _originalCircleMaskColor;
                _circleMask.enabled = _isCircleMaskEnabled;
            }
        }
    }
}
