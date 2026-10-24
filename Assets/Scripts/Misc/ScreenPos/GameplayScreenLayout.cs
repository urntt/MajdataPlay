using Cysharp.Threading.Tasks;
using UnityEngine;
#nullable enable
namespace MajdataPlay
{
    /// <summary>
    /// 游戏场景的画面布局状态。只有 Game 场景会请求横屏，其它场景保持竖屏。
    /// </summary>
    public static class GameplayScreenLayout
    {
        /// <summary>
        /// 横屏下判定圆（直径 10.8 世界单位）占满的参考画布尺寸，与竖屏的 1080×1920 保持每世界单位 100 像素。
        /// </summary>
        public const float LANDSCAPE_REFERENCE_WIDTH = 1920f;
        public const float LANDSCAPE_REFERENCE_HEIGHT = 1080f;
        /// <summary>
        /// 判定圆外沿半径（与 InputManager.TouchButtonRingEdge 一致）
        /// </summary>
        public const float LANDSCAPE_HALF_HEIGHT = 5.4f;
        /// <summary>
        /// 下落式轨道区的宽度（世界单位）：16:9 时占屏宽 70%，两侧各留 15%
        /// </summary>
        public const float FALLING_LANE_AREA_WIDTH = LANDSCAPE_REFERENCE_WIDTH / 100f * 0.7f;
        /// <summary>
        /// 下落式最少可见的宽度：屏幕比 16:9 更窄时相机拉远，让轨道区最多占屏宽 85%
        /// </summary>
        public const float FALLING_MIN_VISIBLE_WIDTH = FALLING_LANE_AREA_WIDTH / 0.85f;

        static bool _isLandscapeRequested = false;
        static bool _isFallingPlayArea = false;

        public static bool IsLandscapeRequested => _isLandscapeRequested;
        /// <summary>
        /// 下落式：画面铺满屏幕，音符只在中间的轨道区下落；不显示副屏，忽略主屏缩放设置。
        /// </summary>
        public static bool IsFallingPlayArea => _isLandscapeRequested && _isFallingPlayArea;
        /// <summary>
        /// 已请求横屏，并且屏幕（或窗口）实际为横向。手机旋转完成前、或 PC 竖向窗口时为 false，回退到竖屏布局。
        /// 下落式始终使用横屏布局（竖向窗口时相机拉远，画面仍铺满）。
        /// </summary>
        public static bool IsLandscape => _isLandscapeRequested && (_isFallingPlayArea || Screen.width > Screen.height);

        /// <param name="isFallingPlayArea">下落式布局：画面铺满屏幕，轨道居中</param>
        public static void RequestLandscape(bool isFallingPlayArea = false)
        {
            _isFallingPlayArea = isFallingPlayArea;
            if (_isLandscapeRequested)
            {
                return;
            }
            _isLandscapeRequested = true;
#if UNITY_ANDROID || UNITY_IOS
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            RestoreAutoRotationAsync().Forget();
#endif
        }
        public static void Reset()
        {
            _isFallingPlayArea = false;
            if (!_isLandscapeRequested)
            {
                return;
            }
            _isLandscapeRequested = false;
#if UNITY_ANDROID || UNITY_IOS
            LockPortrait();
            Screen.orientation = ScreenOrientation.Portrait;
            RestoreAutoRotationAsync().Forget();
#endif
        }
        /// <summary>
        /// 播放器设置允许横屏（iOS 的 plist 需要包含横屏方向），因此启动时把自动旋转限制回竖屏。
        /// </summary>
        public static void LockPortrait()
        {
#if UNITY_ANDROID || UNITY_IOS
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = true;
#endif
        }
#if UNITY_ANDROID || UNITY_IOS
        static async UniTaskVoid RestoreAutoRotationAsync()
        {
            // 先强制到目标方向，等旋转生效后再交还给自动旋转（只在允许的方向之间切换）。
            var isLandscapeRequested = _isLandscapeRequested;
            await UniTask.DelayFrame(3);
            if (isLandscapeRequested != _isLandscapeRequested)
            {
                return;
            }
            Screen.orientation = ScreenOrientation.AutoRotation;
        }
#endif
    }
}
