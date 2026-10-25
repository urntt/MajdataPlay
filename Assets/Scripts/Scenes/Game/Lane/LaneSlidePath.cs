using MajdataPlay.Scenes.Game.Notes.Slide;
using System;
using System.Collections.Generic;
using UnityEngine;
#nullable enable
namespace MajdataPlay.Scenes.Game.Lane
{
    /// <summary>
    /// 下落式 Slide：沿判定区连成的路径等距排布皮肤里的 Slide 箭头，整体随时间下落，终点画一颗星星标出结束的列。
    /// 路径落到判定线时所在的轨道，就是手指此时应在的位置。
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
        const float END_MARKER_SCALE = 0.8f;

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
        // 终点星星的位置（相对高度同 Arrow.Y）；没有终点标记时为 null
        readonly Vector2? _endPoint;
        readonly Sprite? _endSprite;
        SpriteRenderer? _endMarker;
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
        /// <param name="endSprite">终点标记（星星）；为 null 时不画，例如连接 Slide 的中间段</param>
        public LaneSlidePath(ReadOnlySpan<Memory<SlideArea>> judgeQueues,
                             float startTiming,
                             float length,
                             float speed,
                             Sprite? sprite,
                             Sprite? endSprite,
                             Material material,
                             int sortingOrder)
        {
            _heightPerSec = Mathf.Abs(speed) * LaneLayout.DistanceScale;
            _sprite = sprite != null ? sprite : LaneVisuals.WhiteSprite;
            _endSprite = endSprite;
            _material = material;
            _sortingOrder = sortingOrder;
            var paths = new List<Arrow[]>(judgeQueues.Length);
            var vertexPaths = new List<Vertex[]>(judgeQueues.Length);
            foreach (var queue in judgeQueues)
            {
                if (queue.IsEmpty)
                {
                    continue;
                }
                var vertices = BuildPath(queue.Span, startTiming, length);
                vertexPaths.Add(vertices);
                paths.Add(BuildArrows(vertices, _heightPerSec));
            }
            _paths = paths.ToArray();
            _firstVisible = new int[_paths.Length];
            if (endSprite != null && vertexPaths.Count != 0)
            {
                // Wifi 只标中间一条的终点
                var path = vertexPaths[vertexPaths.Count == 3 ? 1 : 0];
                var end = path[path.Length - 1];
                _endPoint = new Vector2(end.X, end.Timing * _heightPerSec);
            }
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
            UpdateEndMarker(offset, top);
        }
        void UpdateEndMarker(float offset, float top)
        {
            if (_endPoint is not Vector2 endPoint)
            {
                return;
            }
            var y = endPoint.y + offset;
            var isVisible = y >= LaneLayout.JUDGE_LINE_Y && y <= top;
            if (!isVisible)
            {
                if (_endMarker != null)
                {
                    _endMarker.enabled = false;
                }
                return;
            }
            if (_endMarker == null)
            {
                _endMarker = LaneVisuals.CreateSprite("LaneSlideEnd", GetRoot(), _endSprite!, _material, LaneVisuals.SlidePathSortingLayerId, _sortingOrder + 1);
                _endMarker.transform.localScale = new Vector3(END_MARKER_SCALE, END_MARKER_SCALE, 1f);
            }
            _endMarker.enabled = true;
            _endMarker.transform.position = new Vector3(endPoint.x, y, 0);
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
            if (_endMarker != null)
            {
                UnityEngine.Object.Destroy(_endMarker.gameObject);
                _endMarker = null;
            }
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
            if (count == 1)
            {
                // 只有一个判定区：从起点到终点停在同一列
                vertices.Add(new Vertex(startTiming, GetAreaX(areas[0])));
            }
            for (var i = 0; i < count; i++)
            {
                vertices.Add(new Vertex(GetAreaTiming(areas, i, startTiming, length), GetAreaX(areas[i])));
            }
            return vertices.ToArray();
        }
        /// <summary>
        /// 沿折线按弧长等距取点，箭头指向路径终点（时间更晚、位置更高的一端）
        /// </summary>
        static Arrow[] BuildArrows(Vertex[] vertices, float heightPerSec)
        {
            var arrows = new List<Arrow>();
            // 下一个箭头距当前线段起点的弧长
            var next = ARROW_SPACING / 2f;
            for (var i = 0; i < vertices.Length - 1; i++)
            {
                var from = new Vector2(vertices[i].X, vertices[i].Timing * heightPerSec);
                var to = new Vector2(vertices[i + 1].X, vertices[i + 1].Timing * heightPerSec);
                var delta = to - from;
                var length = delta.magnitude;
                if (length <= 1e-4f)
                {
                    continue;
                }
                var rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 180f);
                for (; next <= length; next += ARROW_SPACING)
                {
                    var point = from + delta * (next / length);
                    arrows.Add(new Arrow(point.x, point.y, rotation));
                }
                next -= length;
            }
            return arrows.ToArray();
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
