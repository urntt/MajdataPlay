using UnityEngine;
#nullable enable
namespace MajdataPlay.Scenes.Game.Lane
{
    /// <summary>
    /// 下落式模式在运行时创建的简单图形（轨道线、判定线、Slide 路径）
    /// </summary>
    internal static class LaneVisuals
    {
        const string BACKGROUND_SORTING_LAYER = "Outline";
        const string SLIDE_PATH_SORTING_LAYER = "Slides";

        static Sprite? _whiteSprite;

        /// <summary>
        /// 1×1 世界单位的白色方块，用缩放和颜色绘制线条
        /// </summary>
        public static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite == null)
                {
                    var texture = Texture2D.whiteTexture;
                    _whiteSprite = Sprite.Create(texture,
                                                 new Rect(0, 0, texture.width, texture.height),
                                                 new Vector2(0.5f, 0.5f),
                                                 texture.width);
                    _whiteSprite.name = "LaneWhite";
                }
                return _whiteSprite;
            }
        }
        public static int BackgroundSortingLayerId => SortingLayer.NameToID(BACKGROUND_SORTING_LAYER);
        public static int SlidePathSortingLayerId => SortingLayer.NameToID(SLIDE_PATH_SORTING_LAYER);

        public static SpriteRenderer CreateQuad(string name, Transform parent, Material material, int sortingLayerId, int sortingOrder, Color color)
        {
            var obj = new GameObject(name);
            obj.layer = MajEnv.DEFAULT_LAYER;
            obj.transform.SetParent(parent, false);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = WhiteSprite;
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
            renderer.color = color;
            return renderer;
        }
        /// <summary>
        /// 把 <paramref name="renderer"/> 摆成从 <paramref name="from"/> 到 <paramref name="to"/>、宽 <paramref name="width"/> 的线段
        /// </summary>
        public static void SetSegment(SpriteRenderer renderer, Vector2 from, Vector2 to, float width)
        {
            var transform = renderer.transform;
            var delta = to - from;
            var length = delta.magnitude;
            transform.position = (from + to) / 2f;
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f);
            transform.localScale = new Vector3(width, length, 1f);
        }
        public static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (var i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }
        /// <summary>
        /// 创建轨道分隔线与判定线；垂直判定关闭时标出判定线附近的可按区域
        /// </summary>
        public static GameObject CreateBackground(Material material)
        {
            var root = new GameObject("LaneBackground");
            var parent = root.transform;
            var layerId = BackgroundSortingLayerId;
            var left = LaneLayout.LEFT;
            var right = LaneLayout.LEFT + LaneLayout.LANE_WIDTH * LaneLayout.LANE_COUNT;
            var bottom = LaneLayout.BOTTOM_Y;
            var top = LaneLayout.TOP_Y;

            var shade = CreateQuad("LaneShade", parent, material, layerId, -110, new Color(0f, 0f, 0f, 0.35f));
            shade.transform.position = new Vector3((left + right) / 2f, (bottom + top) / 2f, 0);
            shade.transform.localScale = new Vector3(right - left, top - bottom, 1f);

            if (!LaneMode.IsVerticalJudge)
            {
                var band = CreateQuad("JudgeBand", parent, material, layerId, -105, new Color(1f, 1f, 1f, 0.08f));
                var bandBottom = Mathf.Max(bottom, LaneLayout.JUDGE_LINE_Y - LaneLayout.JUDGE_BAND_HALF_HEIGHT);
                var bandTop = LaneLayout.JUDGE_LINE_Y + LaneLayout.JUDGE_BAND_HALF_HEIGHT;
                band.transform.position = new Vector3((left + right) / 2f, (bandBottom + bandTop) / 2f, 0);
                band.transform.localScale = new Vector3(right - left, bandTop - bandBottom, 1f);
            }
            for (var i = 0; i <= LaneLayout.LANE_COUNT; i++)
            {
                var x = left + i * LaneLayout.LANE_WIDTH;
                var divider = CreateQuad($"LaneDivider_{i}", parent, material, layerId, -100, new Color(1f, 1f, 1f, 0.25f));
                SetSegment(divider, new Vector2(x, bottom), new Vector2(x, top), 0.03f);
            }
            var judgeLine = CreateQuad("JudgeLine", parent, material, layerId, -90, new Color(1f, 1f, 1f, 0.9f));
            SetSegment(judgeLine, new Vector2(left, LaneLayout.JUDGE_LINE_Y), new Vector2(right, LaneLayout.JUDGE_LINE_Y), 0.06f);
            return root;
        }
    }
}
