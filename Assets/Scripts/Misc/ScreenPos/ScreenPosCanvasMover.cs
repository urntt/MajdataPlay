using MajdataPlay.Diagnostics;
using MajdataPlay.Editor;
using MajdataPlay.IO;
using MajdataPlay.Numerics;
using MajdataPlay.Settings;
using MajdataPlay.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#nullable enable
namespace MajdataPlay
{
    [DefaultExecutionOrder(150)]
    public class ScreenPosCanvasMover : MonoBehaviour
    {
        const int FLAG_NOT_INIT = 0;
        const int FLAG_INITED = 1;

        const float SCREEN_CANVAS_HEIGHT = 1920;
        const float SCREEN_CANVAS_WIDTH = 1080;

        const float MAIN_DISPLAY_POS_Y = 540;
        const float SUB_COVER_HEIGHT = 390;
        const float SUB_COVER_WIDTH = 1080;
        const float SUB_COVER_POS_Y = 1275;

        const float SUB_COVER_BOTTOM_HEIGHT = 0;
        const float SUB_COVER_BOTTOM_WIDTH = 1080;
        const float SUB_COVER_BOTTOM_POS_Y = 0;

        const float SUB_DISPLAY_ORIGINAL_POS_Y = 735f;
        const float SUB_DISPLAY_HEIGHT = 450f;
        const float MAIN_DISPLAY_HEIGHT = 1080f;
        // 横屏时副屏放在左侧栏，与屏幕边缘、主屏之间留出的间距（Canvas 单位）
        const float LANDSCAPE_SIDE_MARGIN = 24f;
        // 横屏时 EdgeCanvas 黑板伸到屏幕外的长度；原值 1000 万过大，换算位置时会损失精度
        const float LANDSCAPE_EDGE_PANEL_EXTENT = 20000f;
        const int EDGE_PANEL_UP = 0;
        const int EDGE_PANEL_DOWN = 1;
        const int EDGE_PANEL_LEFT = 2;
        const int EDGE_PANEL_RIGHT = 3;
        static readonly string[] EDGE_PANEL_NAMES = { "UpCanvas", "DownCanvas", "LeftCanvas", "RightCanvas" };

        int _flag = FLAG_NOT_INIT;

        Transform _transform;
        RectTransform _rt;

        // 主屏幕 Y 轴的"零偏移基准位置"（Canvas 坐标系），偏移量以此为基础叠加
        [SerializeField]
        [ReadOnlyField]
        float _basePosY;
        // 预制体自带的原始 Y 位置，关闭 MainScreenTransform 功能时用于完整还原
        [SerializeField]
        [ReadOnlyField]
        float _originalPosY;
        // 父节点的 RectTransform，用于在无 CanvasScaler 时读取父容器高度以计算屏幕中心
        [SerializeField]
        [ReadOnlyField]
        RectTransform? _parentRt;

        [SerializeField]
        [ReadOnlyField]
        float _screenHeight = 1920;
        [SerializeField]
        [ReadOnlyField]
        float _screenWidth = 1080;
        [SerializeField]
        [ReadOnlyField]
        float _canvasScaleFactor = 1f;


        // 脏检测（避免每帧重复计算）
        [SerializeField]
        [ReadOnlyField]
        float _lastMainDisplayOffset = float.NaN;       // 上一帧的主屏幕偏移量
        [SerializeField]
        [ReadOnlyField]
        float _lastMainDisplayScale = float.NaN;        // 上一帧的主屏幕缩放
        [SerializeField]
        [ReadOnlyField]
        float _lastSubDisplayOffset = float.NaN; // 上一帧的副屏幕偏移量
        [SerializeField]
        [ReadOnlyField]
        float _lastSubDisplayScale = float.NaN; // 上一帧的副屏幕偏移量
        [SerializeField]
        [ReadOnlyField]
        bool _lastTransformDisplay;          // 上一帧 MainScreenTransform 开关的状态

        // 屏幕中心缓存，缩放时需要以屏幕中心为锚点补偿位移
        [SerializeField]
        [ReadOnlyField]
        float _cachedScreenCenterY;
        [SerializeField]
        [ReadOnlyField]
        CanvasScaler? _canvasScaler;
        // 跨场景保存真正的原始分辨率
        static Vector2? _originalReferenceResolution;
        // 当前场景启动时记录的基准分辨率
        [SerializeField]
        [ReadOnlyField]
        Vector2 _baseReferenceResolution;

        // 副屏遮罩（Sub_Cover）
        [SerializeField]
        [ReadOnlyField]
        GameObject _subCover;
        [SerializeField]
        [ReadOnlyField]
        Transform _subCoverTransform;
        [SerializeField]
        [ReadOnlyField]
        RectTransform? _subCoverRectTransform;

        [SerializeField]
        [ReadOnlyField]
        GameObject _subCoverBottom;
        [SerializeField]
        [ReadOnlyField]
        Transform _subCoverBottomTransform;
        [SerializeField]
        [ReadOnlyField]
        RectTransform _subCoverBottomRectTransform;

        //副屏幕（Sub_Display）
        [SerializeField]
        [ReadOnlyField]
        RectTransform? _subDisplay;

        DisplayOptions? _displayOptions;

        private bool _isSubCoverPendingRefresh = false;

        // 横屏布局（GameplayScreenLayout）状态
        bool _isLandscape = false;
        int _lastLandscapeScreenWidth;
        int _lastLandscapeScreenHeight;
        float _lastLandscapeScale = float.NaN;
        bool _lastFallingPlayArea;
        Vector2 _originalMainAnchorMin;
        Vector2 _originalMainAnchorMax;
        float _originalSubDisplayPosX;

        // 常驻画布（GlobalCanvas）上才有：EdgeCanvas 遮住主屏两侧，Main_Display 上的 Image 是圆外全黑的遮罩
        GameObject? _edgeCanvas;
        bool _isEdgeCanvasActive;
        RectTransform? _edgeCanvasRt;
        Vector2 _originalEdgeCanvasPos;
        // EdgeCanvas 下的上、下、左、右黑板及其原始状态，退出横屏时还原
        readonly RectTransform?[] _edgePanels = new RectTransform?[4];
        readonly Vector2[] _originalEdgePanelPos = new Vector2[4];
        readonly Vector2[] _originalEdgePanelSize = new Vector2[4];
        readonly bool[] _isEdgePanelActive = new bool[4];
        Image? _circleMask;
        bool _isCircleMaskEnabled;


        void Awake()
        {
            _displayOptions = MajEnv.Settings?.Display;
            _rt = GetComponent<RectTransform>();
            _transform = transform;
            _parentRt = _transform.parent as RectTransform;
            // 保存预制体的原始Y位置，用于关闭MainScreenTransform时恢复
            _originalPosY = _rt.anchoredPosition.y;
            _originalMainAnchorMin = _rt.anchorMin;
            _originalMainAnchorMax = _rt.anchorMax;

            _parentRt = _transform.parent as RectTransform;
            // 先查找CanvasScaler，再计算屏幕中心
            var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (rootCanvas is not null)
            {
                _canvasScaler = rootCanvas.GetComponent<CanvasScaler>();
                if (_canvasScaler is not null)
                {
                    // 只在第一次记录真正的原始分辨率，避免场景切换时读到被修改过的值
                    if (_originalReferenceResolution is null)
                    {
                        _originalReferenceResolution = _canvasScaler.referenceResolution;
                    }
                    _baseReferenceResolution = (Vector2)_originalReferenceResolution;
                }
            }
            // 从Screen尺寸直接计算未缩放Canvas中心（Awake中rect可能尚未初始化）
            if (_canvasScaler is not null)
            {
                var baseScaleFactor = Mathf.Min(
                    Screen.width / _baseReferenceResolution.x,
                    Screen.height / _baseReferenceResolution.y
                );
                _cachedScreenCenterY = Screen.height / baseScaleFactor / 2f;
            }
            else if (_parentRt is not null && _parentRt.rect.height > 0)
            {
                _cachedScreenCenterY = _parentRt.rect.height / 2f;
            }
            else
            {
                _cachedScreenCenterY = _displayOptions?.MainScreenCachedScreenCenterY ?? 960f;
            }
            if (_displayOptions is not null)
            {
                _displayOptions.MainScreenCachedScreenCenterY = _cachedScreenCenterY;
            }

            _basePosY = 810f;
            _rt.anchoredPosition = new Vector2(0, _basePosY);

            var sub = _transform.parent.Find("Sub_Cover");
            if (sub is not null)
            {
                _subCover = sub.gameObject;
                _subCoverTransform = sub;
                _subCoverRectTransform = sub.GetComponent<RectTransform>();
            }
            var subBottom = _transform.parent.Find("Sub_Cover_Bottom");
            if (subBottom is not null)
            {
                _subCoverBottom = subBottom.gameObject;
                _subCoverBottomTransform = subBottom;
                _subCoverBottomRectTransform = subBottom.GetComponent<RectTransform>();
            }
            var subDisplay = _transform.parent.Find("Sub_Display");
            if (subDisplay is not null)
            {
                _subDisplay = subDisplay.GetComponent<RectTransform>();
                _originalSubDisplayPosX = _subDisplay.anchoredPosition.x;
            }
            var edgeCanvas = _transform.parent.Find("EdgeCanvas");
            if (edgeCanvas is not null)
            {
                _edgeCanvas = edgeCanvas.gameObject;
                _isEdgeCanvasActive = _edgeCanvas.activeSelf;
                _edgeCanvasRt = edgeCanvas as RectTransform;
                if (_edgeCanvasRt != null)
                {
                    _originalEdgeCanvasPos = _edgeCanvasRt.anchoredPosition;
                }
                for (var i = 0; i < EDGE_PANEL_NAMES.Length; i++)
                {
                    if (edgeCanvas.Find(EDGE_PANEL_NAMES[i]) is RectTransform panel)
                    {
                        _edgePanels[i] = panel;
                        _originalEdgePanelPos[i] = panel.anchoredPosition;
                        _originalEdgePanelSize[i] = panel.sizeDelta;
                        _isEdgePanelActive[i] = panel.gameObject.activeSelf;
                    }
                }
            }
            if (TryGetComponent<Image>(out var circleMask))
            {
                _circleMask = circleMask;
                _isCircleMaskEnabled = circleMask.enabled;
            }
            _canvasScaleFactor = Mathf.Max(
                Screen.width / _baseReferenceResolution.x,
                Screen.height / _baseReferenceResolution.y);
            var screenRect = _parentRt!.rect;
            MajDebug.LogDebug($"[ScreenPosCanvasMover]Canvas scale factor: {_canvasScaleFactor}");
            _screenHeight = screenRect.height / _canvasScaleFactor;
            _screenWidth = screenRect.width / _canvasScaleFactor;
        }

        void RestoreOriginal()
        {
            // 恢复到预制体的原始状态
            if (_canvasScaler != null)
            {
                _canvasScaler.referenceResolution = _baseReferenceResolution;
            }
            _rt.anchoredPosition = new Vector2(0, _originalPosY);
            _rt.localScale = Vector3.one;
            // 恢复Sub_Display位置
            if (_subDisplay != null)
            {
                _subDisplay.anchoredPosition = new Vector2(_subDisplay.anchoredPosition.x, SUB_DISPLAY_ORIGINAL_POS_Y);
                _subDisplay.localScale = Vector3.one;
            }
            //恢复Sub_Cover位置和大小
            if (_subCoverRectTransform != null)
            {
                _subCoverRectTransform.anchoredPosition = new Vector2(0, SUB_COVER_POS_Y);
                _subCoverRectTransform.sizeDelta = new Vector2(SUB_COVER_WIDTH, SUB_COVER_HEIGHT);
            }
            if (_subCoverBottomRectTransform != null)
            {
                _subCoverBottomRectTransform.anchoredPosition = new Vector2(0, SUB_COVER_BOTTOM_POS_Y);
                _subCoverBottomRectTransform.sizeDelta = new Vector2(SUB_COVER_BOTTOM_WIDTH, SUB_COVER_BOTTOM_HEIGHT);
            }
            _isSubCoverPendingRefresh = false;
        }

        void ApplyPosition(float offset, float scale, bool updateCache = false)
        {
            if (updateCache && _canvasScaler == null)
            {
                // localScale路径：parent rect高度不受我们代码影响，可以安全读取
                // CanvasScaler路径：屏幕中心已在Awake中从Screen尺寸计算，运行时不更新
                float parentHeight = _parentRt != null ? _parentRt.rect.height : 0f;
                if (parentHeight > 0)
                {
                    float realCenter = parentHeight / 2f;
                    if (realCenter != _cachedScreenCenterY)
                    {
                        _cachedScreenCenterY = realCenter;
                        _displayOptions!.MainScreenCachedScreenCenterY = realCenter;
                    }
                }
            }

            if (_canvasScaler != null && scale > 0)
            {
                // 通过缩小CanvasScaler的referenceResolution来实现放大效果
                var newRef = new Vector2(
                    _baseReferenceResolution.x / scale,
                    _baseReferenceResolution.y / scale
                );
                //_canvasScaleFactor = Mathf.Max(
                //    Screen.width / newRef.x,
                //    Screen.height / newRef.y);
                _canvasScaler.referenceResolution = newRef;
                float posY = _basePosY - (offset * 270f) + (_cachedScreenCenterY * ((1f / scale) - 1f));
                _rt.anchoredPosition = new Vector2(0, posY);
                _rt.localScale = Vector3.one;
            }
            else
            {
                // 回退到localScale方式（无CanvasScaler时）
                float posYBase = _basePosY - (offset * 270f);
                float scaleCorrection = (_cachedScreenCenterY - posYBase) * (1f - scale);
                _rt.anchoredPosition = new Vector2(0, posYBase + scaleCorrection);
                if (scale > 0)
                {
                    _rt.localScale = new Vector3(scale, scale, 1f);
                }
            }
        }

        void ApplySubDisplayTransform(float offset, float scale)
        {
            if (_subDisplay == null)
            {
                return;
            }
            float newY = SUB_DISPLAY_ORIGINAL_POS_Y + offset;
            _subDisplay.anchoredPosition = new Vector2(_subDisplay.anchoredPosition.x, newY);
            _subDisplay.localScale = new Vector3(scale, scale, 1f);
        }

        // 遮罩与主副屏均为同一父节点下的无旋转 UI，所有边界使用父节点局部坐标。
        static void SetCoverRect(RectTransform cover, float left, float right, float bottom, float top)
        {
            var scale = cover.localScale;
            if (Mathf.Approximately(scale.x, 0f) || Mathf.Approximately(scale.y, 0f))
            {
                return;
            }

            var width = Mathf.Max(0f, right - left);
            var height = Mathf.Max(0f, top - bottom);
            // sizeDelta 在拉伸锚点下不等于实际尺寸，同时需要抵消遮罩自身的缩放。
            cover.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width / Mathf.Abs(scale.x));
            cover.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height / Mathf.Abs(scale.y));

            var position = cover.localPosition;
            position.x = left + width * (scale.x > 0f ? cover.pivot.x : 1f - cover.pivot.x);
            position.y = bottom + height * (scale.y > 0f ? cover.pivot.y : 1f - cover.pivot.y);
            cover.localPosition = position;
        }

        void UpdateSubCover()
        {
            if (_parentRt == null)
            {
                return;
            }

            // RectTransform.rect 已经是 Canvas 单位，不应再除以 Canvas 的缩放系数。
            var parentRect = _parentRt.rect;
            // 横向边界对齐主屏幕（原始宽度 1080），并跟随 localScale 缩放。
            var mainX1 = _rt.localPosition.x + _rt.rect.xMin * _rt.localScale.x;
            var mainX2 = _rt.localPosition.x + _rt.rect.xMax * _rt.localScale.x;
            var mainDisplayLeft = Mathf.Min(mainX1, mainX2);
            var mainDisplayRight = Mathf.Max(mainX1, mainX2);
            var mainY1 = _rt.localPosition.y + _rt.rect.yMin * _rt.localScale.y;
            var mainY2 = _rt.localPosition.y + _rt.rect.yMax * _rt.localScale.y;
            var mainDisplayBottom = Mathf.Min(mainY1, mainY2);
            var mainDisplayTop = Mathf.Max(mainY1, mainY2);

            if (_subCoverRectTransform != null && _subDisplay != null)
            {
                var subDisplayBottom = _subDisplay.localPosition.y + Mathf.Min(
                    _subDisplay.rect.yMin * _subDisplay.localScale.y,
                    _subDisplay.rect.yMax * _subDisplay.localScale.y);
                SetCoverRect(_subCoverRectTransform, mainDisplayLeft, mainDisplayRight, mainDisplayTop, subDisplayBottom);
            }
            if (_subCoverBottomRectTransform != null)
            {
                SetCoverRect(_subCoverBottomRectTransform, mainDisplayLeft, mainDisplayRight, parentRect.yMin, mainDisplayBottom);
            }
        }

        float GetLandscapeScale()
        {
            if (GameplayScreenLayout.IsFallingPlayArea)
            {
                // 下落式的 16:9 游玩区始终铺满屏幕
                return 1f;
            }
            var scale = _displayOptions!.MainScreenTransform ? _displayOptions.MainScreenScale : 1f;
            return scale > 0 ? scale : 1f;
        }

        /// <summary>
        /// 横屏布局：主屏（判定圆）居中占满屏高，副屏按左侧栏宽度等比缩小放在左上，遮罩收起。
        /// </summary>
        void ApplyLandscape(float scale)
        {
            var referenceResolution = new Vector2(GameplayScreenLayout.LANDSCAPE_REFERENCE_WIDTH / scale,
                                                  GameplayScreenLayout.LANDSCAPE_REFERENCE_HEIGHT / scale);
            if (_canvasScaler != null)
            {
                _canvasScaler.referenceResolution = referenceResolution;
            }
            _rt.anchorMin = new Vector2(0.5f, 0.5f);
            _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.anchoredPosition = Vector2.zero;
            _rt.localScale = Vector3.one;

            // CanvasScaler Expand：取宽高比例中的较小值
            var scaleFactor = Mathf.Min(Screen.width / referenceResolution.x, Screen.height / referenceResolution.y);
            var canvasWidth = Screen.width / scaleFactor;
            var canvasHeight = Screen.height / scaleFactor;
            var sideWidth = Mathf.Max(0f, (canvasWidth - MAIN_DISPLAY_HEIGHT) / 2f);
            var subScale = Mathf.Clamp((sideWidth - LANDSCAPE_SIDE_MARGIN * 2f) / SCREEN_CANVAS_WIDTH, 0.05f, 1f);
            var subX = -(MAIN_DISPLAY_HEIGHT / 2f + sideWidth / 2f);
            var subY = canvasHeight / 2f - SUB_DISPLAY_HEIGHT * subScale / 2f - LANDSCAPE_SIDE_MARGIN;
            if (_subDisplay != null && GameplayScreenLayout.IsFallingPlayArea)
            {
                // 下落式由 HUD 显示信息，不显示副屏；缩放为 0 后副屏点击区域也随之消失
                _subDisplay.localScale = Vector3.zero;
            }
            else if (_subDisplay != null)
            {
                _subDisplay.anchoredPosition = new Vector2(subX, subY);
                _subDisplay.localScale = new Vector3(subScale, subScale, 1f);
            }
            // 遮罩只用于竖屏主副屏之间的区域，横屏下连同其中的调试文字一起缩为 0。
            if (_subCoverTransform != null)
            {
                _subCoverTransform.localScale = Vector3.zero;
            }
            if (_subCoverBottomTransform != null)
            {
                _subCoverBottomTransform.localScale = Vector3.zero;
            }
            // 下落式画面铺满屏幕，EdgeCanvas 和圆形遮罩都要关掉；
            // 其他页面的 EdgeCanvas 改为遮住主屏方框外的区域，只让出左上角的副屏
            if (_edgeCanvas != null)
            {
                if (GameplayScreenLayout.IsFallingPlayArea)
                {
                    _edgeCanvas.SetActive(false);
                }
                else
                {
                    _edgeCanvas.SetActive(true);
                    ApplyLandscapeEdgePanels(subY - SUB_DISPLAY_HEIGHT * subScale / 2f);
                }
            }
            if (_circleMask != null)
            {
                _circleMask.enabled = _isCircleMaskEnabled && !GameplayScreenLayout.IsFallingPlayArea;
            }
            _isSubCoverPendingRefresh = false;
        }

        /// <summary>
        /// 横屏时 EdgeCanvas 的四块黑板：右侧整列、方框上方、方框下方、左侧副屏底边以下，
        /// 遮住竖屏时伸出圆形主屏的半圆按键，与竖屏的观感一致
        /// </summary>
        /// <param name="subDisplayBottom">副屏底边，与主屏在同一父节点坐标系</param>
        void ApplyLandscapeEdgePanels(float subDisplayBottom)
        {
            if (_edgeCanvasRt == null)
            {
                return;
            }
            _edgeCanvasRt.anchoredPosition = Vector2.zero;
            var origin = (Vector2)_edgeCanvasRt.localPosition;
            var mainRect = _rt.rect;
            var mainPosition = (Vector2)_rt.localPosition;
            var left = mainPosition.x + mainRect.xMin;
            var right = mainPosition.x + mainRect.xMax;
            var bottom = mainPosition.y + mainRect.yMin;
            var top = mainPosition.y + mainRect.yMax;
            const float FAR = LANDSCAPE_EDGE_PANEL_EXTENT;
            SetEdgePanel(EDGE_PANEL_RIGHT, origin, right, FAR, -FAR, FAR);
            SetEdgePanel(EDGE_PANEL_UP, origin, left, right, top, FAR);
            SetEdgePanel(EDGE_PANEL_DOWN, origin, left, right, -FAR, bottom);
            SetEdgePanel(EDGE_PANEL_LEFT, origin, -FAR, left, -FAR, subDisplayBottom);
        }
        void SetEdgePanel(int index, Vector2 origin, float left, float right, float bottom, float top)
        {
            var panel = _edgePanels[index];
            if (panel == null)
            {
                return;
            }
            panel.gameObject.SetActive(true);
            panel.sizeDelta = new Vector2(Mathf.Max(0f, right - left), Mathf.Max(0f, top - bottom));
            panel.anchoredPosition = new Vector2((left + right) / 2f, (bottom + top) / 2f) - origin;
        }
        void RestoreEdgePanels()
        {
            if (_edgeCanvasRt != null)
            {
                _edgeCanvasRt.anchoredPosition = _originalEdgeCanvasPos;
            }
            for (var i = 0; i < _edgePanels.Length; i++)
            {
                var panel = _edgePanels[i];
                if (panel == null)
                {
                    continue;
                }
                panel.anchoredPosition = _originalEdgePanelPos[i];
                panel.sizeDelta = _originalEdgePanelSize[i];
                panel.gameObject.SetActive(_isEdgePanelActive[i]);
            }
        }

        /// <summary>
        /// 处理横屏布局的进入与退出。返回 true 表示当前为横屏，竖屏逻辑应跳过。
        /// </summary>
        bool UpdateLandscapeLayout()
        {
            if (GameplayScreenLayout.IsLandscape)
            {
                var width = Screen.width;
                var height = Screen.height;
                var scale = GetLandscapeScale();
                var isFallingPlayArea = GameplayScreenLayout.IsFallingPlayArea;
                if (!_isLandscape || width != _lastLandscapeScreenWidth || height != _lastLandscapeScreenHeight || scale != _lastLandscapeScale || isFallingPlayArea != _lastFallingPlayArea)
                {
                    _isLandscape = true;
                    _lastLandscapeScreenWidth = width;
                    _lastLandscapeScreenHeight = height;
                    _lastLandscapeScale = scale;
                    _lastFallingPlayArea = isFallingPlayArea;
                    ApplyLandscape(scale);
                }
                return true;
            }
            else if (_isLandscape)
            {
                _isLandscape = false;
                _lastLandscapeScale = float.NaN;
                _rt.anchorMin = _originalMainAnchorMin;
                _rt.anchorMax = _originalMainAnchorMax;
                if (_subDisplay != null)
                {
                    _subDisplay.anchoredPosition = new Vector2(_originalSubDisplayPosX, _subDisplay.anchoredPosition.y);
                }
                if (_subCoverTransform != null)
                {
                    _subCoverTransform.localScale = Vector3.one;
                }
                if (_subCoverBottomTransform != null)
                {
                    _subCoverBottomTransform.localScale = Vector3.one;
                }
                if (_edgeCanvas != null)
                {
                    RestoreEdgePanels();
                    _edgeCanvas.SetActive(_isEdgeCanvasActive);
                }
                if (_circleMask != null)
                {
                    _circleMask.enabled = _isCircleMaskEnabled;
                }
                _lastMainDisplayOffset = float.NaN;
                _lastMainDisplayScale = float.NaN;
                _lastSubDisplayOffset = float.NaN;
                _lastSubDisplayScale = float.NaN;
                ApplyTransform();
                _isSubCoverPendingRefresh = _lastTransformDisplay;
            }
            return false;
        }

        void ApplyTransform()
        {
            var initTransform = _displayOptions!.MainScreenTransform;
            _lastTransformDisplay = initTransform;
            if (initTransform)
            {
                var offset = _displayOptions!.MainScreenOffset;
                var scale = _displayOptions!.MainScreenScale;
                _lastMainDisplayOffset = offset;
                _lastMainDisplayScale = scale;
                ApplyPosition(offset, scale, true);

                var subOffset = _displayOptions.SubDisplayOffset;
                var subScale = _displayOptions.SubDisplayScale;
                _lastSubDisplayOffset = subOffset;
                _lastSubDisplayScale = subScale;
                ApplySubDisplayTransform(subOffset * 100f, subScale);

                UpdateSubCover();
            }
            else
            {
                RestoreOriginal();
            }
        }
        private void Update()
        {
            if (_isSubCoverPendingRefresh)
            {
                UpdateSubCover();
                _isSubCoverPendingRefresh = false;
            }
        }

        private void LateUpdate()
        {
            switch (_flag)
            {
                case FLAG_NOT_INIT:
                    {
                        _displayOptions = MajEnv.Settings?.Display;
                        if (_displayOptions is null)
                        {
                            return;
                        }
                        ApplyTransform();
                        _isSubCoverPendingRefresh = true;
                        _flag = FLAG_INITED;
                    }
                    return;
                case FLAG_INITED:
                    {
                        if (UpdateLandscapeLayout())
                        {
                            return;
                        }
                        var transformDisplay = _displayOptions!.MainScreenTransform;

                        if (!transformDisplay)
                        {
                            if (_lastTransformDisplay)
                            {
                                _lastTransformDisplay = false;
                                _lastMainDisplayOffset = float.NaN;
                                _lastMainDisplayScale = float.NaN;
                                _lastSubDisplayOffset = float.NaN;
                                _lastSubDisplayScale = float.NaN;
                                RestoreOriginal();
                            }
                            return;
                        }
                        _lastTransformDisplay = true;

                        var subDisplayOffset = _displayOptions.SubDisplayOffset;
                        var subDisplayScale = _displayOptions.SubDisplayScale;
                        var screenOffset = _displayOptions.MainScreenOffset;
                        var screenScale = _displayOptions.MainScreenScale;

                        var subChanged = subDisplayOffset != _lastSubDisplayOffset || subDisplayScale != _lastSubDisplayScale;
                        var mainChanged = screenOffset != _lastMainDisplayOffset || screenScale != _lastMainDisplayScale;

                        if (!subChanged && !mainChanged)
                        {
                            return;
                        }
                        // 仅主副屏变换依赖脏检测，遮罩需要每帧跟随实际布局刷新。
                        if (subChanged)
                        {
                            _lastSubDisplayOffset = subDisplayOffset;
                            _lastSubDisplayScale = subDisplayScale;
                            ApplySubDisplayTransform(subDisplayOffset * 100f, subDisplayScale);
                        }

                        if (mainChanged)
                        {
                            _lastMainDisplayOffset = screenOffset;
                            _lastMainDisplayScale = screenScale;
                            ApplyPosition(screenOffset, screenScale, updateCache: true);
                        }
                        _isSubCoverPendingRefresh = true;
                    }
                    return;
            }
        }
    }
}
