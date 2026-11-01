using System;
using UnityEngine;
using UnityEngine.UI;
#nullable enable
namespace MajdataPlay.Scenes.List
{
    /// <summary>
    /// 列表式选歌的谱面雷达：画出网格与按各维度数值（0~1）连成的多边形
    /// </summary>
    internal sealed class ListRadarGraphic : MaskableGraphic
    {
        const int GRID_LEVELS = 4;
        const float GRID_LINE_WIDTH = 1.5f;
        const float OUTLINE_WIDTH = 3f;

        public Color GridColor { get; set; } = new Color(0.88f, 0.9f, 0.93f, 1f);
        public Color FillColor { get; set; } = new Color(0.4f, 0.49f, 0.9f, 0.18f);
        public Color OutlineColor { get; set; } = new Color(0.345f, 0.435f, 0.867f, 1f);

        float[] _values = Array.Empty<float>();
        int _dimensionCount = 7;

        public void SetValues(float[] values)
        {
            _values = values;
            _dimensionCount = Math.Max(3, values.Length);
            SetVerticesDirty();
        }
        public void SetDimensionCount(int count)
        {
            _dimensionCount = Math.Max(3, count);
            SetVerticesDirty();
        }
        /// <summary>
        /// 第 <paramref name="index"/> 个维度在半径为 1 时的方向，从正上方开始顺时针
        /// </summary>
        public Vector2 GetDirection(int index)
        {
            var angle = (90f - 360f * index / _dimensionCount) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        public float Radius
        {
            get
            {
                var rect = rectTransform.rect;
                return Mathf.Min(rect.width, rect.height) * 0.5f;
            }
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var center = rectTransform.rect.center;
            var radius = Radius;
            for (var level = 1; level <= GRID_LEVELS; level++)
            {
                var r = radius * level / GRID_LEVELS;
                for (var i = 0; i < _dimensionCount; i++)
                {
                    AddLine(vh, center + GetDirection(i) * r, center + GetDirection((i + 1) % _dimensionCount) * r, GRID_LINE_WIDTH, GridColor);
                }
            }
            for (var i = 0; i < _dimensionCount; i++)
            {
                AddLine(vh, center, center + GetDirection(i) * radius, GRID_LINE_WIDTH, GridColor);
            }
            if (_values.Length < 3)
            {
                return;
            }
            var count = _values.Length;
            var points = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                points[i] = center + GetDirection(i) * radius * Mathf.Clamp01(_values[i]);
            }
            // 以中心为扇心的三角扇填充
            var centerIndex = vh.currentVertCount;
            vh.AddVert(center, FillColor, Vector4.zero);
            for (var i = 0; i < count; i++)
            {
                vh.AddVert(points[i], FillColor, Vector4.zero);
            }
            for (var i = 0; i < count; i++)
            {
                vh.AddTriangle(centerIndex, centerIndex + 1 + i, centerIndex + 1 + (i + 1) % count);
            }
            for (var i = 0; i < count; i++)
            {
                AddLine(vh, points[i], points[(i + 1) % count], OUTLINE_WIDTH, OutlineColor);
            }
        }
        static void AddLine(VertexHelper vh, Vector2 from, Vector2 to, float width, Color color)
        {
            var direction = to - from;
            if (direction.sqrMagnitude < 1e-6f)
            {
                return;
            }
            var normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
            var index = vh.currentVertCount;
            vh.AddVert(from - normal, color, Vector4.zero);
            vh.AddVert(from + normal, color, Vector4.zero);
            vh.AddVert(to + normal, color, Vector4.zero);
            vh.AddVert(to - normal, color, Vector4.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
