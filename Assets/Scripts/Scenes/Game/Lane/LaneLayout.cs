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

        public static void Enable(GameOptions options)
        {
            IsEnabled = true;
            IsVerticalJudge = options.FallingVerticalJudge;
            IsBreakToFlick = options.FallingBreakToFlick;
        }
        public static void Disable()
        {
            IsEnabled = false;
            IsVerticalJudge = true;
            IsBreakToFlick = false;
        }
    }
    /// <summary>
    /// 下落式模式的轨道几何。游玩区固定为 19.2×10.8 世界单位（16:9，对应画布 1920×1080），
    /// 8 条轨道铺满游玩区宽度；相机与画布按横屏布局适配屏幕，多余部分留黑边。
    /// </summary>
    internal static class LaneLayout
    {
        public const int LANE_COUNT = 8;
        public const float PLAY_AREA_WIDTH = 19.2f;
        public const float PLAY_AREA_HEIGHT = 10.8f;
        public const float LEFT = -PLAY_AREA_WIDTH / 2f;
        public const float LANE_WIDTH = PLAY_AREA_WIDTH / LANE_COUNT;
        public const float JUDGE_LINE_Y = -4.2f;
        public const float TOP_Y = 5.4f;
        public const float BOTTOM_Y = -5.4f;
        public const float TRAVEL_LENGTH = TOP_Y - JUDGE_LINE_Y;
        /// <summary>
        /// 垂直判定关闭时，判定线上下可按的半高
        /// </summary>
        public const float JUDGE_BAND_HALF_HEIGHT = 1.2f;
        /// <summary>
        /// 悬空 Touch 可按的半高
        /// </summary>
        public const float FLOAT_BAND_HALF_HEIGHT = 0.48f;

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
            return LEFT + (lane - 0.5f) * LANE_WIDTH;
        }
        /// <returns>1~8；不在任何轨道上时返回 0</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetLaneAt(float worldX)
        {
            var lane = Mathf.FloorToInt((worldX - LEFT) / LANE_WIDTH) + 1;
            return lane is >= 1 and <= LANE_COUNT ? lane : 0;
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
            return lane == 0 ? LEFT + LANE_WIDTH * LANE_COUNT / 2f : GetLaneX(lane);
        }
        /// <summary>
        /// 悬空 Touch 的高度：越靠外圈越接近判定线
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetSensorFloatY(SensorArea area)
        {
            var ratio = area switch
            {
                >= SensorArea.A1 and <= SensorArea.A8 => 0.15f,
                >= SensorArea.D1 and <= SensorArea.D8 => 0.25f,
                >= SensorArea.E1 and <= SensorArea.E8 => 0.4f,
                >= SensorArea.B1 and <= SensorArea.B8 => 0.55f,
                _ => 0.7f,
            };
            return JUDGE_LINE_Y + TRAVEL_LENGTH * ratio;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetSensorFloatPosition(SensorArea area)
        {
            return new Vector3(GetSensorX(area), GetSensorFloatY(area), 0);
        }
    }
}
