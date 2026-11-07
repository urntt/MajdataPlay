using MajdataPlay.IO;
using MajdataPlay.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;
#nullable enable
namespace MajdataPlay.Scenes.Game.Lane
{
    /// <summary>
    /// 下落式模式的开关状态。由 GamePlayManager 在进入游戏时设置，退出时复位。
    /// </summary>
    internal static class LaneMode
    {
        public static bool IsEnabled { get; private set; } = false;
        /// <summary>
        /// 开启时 Tap/Hold/Break/Flick/Slide 在正确的列上任意高度都算；关闭时需按在判定线附近
        /// </summary>
        public static bool IsVerticalJudge { get; private set; } = true;
        public static bool IsBreakToFlick { get; private set; } = false;
        public static bool IsLaneDividerVisible { get; private set; } = true;
        /// <summary>
        /// 扫键：按住的手指划进某一列也算点击这一列
        /// </summary>
        public static bool IsSweepEnabled { get; private set; } = false;
        /// <summary>
        /// 扫键时保护正在打星星的手指（同一列最多一根）；关闭时所有手指都扫键
        /// </summary>
        public static bool IsSweepSlideGuardEnabled { get; private set; } = true;
        /// <summary>
        /// Slide 样式：标准、下落式或悬空式
        /// </summary>
        public static FallingSlideStyleOption SlideStyle { get; private set; } = FallingSlideStyleOption.Falling;
        /// <summary>
        /// Slide 样式为标准：保持标准游玩的样式与判定，不转成下落的箭头，按标准圆形传感器判定
        /// </summary>
        public static bool IsSlideOriginal => IsEnabled && SlideStyle == FallingSlideStyleOption.Standard;
        /// <summary>
        /// Slide 样式为悬空式：箭头落到判定线上方的第二条判定线，只认那条线以上的手指
        /// </summary>
        public static bool IsSlideFloating => IsEnabled && SlideStyle == FallingSlideStyleOption.Floating;
        /// <summary>
        /// 悬空 Touch 在所在列的任意高度都能按（与 <see cref="IsVerticalJudge"/> 互不影响，地雷 Touch 不受影响）
        /// </summary>
        public static bool IsTouchVerticalJudge { get; private set; } = false;
        /// <summary>
        /// 星星尾转 Flick 的位置（起点或终点）；Slide 样式为标准时为关
        /// </summary>
        public static FallingSlideFlickOption SlideTailFlick { get; private set; } = FallingSlideFlickOption.Off;
        /// <summary>
        /// 星星尾转 Flick：整条箭头换成一个 Flick
        /// </summary>
        public static bool IsSlideTailToFlick => SlideTailFlick != FallingSlideFlickOption.Off;
        /// <summary>
        /// 点亮手指所在列（只在显示轨道分割线时生效）
        /// </summary>
        public static bool IsLaneHighlightEnabled { get; private set; } = false;

        public static void Enable(GameSetting settings)
        {
            var falling = settings.Falling;
            var mod = settings.Mod;
            IsEnabled = true;
            IsVerticalJudge = falling.FallingVerticalJudge;
            IsBreakToFlick = mod.FallingBreakToFlick;
            IsLaneDividerVisible = falling.FallingLaneDividers;
            IsSweepEnabled = falling.FallingSweep;
            IsSweepSlideGuardEnabled = falling.FallingSweepSlideGuard;
            SlideStyle = falling.FallingSlideStyle;
            IsTouchVerticalJudge = falling.FallingTouchVerticalJudge;
            SlideTailFlick = falling.FallingSlideStyle == FallingSlideStyleOption.Standard ? FallingSlideFlickOption.Off : mod.FallingSlideTailToFlick;
            IsLaneHighlightEnabled = falling.FallingLaneHighlight && falling.FallingLaneDividers;
            // 宽屏与带刘海的手机加宽轨道：进入游戏时按屏幕比例和安全区算定，整局不变
            LaneLayout.ConfigureForScreen();
        }
        public static void Disable()
        {
            IsEnabled = false;
            IsVerticalJudge = true;
            IsBreakToFlick = false;
            IsLaneDividerVisible = true;
            IsSweepEnabled = false;
            IsSweepSlideGuardEnabled = true;
            SlideStyle = FallingSlideStyleOption.Falling;
            IsTouchVerticalJudge = false;
            SlideTailFlick = FallingSlideFlickOption.Off;
            IsLaneHighlightEnabled = false;
            LaneLayout.ResetWidth();
        }
    }
    /// <summary>
    /// 下落式模式的轨道几何。可见高度为 10.8 世界单位，画面铺满屏幕；
    /// 8 条轨道居中，宽度为安全区内可见宽度的 85%（4:3 时与最窄宽度相同），判定线横贯整个屏幕。
    /// </summary>
    internal static class LaneLayout
    {
        public const int LANE_COUNT = 8;
        /// <summary>
        /// 中心 C 区转成的 Tap/Hold 落在第 4、5 列之间，按其中任一列都算
        /// </summary>
        public const int CENTER_LANE = 4;
        public const int CENTER_ALT_LANE = 5;
        /// <summary>
        /// Touch 转 Tap 后仍保留该 TouchArea，用来标记落在中间两列之间的音符
        /// </summary>
        public const char CENTER_TOUCH_AREA = 'C';
        /// <summary>
        /// The narrowest lane area in world units: 70% of the screen width at 16:9, 85% at 4:3.
        /// </summary>
        public const float BASE_LANE_AREA_WIDTH = GameplayScreenLayout.FALLING_LANE_AREA_WIDTH;
        /// <summary>
        /// The share of the visible width inside the safe area that the lane area takes on wide screens.
        /// </summary>
        public const float SAFE_WIDTH_SHARE = 0.85f;
        /// <summary>
        /// Gets the width of the lane area in world units, set for the screen by <see cref="ConfigureForScreen"/> when a game starts.
        /// </summary>
        public static float LaneAreaWidth { get; private set; } = BASE_LANE_AREA_WIDTH;
        /// <summary>
        /// Gets the left edge of the lane area.
        /// </summary>
        public static float Left => -LaneAreaWidth / 2f;
        /// <summary>
        /// Gets the right edge of the lane area.
        /// </summary>
        public static float Right => LaneAreaWidth / 2f;
        /// <summary>
        /// Gets the width of one lane.
        /// </summary>
        public static float LaneWidth => LaneAreaWidth / LANE_COUNT;
        /// <summary>
        /// 最外两列的判定范围向轨道区外延伸的宽度
        /// </summary>
        public static float EdgeLaneTolerance => LaneWidth / 2f;
        public const float JUDGE_LINE_Y = -4.2f;
        public const float TOP_Y = 5.4f;
        public const float BOTTOM_Y = -5.4f;
        public const float TRAVEL_LENGTH = TOP_Y - JUDGE_LINE_Y;
        /// <summary>
        /// 垂直判定关闭时，判定线上下可按的半高
        /// </summary>
        public const float JUDGE_BAND_HALF_HEIGHT = 1.2f;
        /// <summary>
        /// 悬空 Touch 可按的半高，比 Touch 图形大；相邻高度的可按区域允许重叠
        /// </summary>
        public const float FLOAT_HIT_HALF_HEIGHT = 1.3f;
        /// <summary>
        /// 悬空 Touch 的可按区域在所在列两侧再各延伸的列数
        /// </summary>
        public const int FLOAT_HIT_EXTRA_LANES = 1;
        /// <summary>
        /// Slide 样式为悬空式时的第二条判定线：在判定线上方，刚好高出关闭垂直判定时 Tap 的判定带。星星箭头落到这里判定
        /// </summary>
        public const float FLOAT_SLIDE_LINE_Y = JUDGE_LINE_Y + JUDGE_BAND_HALF_HEIGHT * 2f;
        /// <summary>
        /// 当前 Slide 样式下星星箭头到达的判定线：悬空式为第二条判定线，否则为判定线
        /// </summary>
        public static float SlideLineY => LaneMode.IsSlideFloating ? FLOAT_SLIDE_LINE_Y : JUDGE_LINE_Y;

        /// <summary>
        /// Gets the lane area width for a screen: <see cref="SAFE_WIDTH_SHARE"/> of the width the falling-mode camera shows inside
        /// the safe area, but at least <see cref="BASE_LANE_AREA_WIDTH"/>.
        /// </summary>
        /// <remarks>
        /// The camera always shows 10.8 world units of height, or more on screens narrower than
        /// <see cref="GameplayScreenLayout.FALLING_MIN_VISIBLE_WIDTH"/> allows, so 4:3 screens keep the base width while wider screens
        /// and phones with notches get wider lanes.
        /// </remarks>
        /// <param name="screenWidth">The screen width in pixels, in landscape.</param>
        /// <param name="screenHeight">The screen height in pixels, in landscape.</param>
        /// <param name="safeWidth">The width of the safe area in pixels.</param>
        /// <returns>The lane area width in world units.</returns>
        public static float GetLaneAreaWidth(float screenWidth, float screenHeight, float safeWidth)
        {
            if (screenWidth <= 0f || screenHeight <= 0f)
            {
                return BASE_LANE_AREA_WIDTH;
            }
            var aspect = screenWidth / screenHeight;
            var halfHeight = Mathf.Max(GameplayScreenLayout.LANDSCAPE_HALF_HEIGHT, GameplayScreenLayout.FALLING_MIN_VISIBLE_WIDTH / 2f / aspect);
            var visibleWidth = halfHeight * 2f * aspect;
            var safeShare = Mathf.Clamp01(safeWidth / screenWidth);
            return Mathf.Max(BASE_LANE_AREA_WIDTH, visibleWidth * safeShare * SAFE_WIDTH_SHARE);
        }

        /// <summary>
        /// Sets <see cref="LaneAreaWidth"/> for the current screen as it is in falling mode, which phones and tablets always play in
        /// landscape even when the game starts before the screen has turned.
        /// </summary>
        public static void ConfigureForScreen()
        {
            float width = Screen.width;
            float height = Screen.height;
            var safeArea = Screen.safeArea;
            var safeWidth = safeArea.width;
#if UNITY_ANDROID || UNITY_IOS
            if (height > width)
            {
                // 还没转到横屏：按转过来以后的方向计算，长边为宽，刘海与底部横条的边距换到左右两侧
                (width, height) = (height, width);
                safeWidth = safeArea.height;
            }
#endif
            LaneAreaWidth = GetLaneAreaWidth(width, height, safeWidth);
        }

        /// <summary>
        /// Restores <see cref="LaneAreaWidth"/> to <see cref="BASE_LANE_AREA_WIDTH"/> when falling mode ends.
        /// </summary>
        public static void ResetWidth()
        {
            LaneAreaWidth = BASE_LANE_AREA_WIDTH;
        }

        // 圆形模式中音符从内圈 1.225 移动到判定圈 4.8，下落式把这段距离线性映射到轨道长度，出现时机不变。
        const float INNER_DISTANCE = 1.225f;
        const float OUTER_DISTANCE = 4.8f;
        const float DISTANCE_TO_LANE = TRAVEL_LENGTH / (OUTER_DISTANCE - INNER_DISTANCE);

        /// <summary>
        /// 圆形模式中音符朝外的方向，在下落式中对应朝下
        /// </summary>
        public static readonly Quaternion NoteRotation = Quaternion.Euler(0, 0, 180f);

        /// <summary>
        /// 圆形模式的距离差换算为轨道上的长度
        /// </summary>
        public static float DistanceScale
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => DISTANCE_TO_LANE;
        }
        /// <param name="lane">1~8</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetLaneX(int lane)
        {
            return Left + (lane - 0.5f) * LaneWidth;
        }
        /// <returns>1~8；不在任何轨道上时返回 0。稍微按出轨道区也算最外侧的轨道</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetLaneAt(float worldX)
        {
            if (worldX < Left - EdgeLaneTolerance || worldX > Right + EdgeLaneTolerance)
            {
                return 0;
            }
            var lane = Mathf.FloorToInt((worldX - Left) / LaneWidth) + 1;
            return Mathf.Clamp(lane, 1, LANE_COUNT);
        }
        /// <summary>
        /// 每列的判定范围是整列加左右各半列：手指在 <paramref name="lane"/> 的右半边时也算右边一列，左半边时也算左边一列
        /// </summary>
        /// <param name="lane">手指所在的列，1~8</param>
        /// <returns>同时算作的相邻列，1~8；没有时返回 0</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetNeighborLane(float worldX, int lane)
        {
            if (lane is < 1 or > LANE_COUNT)
            {
                return 0;
            }
            var offset = worldX - GetLaneX(lane);
            var neighbor = offset > 0 ? lane + 1 : offset < 0 ? lane - 1 : 0;
            return neighbor is >= 1 and <= LANE_COUNT ? neighbor : 0;
        }
        /// <summary>
        /// 把圆形模式中的径向距离投影为轨道上的高度
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetY(float distance)
        {
            var d = Mathf.Max(distance, INNER_DISTANCE);
            return JUDGE_LINE_Y + (OUTER_DISTANCE - d) * DISTANCE_TO_LANE;
        }
        /// <param name="lane">1~8</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetNotePosition(int lane, float distance)
        {
            return new Vector3(GetLaneX(lane), GetY(distance), 0);
        }
        /// <summary>
        /// 距离判定还有 <paramref name="timeToArrive"/> 秒的点所在高度（与音符同速）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetYByTime(float timeToArrive, float speed)
        {
            return JUDGE_LINE_Y + timeToArrive * speed * DISTANCE_TO_LANE;
        }
        /// <summary>
        /// 判定线上该轨道的位置
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetJudgePosition(int lane)
        {
            return new Vector3(GetLaneX(lane), JUDGE_LINE_Y, 0);
        }
        /// <summary>
        /// 传感器对应的轨道：A/B/D/E 的第 i 个对应轨道 i；C 对应中间的轨道 4、5（返回 0）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetSensorLane(SensorArea area)
        {
            return area switch
            {
                >= SensorArea.A1 and <= SensorArea.A8 => area - SensorArea.A1 + 1,
                >= SensorArea.B1 and <= SensorArea.B8 => area - SensorArea.B1 + 1,
                >= SensorArea.D1 and <= SensorArea.D8 => area - SensorArea.D1 + 1,
                >= SensorArea.E1 and <= SensorArea.E8 => area - SensorArea.E1 + 1,
                _ => 0,
            };
        }
        /// <summary>
        /// 传感器对应的轨道位掩码，第 n 位对应第 n + 1 列；C 对应中间两列
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetSensorLaneMask(SensorArea area)
        {
            var lane = GetSensorLane(area);
            return lane == 0 ? (1 << 3) | (1 << 4) : 1 << (lane - 1);
        }
        /// <param name="lane">1~8</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSensorInLane(SensorArea area, int lane)
        {
            var sensorLane = GetSensorLane(area);
            return sensorLane == 0 ? lane is 4 or 5 : sensorLane == lane;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetSensorX(SensorArea area)
        {
            var lane = GetSensorLane(area);
            return lane == 0 ? Left + LaneWidth * LANE_COUNT / 2f : GetLaneX(lane);
        }
        /// <summary>
        /// 悬空 Touch 的高度：越靠外圈越接近判定线，最高的 C 区也不到轨道一半，便于和判定线上的音符一起看
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetSensorFloatY(SensorArea area)
        {
            var ratio = area switch
            {
                >= SensorArea.A1 and <= SensorArea.A8 => 0.12f,
                >= SensorArea.D1 and <= SensorArea.D8 => 0.2f,
                >= SensorArea.E1 and <= SensorArea.E8 => 0.28f,
                >= SensorArea.B1 and <= SensorArea.B8 => 0.36f,
                _ => 0.45f,
            };
            return JUDGE_LINE_Y + TRAVEL_LENGTH * ratio;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetSensorFloatPosition(SensorArea area)
        {
            return new Vector3(GetSensorX(area), GetSensorFloatY(area), 0);
        }
        /// <summary>
        /// 世界坐标 <paramref name="position"/> 是否落在悬空 Touch 的可按区域内：所在列（C 为中间两列）加左右各 <see cref="FLOAT_HIT_EXTRA_LANES"/> 列，上下各 <see cref="FLOAT_HIT_HALF_HEIGHT"/>；
        /// 开启 Touch 垂直判定时不限高度
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInFloatHitArea(SensorArea area, Vector2 position)
        {
            var halfWidth = (GetSensorLane(area) == 0 ? LaneWidth : LaneWidth / 2f) + LaneWidth * FLOAT_HIT_EXTRA_LANES;
            return Mathf.Abs(position.x - GetSensorX(area)) <= halfWidth &&
                   (LaneMode.IsTouchVerticalJudge || Mathf.Abs(position.y - GetSensorFloatY(area)) <= FLOAT_HIT_HALF_HEIGHT);
        }
        /// <summary>
        /// 世界坐标 <paramref name="position"/> 是否按在地雷 Touch 本身上：只有所在列（C 为中间两列），上下各半列宽，与 Touch 图形相当；
        /// 不放大，也不受 Touch 垂直判定影响
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInFloatMineArea(SensorArea area, Vector2 position)
        {
            var halfWidth = GetSensorLane(area) == 0 ? LaneWidth : LaneWidth / 2f;
            return Mathf.Abs(position.x - GetSensorX(area)) <= halfWidth &&
                   Mathf.Abs(position.y - GetSensorFloatY(area)) <= LaneWidth / 2f;
        }
    }
}
