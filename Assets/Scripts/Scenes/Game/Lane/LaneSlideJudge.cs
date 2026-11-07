using MajdataPlay.Scenes.Game.Notes;
using System;
using System.Collections.Generic;
using UnityEngine;
#nullable enable
namespace MajdataPlay.Scenes.Game.Lane
{
    /// <summary>
    /// 下落式 Slide 的 Drag 式判定：沿箭头路径每隔一小段时间取一个点，相当于一串隐形的 Drag。
    /// 点到达判定线（悬空式为第二判定线）前后 0.3 秒内，有手指的横坐标离箭头位置不超过两列半宽（合计五列）即算命中；
    /// 提前命中不会让后面的点提前结算。全部点结算后由 <see cref="LaneSlideGrading"/> 评级。
    /// 星星尾转 Flick 时改为只有一个点，需要在所在列划动（见 <see cref="CreateFlick"/>）。
    /// </summary>
    internal sealed class LaneSlideJudge
    {
        /// <summary>
        /// 取点间隔
        /// </summary>
        const float SAMPLE_INTERVAL_SEC = 0.05f;
        /// <summary>
        /// 检查点到达判定线前多久开始接受手指
        /// </summary>
        const float EARLY_SEC = 0.3f;
        /// <summary>
        /// 检查点到达判定线后多久仍未命中则记为漏掉
        /// </summary>
        const float LATE_SEC = 0.3f;
        /// <summary>
        /// 星星尾转 Flick：与 Tap 的判定范围相同，到达判定线前后 0.15 秒内划动才算
        /// </summary>
        const float FLICK_EARLY_SEC = 0.15f;
        /// <summary>
        /// 星星尾转 Flick：到达判定线后多久仍未划动则记为漏掉
        /// </summary>
        const float FLICK_LATE_SEC = 0.15f;
        /// <summary>
        /// 手指横坐标与箭头位置相差不超过该值即算命中：左右各两列半，合计五列；
        /// “Slide 样式”为标准时，扫键保护也用它判断手指是否在打这条星星
        /// </summary>
        internal static float HitHalfWidth => LaneLayout.LaneWidth * 2.5f;
        /// <summary>
        /// 路径中横向跨过该距离的一段视为 8 与 1 之间的跳跃（圆周上相邻，轨道上却在两端）
        /// </summary>
        static float WrapDistance => LaneLayout.LaneWidth * 4;
        /// <summary>
        /// 跳跃结束后留给手指横移的时间，期间的点不计
        /// </summary>
        const float WRAP_GRACE_SEC = 0.15f;

        struct Sample
        {
            public float Timing;
            /// <summary>
            /// 到达判定线前多久开始可以命中
            /// </summary>
            public float EarlySec;
            /// <summary>
            /// 到达判定线后仍可命中的时间
            /// </summary>
            public float LateSec;
            public bool IsHit;
        }

        readonly LanePathVertex[][] _paths;
        readonly Sample[] _samples;
        // 每个点在各条路径上的横坐标，按 [点序号 * 路径数 + 路径序号] 存放；Wifi 有三条路径
        readonly float[] _sampleXs;
        readonly int _pathCount;
        // 第一个尚未结算的点
        int _next = 0;
        readonly bool _isFlick;

        public int HitCount { get; private set; } = 0;
        public int TotalCount => _samples.Length;
        /// <summary>
        /// 全部点都已结算
        /// </summary>
        public bool IsResolved => _next >= _samples.Length;
        /// <summary>
        /// 第一个点（星星的起点）是否命中
        /// </summary>
        public bool IsFirstHit => _samples.Length != 0 && _samples[0].IsHit;
        /// <summary>
        /// 最后一个点（星星的终点）是否命中
        /// </summary>
        public bool IsLastHit => _samples.Length != 0 && _samples[^1].IsHit;
        /// <summary>
        /// Gets when the earliest hit check point reaches the judge line, or <see langword="null"/> while no check point is hit.
        /// </summary>
        /// <remarks>
        /// A check point can be hit up to <see cref="EARLY_SEC"/> before it arrives, for example by a finger resting where the star
        /// will pass; the slide sound waits for this time so it is not played early.
        /// </remarks>
        public float? FirstHitTiming { get; private set; }
        /// <summary>
        /// 星星尾转 Flick：唯一的点需要在 <see cref="FlickLane"/> 划动才算命中
        /// </summary>
        public bool IsFlick => _isFlick;
        /// <summary>
        /// Flick 所在的列（1~8）
        /// </summary>
        public int FlickLane { get; }
        /// <summary>
        /// 星星尾转 Flick：Flick 的判定时刻（含判定偏移）；不是 Flick 时为 0
        /// </summary>
        public float FlickTiming => _isFlick ? _samples[0].Timing : 0f;
        /// <summary>
        /// Flick 命中时与判定时刻的差（秒，晚为正）；未命中为 null
        /// </summary>
        public float? FlickDiffSec { get; private set; }

        /// <param name="paths">由 <see cref="LaneSlidePath.BuildPaths"/> 生成的路径，与箭头共用</param>
        /// <param name="startTiming">星星开始移动的时刻</param>
        /// <param name="length">星星移动所需时间</param>
        public LaneSlideJudge(LanePathVertex[][] paths, float startTiming, float length)
        {
            _paths = paths;
            _pathCount = paths.Length;
            if (_pathCount == 0)
            {
                _samples = Array.Empty<Sample>();
                _sampleXs = Array.Empty<float>();
                return;
            }
            var intervalCount = Mathf.Max(1, Mathf.CeilToInt(length / SAMPLE_INTERVAL_SEC));
            var interval = length / intervalCount;
            var samples = new List<Sample>(intervalCount + 1);
            var sampleXs = new List<float>((intervalCount + 1) * _pathCount);
            for (var k = 0; k <= intervalCount; k++)
            {
                var isLast = k == intervalCount;
                var timing = isLast ? startTiming + length : startTiming + interval * k;
                var lateSec = LATE_SEC;
                if (k != 0 && IsInWrapGrace(paths, timing))
                {
                    // 起点、终点必取；终点落在跳跃里时多留一些时间
                    if (!isLast)
                    {
                        continue;
                    }
                    lateSec += WRAP_GRACE_SEC;
                }
                samples.Add(new Sample()
                {
                    Timing = timing,
                    EarlySec = EARLY_SEC,
                    LateSec = lateSec
                });
                foreach (var path in paths)
                {
                    sampleXs.Add(GetX(path, timing));
                }
            }
            _samples = samples.ToArray();
            _sampleXs = sampleXs.ToArray();
        }
        LaneSlideJudge(float flickTiming, int lane)
        {
            _paths = Array.Empty<LanePathVertex[]>();
            _pathCount = 0;
            _sampleXs = Array.Empty<float>();
            _samples = new[] { new Sample() { Timing = flickTiming, EarlySec = FLICK_EARLY_SEC, LateSec = FLICK_LATE_SEC } };
            _isFlick = true;
            FlickLane = lane;
        }
        /// <summary>
        /// 星星尾转 Flick：整条箭头换成一个 Flick。前后 <see cref="FLICK_EARLY_SEC"/>、<see cref="FLICK_LATE_SEC"/>（与 Tap 的判定范围相同）内
        /// 在 <paramref name="lane"/> 划动即命中，由 <see cref="HitFlick"/> 记录时间差，过晚则记为漏掉
        /// </summary>
        /// <param name="timing">含判定偏移的判定时刻</param>
        public static LaneSlideJudge CreateFlick(float timing, int lane)
        {
            return new LaneSlideJudge(timing, lane);
        }
        /// <summary>
        /// 星星尾转 Flick 时连接 Slide 的后续各段：没有自己的点，跟随持有 Flick 的第一段结算
        /// </summary>
        public static LaneSlideJudge CreateFlickFollower()
        {
            return new LaneSlideJudge(Array.Empty<LanePathVertex[]>(), 0f, 0f);
        }
        /// <summary>
        /// 星星尾转 Flick：现在是否可以用一次划动命中
        /// </summary>
        public bool IsFlickJudgeable(float now)
        {
            if (!_isFlick || IsResolved)
            {
                return false;
            }
            ref readonly var sample = ref _samples[0];
            return now >= sample.Timing - sample.EarlySec && now <= sample.Timing + sample.LateSec;
        }
        /// <summary>
        /// 星星尾转 Flick：在 <paramref name="now"/> 划动命中，立即结算
        /// </summary>
        public void HitFlick(float now)
        {
            if (!IsFlickJudgeable(now))
            {
                return;
            }
            ref var sample = ref _samples[0];
            sample.IsHit = true;
            FlickDiffSec = now - sample.Timing;
            HitCount = 1;
            _next = 1;
        }
        /// <summary>
        /// 每帧调用，按顺序结算各点
        /// </summary>
        /// <param name="thisFrameSec">本帧的判定时刻</param>
        /// <param name="fingers">本帧可以打检查点的手指</param>
        /// <param name="hitFingerKeys">不为空时，加入本帧位于某个可命中检查点范围内的手指的键（用于扫键保护）</param>
        /// <returns>本帧是否有点首次被命中</returns>
        public bool Update(float thisFrameSec, ReadOnlySpan<LaneFinger> fingers, List<int>? hitFingerKeys)
        {
            var isNewlyHit = false;
            for (var i = _next; i < _samples.Length; i++)
            {
                ref var sample = ref _samples[i];
                if (thisFrameSec < sample.Timing - sample.EarlySec)
                {
                    break;
                }
                // Flick 只能由 HitFlick 命中
                if (_isFlick || thisFrameSec > sample.Timing + sample.LateSec || (sample.IsHit && hitFingerKeys is null))
                {
                    continue;
                }
                if (CollectNearFingers(i, fingers, hitFingerKeys) && !sample.IsHit)
                {
                    sample.IsHit = true;
                    isNewlyHit = true;
                    if (FirstHitTiming is not float firstHitTiming || sample.Timing < firstHitTiming)
                    {
                        FirstHitTiming = sample.Timing;
                    }
                }
            }
            // 命中的点等到达判定线时才结算，整条 Slide 不会提前结束
            while (_next < _samples.Length)
            {
                ref var sample = ref _samples[_next];
                var isResolved = sample.IsHit ? thisFrameSec >= sample.Timing
                                              : thisFrameSec > sample.Timing + sample.LateSec;
                if (!isResolved)
                {
                    break;
                }
                if (sample.IsHit)
                {
                    HitCount++;
                }
                _next++;
            }
            return isNewlyHit;
        }
        /// <summary>
        /// 检查哪些手指在某个点的命中范围内（离任一路径上的箭头位置不超过 <see cref="HitHalfWidth"/>）
        /// </summary>
        /// <param name="sampleIndex">点的序号</param>
        /// <param name="fingers">本帧可以打检查点的手指</param>
        /// <param name="nearFingerKeys">不为空时加入范围内手指的键（不重复）</param>
        /// <returns>是否有手指在范围内</returns>
        bool CollectNearFingers(int sampleIndex, ReadOnlySpan<LaneFinger> fingers, List<int>? nearFingerKeys)
        {
            var offset = sampleIndex * _pathCount;
            var isAnyNear = false;
            foreach (var finger in fingers)
            {
                for (var p = 0; p < _pathCount; p++)
                {
                    if (Mathf.Abs(finger.X - _sampleXs[offset + p]) > HitHalfWidth)
                    {
                        continue;
                    }
                    isAnyNear = true;
                    if (nearFingerKeys is null)
                    {
                        return true;
                    }
                    if (!nearFingerKeys.Contains(finger.Key))
                    {
                        nearFingerKeys.Add(finger.Key);
                    }
                    break;
                }
            }
            return isAnyNear;
        }
        /// <summary>
        /// 路径在 <paramref name="timing"/> 时的横坐标，在相邻顶点之间线性插值，与箭头的摆放一致
        /// </summary>
        static float GetX(LanePathVertex[] path, float timing)
        {
            if (timing <= path[0].Timing)
            {
                return path[0].X;
            }
            for (var i = 1; i < path.Length; i++)
            {
                var to = path[i];
                if (timing > to.Timing)
                {
                    continue;
                }
                var from = path[i - 1];
                var duration = to.Timing - from.Timing;
                return duration <= 1e-5f ? to.X : Mathf.Lerp(from.X, to.X, (timing - from.Timing) / duration);
            }
            return path[^1].X;
        }
        /// <summary>
        /// <paramref name="timing"/> 是否落在某条路径 8 与 1 之间的跳跃上，或跳跃结束后的缓冲时间内
        /// </summary>
        static bool IsInWrapGrace(LanePathVertex[][] paths, float timing)
        {
            foreach (var path in paths)
            {
                for (var i = 1; i < path.Length; i++)
                {
                    var from = path[i - 1];
                    var to = path[i];
                    if (Mathf.Abs(to.X - from.X) < WrapDistance)
                    {
                        continue;
                    }
                    if (timing > from.Timing && timing < to.Timing + WRAP_GRACE_SEC)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
