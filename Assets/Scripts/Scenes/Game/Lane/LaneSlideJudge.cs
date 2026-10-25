using MajdataPlay.Scenes.Game.Notes;
using MajdataPlay.Scenes.Game.Notes.Slide;
using System;
using UnityEngine;
#nullable enable
namespace MajdataPlay.Scenes.Game.Lane
{
    /// <summary>
    /// 下落式 Slide 的 Phigros 式检查点判定：路径上每个判定区对应一个检查点，相当于一个隐形 Drag。
    /// 检查点到达判定线前后一小段时间内，手指在它所在的列上即算命中；提前按住不会让后面的检查点提前结算。
    /// 全部检查点结算后，按命中数以及首、尾检查点是否命中给出评级。
    /// </summary>
    internal sealed class LaneSlideJudge
    {
        /// <summary>
        /// 检查点到达判定线前多久开始接受按住
        /// </summary>
        const float EARLY_SEC = 0.15f;
        /// <summary>
        /// 检查点到达判定线后多久仍未按住则记为漏掉
        /// </summary>
        const float LATE_SEC = 0.15f;

        struct Checkpoint
        {
            public float Timing;
            /// <summary>
            /// 可以命中的轨道，第 n 位对应第 n + 1 列
            /// </summary>
            public int LaneMask;
            public bool IsHit;
        }

        readonly Checkpoint[] _checkpoints;
        // 第一个尚未结算的检查点
        int _next = 0;

        public int HitCount { get; private set; } = 0;
        public int TotalCount => _checkpoints.Length;
        /// <summary>
        /// 全部检查点都已结算
        /// </summary>
        public bool IsResolved => _next >= _checkpoints.Length;
        /// <summary>
        /// 第一个检查点（星星的起点）是否命中
        /// </summary>
        public bool IsFirstHit => _checkpoints.Length != 0 && _checkpoints[0].IsHit;
        /// <summary>
        /// 最后一个检查点（星星的终点）是否命中
        /// </summary>
        public bool IsLastHit => _checkpoints.Length != 0 && _checkpoints[^1].IsHit;

        /// <param name="judgeQueues">Slide 的判定队列（Wifi 为左、中、右三条）</param>
        /// <param name="startTiming">星星开始移动的时刻</param>
        /// <param name="length">星星移动所需时间</param>
        public LaneSlideJudge(ReadOnlySpan<Memory<SlideArea>> judgeQueues, float startTiming, float length)
        {
            if (judgeQueues.Length == 3 && !judgeQueues[0].IsEmpty && !judgeQueues[1].IsEmpty && !judgeQueues[2].IsEmpty)
            {
                _checkpoints = BuildWifi(judgeQueues[0].Span, judgeQueues[1].Span, judgeQueues[2].Span, startTiming, length);
                return;
            }
            foreach (var queue in judgeQueues)
            {
                if (!queue.IsEmpty)
                {
                    _checkpoints = Build(queue.Span, startTiming, length);
                    return;
                }
            }
            _checkpoints = Array.Empty<Checkpoint>();
        }
        /// <summary>
        /// 每帧调用，按顺序结算检查点
        /// </summary>
        /// <param name="heldLaneMask">本帧被按住的轨道，第 n 位对应第 n + 1 列</param>
        /// <returns>本帧是否有检查点首次被命中</returns>
        public bool Update(float thisFrameSec, int heldLaneMask)
        {
            var isNewlyHit = false;
            for (var i = _next; i < _checkpoints.Length; i++)
            {
                ref var checkpoint = ref _checkpoints[i];
                if (thisFrameSec < checkpoint.Timing - EARLY_SEC)
                {
                    break;
                }
                if (!checkpoint.IsHit && (heldLaneMask & checkpoint.LaneMask) != 0)
                {
                    checkpoint.IsHit = true;
                    isNewlyHit = true;
                }
            }
            // 命中的检查点等到达判定线时才结算，整条 Slide 不会提前结束
            while (_next < _checkpoints.Length)
            {
                ref var checkpoint = ref _checkpoints[_next];
                var isResolved = checkpoint.IsHit ? thisFrameSec >= checkpoint.Timing
                                                  : thisFrameSec > checkpoint.Timing + LATE_SEC;
                if (!isResolved)
                {
                    break;
                }
                if (checkpoint.IsHit)
                {
                    HitCount++;
                }
                _next++;
            }
            return isNewlyHit;
        }
        /// <summary>
        /// 从上往下取第一个满足的：
        /// 全部命中为 Critical Perfect；首、尾都命中为 Perfect；首命中且另有任一检查点命中为 Great；
        /// 有任一检查点命中为 Good；都没命中为 Miss。
        /// 没有快慢之分，调用方需将 JudgeDiff 置 0。
        /// </summary>
        public static JudgeGrade GetGrade(int hitCount, int totalCount, bool isFirstHit, bool isLastHit)
        {
            if (totalCount <= 0 || hitCount >= totalCount)
            {
                return JudgeGrade.Perfect;
            }
            else if (isFirstHit && isLastHit)
            {
                return JudgeGrade.LatePerfect2nd;
            }
            else if (isFirstHit && hitCount >= 2)
            {
                return JudgeGrade.LateGreat;
            }
            else if (hitCount > 0)
            {
                return JudgeGrade.LateGood;
            }
            return JudgeGrade.Miss;
        }
        static Checkpoint[] Build(ReadOnlySpan<SlideArea> areas, float startTiming, float length)
        {
            var checkpoints = new Checkpoint[areas.Length];
            for (var i = 0; i < areas.Length; i++)
            {
                checkpoints[i] = new Checkpoint()
                {
                    Timing = LaneSlidePath.GetAreaTiming(areas, i, startTiming, length),
                    LaneMask = GetLaneMask(areas[i])
                };
            }
            return checkpoints;
        }
        /// <summary>
        /// Wifi 以中间一条为准，每个检查点可以命中左、中、右三条路径在同一进度上的任一列
        /// </summary>
        static Checkpoint[] BuildWifi(ReadOnlySpan<SlideArea> left,
                                      ReadOnlySpan<SlideArea> center,
                                      ReadOnlySpan<SlideArea> right,
                                      float startTiming,
                                      float length)
        {
            var checkpoints = new Checkpoint[center.Length];
            for (var i = 0; i < center.Length; i++)
            {
                checkpoints[i] = new Checkpoint()
                {
                    Timing = LaneSlidePath.GetAreaTiming(center, i, startTiming, length),
                    LaneMask = GetLaneMask(center[i]) |
                               GetLaneMask(left[MapIndex(i, center.Length, left.Length)]) |
                               GetLaneMask(right[MapIndex(i, center.Length, right.Length)])
                };
            }
            return checkpoints;
        }
        static int MapIndex(int index, int fromLength, int toLength)
        {
            if (fromLength <= 1)
            {
                return toLength - 1;
            }
            return Mathf.Clamp(Mathf.RoundToInt(index * (toLength - 1) / (float)(fromLength - 1)), 0, toLength - 1);
        }
        static int GetLaneMask(in SlideArea area)
        {
            var mask = 0;
            foreach (var sensor in area.IncludedAreas)
            {
                mask |= LaneLayout.GetSensorLaneMask(sensor);
            }
            return mask;
        }
    }
}
