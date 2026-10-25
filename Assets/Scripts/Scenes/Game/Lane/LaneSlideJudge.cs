using MajdataPlay.Scenes.Game.Notes;
using System;
using System.Collections.Generic;
using UnityEngine;
#nullable enable
namespace MajdataPlay.Scenes.Game.Lane
{
    /// <summary>
    /// 下落式 Slide 的 Drag 式判定：沿箭头路径每隔一小段时间取一个点，相当于一串隐形的 Drag。
    /// 点到达判定线前后一小段时间内，有手指的横坐标离箭头位置不超过一列半宽即算命中；
    /// 提前命中不会让后面的点提前结算。全部点结算后，按命中情况以及首、尾点是否命中给出评级。
    /// </summary>
    internal sealed class LaneSlideJudge
    {
        /// <summary>
        /// 取点间隔
        /// </summary>
        const float SAMPLE_INTERVAL_SEC = 0.05f;
        /// <summary>
        /// 点到达判定线前多久开始接受手指
        /// </summary>
        const float EARLY_SEC = 0.15f;
        /// <summary>
        /// 点到达判定线后多久仍未命中则记为漏掉
        /// </summary>
        const float LATE_SEC = 0.15f;
        /// <summary>
        /// 手指横坐标与箭头位置相差不超过该值即算命中（左右各一列半）；扫键也用它判断手指是否正在跟这条星星
        /// </summary>
        internal const float HIT_HALF_WIDTH = LaneLayout.LANE_WIDTH * 1.5f;
        /// <summary>
        /// 路径中横向跨过该距离的一段视为 8 与 1 之间的跳跃（圆周上相邻，轨道上却在两端）
        /// </summary>
        const float WRAP_DISTANCE = LaneLayout.LANE_WIDTH * 4;
        /// <summary>
        /// 跳跃结束后留给手指横移的时间，期间的点不计
        /// </summary>
        const float WRAP_GRACE_SEC = 0.15f;

        struct Sample
        {
            public float Timing;
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
        /// <summary>
        /// 每帧调用，按顺序结算各点
        /// </summary>
        /// <param name="fingerXs">本帧手指的横坐标</param>
        /// <returns>本帧是否有点首次被命中</returns>
        public bool Update(float thisFrameSec, ReadOnlySpan<float> fingerXs)
        {
            var isNewlyHit = false;
            for (var i = _next; i < _samples.Length; i++)
            {
                ref var sample = ref _samples[i];
                if (thisFrameSec < sample.Timing - EARLY_SEC)
                {
                    break;
                }
                if (!sample.IsHit && thisFrameSec <= sample.Timing + sample.LateSec && IsAnyFingerNear(i, fingerXs))
                {
                    sample.IsHit = true;
                    isNewlyHit = true;
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
        /// 判定期间（第一个点之前 <see cref="EARLY_SEC"/> 起，到全部点结算为止）各条路径在 <paramref name="now"/> 时的横坐标，
        /// 用于判断手指是否正在跟这条星星
        /// </summary>
        /// <param name="output">长度不少于路径数（Wifi 为 3）</param>
        /// <returns>写入的数量；不在判定期间时为 0</returns>
        public int GetActiveXs(float now, Span<float> output)
        {
            if (IsResolved || _samples.Length == 0 || now < _samples[0].Timing - EARLY_SEC)
            {
                return 0;
            }
            var count = Mathf.Min(_pathCount, output.Length);
            for (var p = 0; p < count; p++)
            {
                output[p] = GetX(_paths[p], now);
            }
            return count;
        }
        /// <summary>
        /// 从上往下取第一个满足的：
        /// 全部命中为 Critical Perfect；首、尾都命中为 Perfect；首命中且另有任一点命中为 Great；
        /// 有任一点命中为 Good；都没命中为 Miss。
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
        bool IsAnyFingerNear(int sampleIndex, ReadOnlySpan<float> fingerXs)
        {
            var offset = sampleIndex * _pathCount;
            for (var p = 0; p < _pathCount; p++)
            {
                var x = _sampleXs[offset + p];
                foreach (var fingerX in fingerXs)
                {
                    if (Mathf.Abs(fingerX - x) <= HIT_HALF_WIDTH)
                    {
                        return true;
                    }
                }
            }
            return false;
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
                    if (Mathf.Abs(to.X - from.X) < WRAP_DISTANCE)
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
