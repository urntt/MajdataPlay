using MajdataPlay.IO;
using MajdataPlay.Scenes.Game.Notes.Slide;
using System;
using System.Collections.Generic;
using UnityEngine;
#nullable enable
namespace MajdataPlay.Scenes.Game.Lane
{
    /// <summary>
    /// 下落式 Slide：沿判定区连成的路径等距排布皮肤里的 Slide 箭头，整体随时间下落。
    /// 路径落到判定线时所在的轨道，就是手指此时应在的位置；最后一个箭头正好落在结束的列上。
    /// </summary>
    internal sealed class LaneSlidePath
    {
        /// <summary>
        /// 相邻箭头的间距（世界单位），与圆形模式的 Slide 箭头相近
        /// </summary>
        const float ARROW_SPACING = 0.5f;
        /// <summary>
        /// 箭头中心超出轨道顶端多少后才隐藏，避免箭头在顶端突然消失
        /// </summary>
        const float TOP_MARGIN = 0.5f;
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
        readonly struct Arrow
        {
            public readonly float X;
            /// <summary>
            /// 相对高度：时刻 × 每秒下落的高度，加上当前帧的偏移即为世界坐标
            /// </summary>
            public readonly float Y;
            public readonly Quaternion Rotation;

            public Arrow(float x, float y, Quaternion rotation)
            {
                X = x;
                Y = y;
                Rotation = rotation;
            }
        }

        readonly Arrow[][] _paths;
        readonly int[] _firstVisible;
        readonly float _heightPerSec;
        readonly Sprite _sprite;
        readonly Material _material;
        readonly int _sortingOrder;
        // 只为同屏可见的箭头创建渲染器，结束后销毁
        readonly List<SpriteRenderer> _renderers = new();
        int _enabledCount = 0;
        bool _isEnded = false;

        /// <param name="judgeQueues">Slide 的判定队列（Wifi 为左、中、右三条）</param>
        /// <param name="startTiming">星星开始移动的时刻</param>
        /// <param name="length">星星移动所需时间</param>
        /// <param name="speed">与音符相同的下落速度</param>
        /// <param name="sprite">Slide 箭头，前进方向为本地 -X</param>
        public LaneSlidePath(ReadOnlySpan<Memory<SlideArea>> judgeQueues,
                             float startTiming,
                             float length,
                             float speed,
                             Sprite? sprite,
                             Material material,
                             int sortingOrder)
        {
            _heightPerSec = Mathf.Abs(speed) * LaneLayout.DistanceScale;
            _sprite = sprite != null ? sprite : LaneVisuals.WhiteSprite;
            _material = material;
            _sortingOrder = sortingOrder;
            var paths = new List<Arrow[]>(judgeQueues.Length);
            foreach (var queue in judgeQueues)
            {
                if (queue.IsEmpty)
                {
                    continue;
                }
                paths.Add(BuildArrows(BuildPath(queue.Span, startTiming, length), _heightPerSec));
            }
            _paths = paths.ToArray();
            _firstVisible = new int[_paths.Length];
        }
        /// <summary>
        /// 按当前时刻摆放箭头，只显示判定线与轨道顶端之间的部分
        /// </summary>
        public void Update(float thisFrameSec)
        {
            if (_isEnded)
            {
                return;
            }
            var offset = LaneLayout.JUDGE_LINE_Y - thisFrameSec * _heightPerSec;
            var top = LaneLayout.TOP_Y + TOP_MARGIN;
            var used = 0;
            for (var i = 0; i < _paths.Length; i++)
            {
                var arrows = _paths[i];
                var j = _firstVisible[i];
                // 相对高度单调不减，越过判定线的箭头之后不会再出现
                while (j < arrows.Length && arrows[j].Y + offset < LaneLayout.JUDGE_LINE_Y)
                {
                    j++;
                }
                _firstVisible[i] = j;
                for (; j < arrows.Length; j++)
                {
                    ref readonly var arrow = ref arrows[j];
                    var y = arrow.Y + offset;
                    if (y > top)
                    {
                        break;
                    }
                    var renderer = GetRenderer(used++);
                    renderer.transform.SetPositionAndRotation(new Vector3(arrow.X, y, 0), arrow.Rotation);
                }
            }
            for (var k = used; k < _enabledCount; k++)
            {
                _renderers[k].enabled = false;
            }
            _enabledCount = used;
        }
        /// <summary>
        /// Slide 结束后销毁箭头
        /// </summary>
        public void End()
        {
            if (_isEnded)
            {
                return;
            }
            _isEnded = true;
            foreach (var renderer in _renderers)
            {
                if (renderer != null)
                {
                    UnityEngine.Object.Destroy(renderer.gameObject);
                }
            }
            _renderers.Clear();
            _enabledCount = 0;
        }
        SpriteRenderer GetRenderer(int index)
        {
            if (index < _renderers.Count)
            {
                var renderer = _renderers[index];
                if (index >= _enabledCount)
                {
                    renderer.enabled = true;
                }
                return renderer;
            }
            var created = LaneVisuals.CreateSprite("LaneSlideArrow", GetRoot(), _sprite, _material, LaneVisuals.SlidePathSortingLayerId, _sortingOrder);
            _renderers.Add(created);
            return created;
        }
        /// <summary>
        /// 判定区到达判定线的时刻：判定区按箭头进度分布在星星移动的时间内，第一区对应起点，最后一区对应终点。
        /// 路径与检查点判定（LaneSlideJudge）共用，保证看到的和判定的一致。
        /// </summary>
        internal static float GetAreaTiming(ReadOnlySpan<SlideArea> areas, int index, float startTiming, float length)
        {
            var count = areas.Length;
            if (index >= count - 1)
            {
                return startTiming + length;
            }
            else if (index == 0)
            {
                return startTiming;
            }
            var totalProgress = Mathf.Max(1, areas[count - 1].ArrowProgressWhenFinished);
            ref readonly var area = ref areas[index];
            return startTiming + length * Mathf.Clamp01((area.ArrowProgressWhenOn + area.ArrowProgressWhenFinished) * 0.5f / totalProgress);
        }
        static Vertex[] BuildPath(ReadOnlySpan<SlideArea> areas, float startTiming, float length)
        {
            var count = areas.Length;
            var vertices = new List<Vertex>(count + 1);
            // 终点落在结束的列上：最后一个判定区优先取外圈 A 区所在的列
            var endX = GetEndAreaX(areas[count - 1]);
            if (count == 1)
            {
                // 只有一个判定区：从起点到终点停在同一列
                vertices.Add(new Vertex(startTiming, endX));
            }
            for (var i = 0; i < count; i++)
            {
                var x = i == count - 1 ? endX : GetAreaX(areas[i]);
                vertices.Add(new Vertex(GetAreaTiming(areas, i, startTiming, length), x));
            }
            return vertices.ToArray();
        }
        /// <summary>
        /// 从终点往回沿折线按弧长等距取点，保证最后一个箭头正好落在终点（结束的列）上；
        /// 箭头指向路径终点（时间更晚、位置更高的一端）
        /// </summary>
        static Arrow[] BuildArrows(Vertex[] vertices, float heightPerSec)
        {
            var arrows = new List<Arrow>();
            // 下一个箭头距当前线段终点的弧长
            var next = 0f;
            for (var i = vertices.Length - 1; i > 0; i--)
            {
                var from = new Vector2(vertices[i - 1].X, vertices[i - 1].Timing * heightPerSec);
                var to = new Vector2(vertices[i].X, vertices[i].Timing * heightPerSec);
                var delta = to - from;
                var length = delta.magnitude;
                if (length <= 1e-4f)
                {
                    continue;
                }
                var rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 180f);
                for (; next <= length; next += ARROW_SPACING)
                {
                    var point = to - delta * (next / length);
                    arrows.Add(new Arrow(point.x, point.y, rotation));
                }
                next -= length;
            }
            // 改回从起点到终点的顺序，相对高度单调不减
            arrows.Reverse();
            return arrows.ToArray();
        }
        /// <summary>
        /// 最后一个判定区的横坐标：含外圈 A 区时取 A 区所在的列（即 Slide 结束的列），否则取各区平均
        /// </summary>
        static float GetEndAreaX(in SlideArea area)
        {
            var sum = 0f;
            var count = 0;
            foreach (var sensor in area.IncludedAreas)
            {
                if (sensor is >= SensorArea.A1 and <= SensorArea.A8)
                {
                    sum += LaneLayout.GetSensorX(sensor);
                    count++;
                }
            }
            return count != 0 ? sum / count : GetAreaX(area);
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
