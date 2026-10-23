using MajdataPlay.Scenes.Game.Notes.Slide;
using System;
using System.Collections.Generic;
using UnityEngine;
#nullable enable
namespace MajdataPlay.Scenes.Game.Lane
{
    /// <summary>
    /// 下落式 Slide：把判定区序列画成一条随时间下落、在轨道间折线移动的路径线。
    /// 路径线落到判定线时所在的轨道，就是手指此时应在的位置。
    /// </summary>
    internal sealed class LaneSlidePath
    {
        const float LINE_WIDTH = 0.35f;
        const string ROOT_NAME = "LaneSlidePaths";

        static Transform? _root;

        readonly struct Vertex
        {
            public readonly float Timing;
            public readonly float X;

            public Vertex(float timing, float x)
            {
                Timing = timing;
                X = x;
            }
        }

        readonly Vertex[][] _paths;
        readonly float _speed;
        readonly Color _color;
        readonly Material _material;
        readonly int _sortingOrder;
        // 只在 Slide 处于活动状态时创建线段，结束后销毁，避免加载时为每个 Slide 生成物体
        SpriteRenderer[][]? _segments;
        bool _isEnded = false;

        /// <param name="judgeQueues">Slide 的判定队列（Wifi 为左、中、右三条）</param>
        /// <param name="startTiming">星星开始移动的时刻</param>
        /// <param name="length">星星移动所需时间</param>
        /// <param name="speed">与音符相同的下落速度</param>
        public LaneSlidePath(ReadOnlySpan<Memory<SlideArea>> judgeQueues,
                             float startTiming,
                             float length,
                             float speed,
                             Color color,
                             Material material,
                             int sortingOrder)
        {
            _speed = Mathf.Abs(speed);
            _color = color;
            _material = material;
            _sortingOrder = sortingOrder;
            var paths = new List<Vertex[]>(judgeQueues.Length);
            foreach (var queue in judgeQueues)
            {
                if (queue.IsEmpty)
                {
                    continue;
                }
                paths.Add(BuildPath(queue.Span, startTiming, length));
            }
            _paths = paths.ToArray();
        }
        /// <summary>
        /// 按当前时刻更新路径线，只绘制判定线与轨道顶端之间的部分
        /// </summary>
        public void Update(float thisFrameSec)
        {
            if (_isEnded)
            {
                return;
            }
            var allSegments = _segments ??= CreateSegments();
            for (var i = 0; i < _paths.Length; i++)
            {
                var path = _paths[i];
                var segments = allSegments[i];
                for (var j = 0; j < segments.Length; j++)
                {
                    var segment = segments[j];
                    var from = path[j];
                    var to = path[j + 1];
                    var fromY = LaneLayout.GetYByTime(from.Timing - thisFrameSec, _speed);
                    var toY = LaneLayout.GetYByTime(to.Timing - thisFrameSec, _speed);
                    if (!TryClip(new Vector2(from.X, fromY), new Vector2(to.X, toY), out var a, out var b))
                    {
                        segment.enabled = false;
                        continue;
                    }
                    segment.enabled = true;
                    LaneVisuals.SetSegment(segment, a, b, LINE_WIDTH);
                }
            }
        }
        /// <summary>
        /// Slide 结束后销毁路径线
        /// </summary>
        public void End()
        {
            if (_isEnded)
            {
                return;
            }
            _isEnded = true;
            if (_segments is null)
            {
                return;
            }
            foreach (var segments in _segments)
            {
                foreach (var segment in segments)
                {
                    if (segment != null)
                    {
                        UnityEngine.Object.Destroy(segment.gameObject);
                    }
                }
            }
            _segments = null;
        }
        SpriteRenderer[][] CreateSegments()
        {
            var parent = GetRoot();
            var sortingLayerId = LaneVisuals.SlidePathSortingLayerId;
            var allSegments = new SpriteRenderer[_paths.Length][];
            for (var i = 0; i < _paths.Length; i++)
            {
                var path = _paths[i];
                var segments = new SpriteRenderer[Math.Max(path.Length - 1, 0)];
                for (var j = 0; j < segments.Length; j++)
                {
                    var segment = LaneVisuals.CreateQuad("LaneSlideSegment", parent, _material, sortingLayerId, _sortingOrder, _color);
                    segment.enabled = false;
                    segments[j] = segment;
                }
                allSegments[i] = segments;
            }
            return allSegments;
        }
        static Vertex[] BuildPath(ReadOnlySpan<SlideArea> areas, float startTiming, float length)
        {
            var count = areas.Length;
            // 判定区按箭头进度分布在星星移动的时间内，最后一区对应 Slide 的终点
            var totalProgress = Mathf.Max(1, areas[count - 1].ArrowProgressWhenFinished);
            var vertices = new List<Vertex>(count + 1)
            {
                new Vertex(startTiming, GetAreaX(areas[0]))
            };
            for (var i = 1; i < count; i++)
            {
                ref readonly var area = ref areas[i];
                var progress = i == count - 1 ? 1f : Mathf.Clamp01((area.ArrowProgressWhenOn + area.ArrowProgressWhenFinished) * 0.5f / totalProgress);
                vertices.Add(new Vertex(startTiming + length * progress, GetAreaX(area)));
            }
            if (count == 1)
            {
                vertices.Add(new Vertex(startTiming + length, GetAreaX(areas[0])));
            }
            return vertices.ToArray();
        }
        static float GetAreaX(in SlideArea area)
        {
            var sensors = area.IncludedAreas;
            if (sensors.Length == 0)
            {
                return 0f;
            }
            var sum = 0f;
            foreach (var sensor in sensors)
            {
                sum += LaneLayout.GetSensorX(sensor);
            }
            return sum / sensors.Length;
        }
        static bool TryClip(Vector2 from, Vector2 to, out Vector2 a, out Vector2 b)
        {
            const float BOTTOM = LaneLayout.JUDGE_LINE_Y;
            const float TOP = LaneLayout.TOP_Y;
            a = from;
            b = to;
            var minY = Mathf.Min(from.y, to.y);
            var maxY = Mathf.Max(from.y, to.y);
            if (maxY < BOTTOM || minY > TOP)
            {
                return false;
            }
            if (Mathf.Approximately(from.y, to.y))
            {
                return true;
            }
            a = ClipPoint(from, to, BOTTOM, TOP);
            b = ClipPoint(to, from, BOTTOM, TOP);
            return true;
        }
        /// <summary>
        /// 把 <paramref name="point"/> 沿线段方向收回到 [bottom, top] 之内
        /// </summary>
        static Vector2 ClipPoint(Vector2 point, Vector2 other, float bottom, float top)
        {
            var targetY = point.y < bottom ? bottom : point.y > top ? top : point.y;
            if (targetY == point.y)
            {
                return point;
            }
            var t = (targetY - point.y) / (other.y - point.y);
            return Vector2.LerpUnclamped(point, other, t);
        }
        static Transform GetRoot()
        {
            if (_root == null)
            {
                _root = new GameObject(ROOT_NAME).transform;
            }
            return _root;
        }
    }
}
