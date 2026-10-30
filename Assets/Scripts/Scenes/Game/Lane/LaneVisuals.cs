using UnityEngine;
#nullable enable
namespace MajdataPlay.Scenes.Game.Lane
{
    /// <summary>
    /// 下落式模式在运行时创建的简单图形（轨道线、判定线、Slide 箭头）
    /// </summary>
    internal static class LaneVisuals
    {
        const string BACKGROUND_SORTING_LAYER = "Outline";
        const string SLIDE_PATH_SORTING_LAYER = "Slides";

        /// <summary>
        /// 横贯整个屏幕的线条与底色使用的长度，任何宽高比都能盖满
        /// </summary>
        const float FAR = 50f;
        const int FADE_TEXTURE_WIDTH = 128;

        static Sprite? _whiteSprite;
        static Sprite? _laneShadeSprite;

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
        /// <summary>
        /// 轨道底色：横向宽 1 世界单位，中间为轨道区，两端各向外渐隐一条轨道宽
        /// </summary>
        static Sprite LaneShadeSprite
        {
            get
            {
                if (_laneShadeSprite == null)
                {
                    var featherRatio = LaneLayout.LANE_WIDTH / LaneShadeWidth;
                    var texture = new Texture2D(FADE_TEXTURE_WIDTH, 1, TextureFormat.RGBA32, false)
                    {
                        name = "LaneShade",
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear
                    };
                    var pixels = new Color32[FADE_TEXTURE_WIDTH];
                    for (var i = 0; i < pixels.Length; i++)
                    {
                        var u = (i + 0.5f) / FADE_TEXTURE_WIDTH;
                        var alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(u, 1f - u) / featherRatio));
                        pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                    }
                    texture.SetPixels32(pixels);
                    texture.Apply(false, true);
                    // FullRect：避免两端透明的像素被裁掉
                    _laneShadeSprite = Sprite.Create(texture,
                                                     new Rect(0, 0, FADE_TEXTURE_WIDTH, 1),
                                                     new Vector2(0.5f, 0.5f),
                                                     FADE_TEXTURE_WIDTH,
                                                     0,
                                                     SpriteMeshType.FullRect);
                    _laneShadeSprite.name = "LaneShade";
                }
                return _laneShadeSprite;
            }
        }
        static float LaneShadeWidth => LaneLayout.LANE_AREA_WIDTH + LaneLayout.LANE_WIDTH * 2f;
        /// <summary>
        /// 当前游戏的判定线，颜色由 LaneHud 按设置更新；离开游戏场景后随场景销毁
        /// </summary>
        public static SpriteRenderer? JudgeLine { get; private set; }
        /// <summary>
        /// Break 转 Flick、星星尾转 Flick 的染色
        /// </summary>
        public static readonly Color FlickColor = new Color(1f, 0.22f, 0.22f);
        public static int BackgroundSortingLayerId => SortingLayer.NameToID(BACKGROUND_SORTING_LAYER);
        public static int SlidePathSortingLayerId => SortingLayer.NameToID(SLIDE_PATH_SORTING_LAYER);

        public static SpriteRenderer CreateQuad(string name, Transform parent, Material material, int sortingLayerId, int sortingOrder, Color color)
        {
            var renderer = CreateSprite(name, parent, WhiteSprite, material, sortingLayerId, sortingOrder);
            renderer.color = color;
            return renderer;
        }
        public static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, Material material, int sortingLayerId, int sortingOrder)
        {
            var obj = new GameObject(name);
            obj.layer = MajEnv.DEFAULT_LAYER;
            obj.transform.SetParent(parent, false);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
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
        /// 创建轨道底色、分隔线与横贯屏幕的判定线；垂直判定关闭时标出判定线附近的可按区域
        /// </summary>
        public static GameObject CreateBackground(Material material)
        {
            var root = new GameObject("LaneBackground");
            var parent = root.transform;
            var layerId = BackgroundSortingLayerId;
            var left = LaneLayout.LEFT;
            var right = LaneLayout.RIGHT;
            var bottom = LaneLayout.BOTTOM_Y;

            // 底色与分隔线上下贯穿屏幕，屏幕较窄、相机拉远时也不会露出边缘
            var shade = CreateSprite("LaneShade", parent, LaneShadeSprite, material, layerId, -110);
            shade.color = new Color(0f, 0f, 0f, 0.35f);
            shade.transform.position = new Vector3((left + right) / 2f, 0, 0);
            // 贴图高 1 像素，即 1 / FADE_TEXTURE_WIDTH 世界单位
            shade.transform.localScale = new Vector3(LaneShadeWidth, FAR * 2f * FADE_TEXTURE_WIDTH, 1f);

            if (!LaneMode.IsVerticalJudge)
            {
                var band = CreateQuad("JudgeBand", parent, material, layerId, -105, new Color(1f, 1f, 1f, 0.08f));
                var bandBottom = Mathf.Max(bottom, LaneLayout.JUDGE_LINE_Y - LaneLayout.JUDGE_BAND_HALF_HEIGHT);
                var bandTop = LaneLayout.JUDGE_LINE_Y + LaneLayout.JUDGE_BAND_HALF_HEIGHT;
                band.transform.position = new Vector3((left + right) / 2f, (bandBottom + bandTop) / 2f, 0);
                band.transform.localScale = new Vector3(right - left, bandTop - bandBottom, 1f);
            }
            for (var i = 0; LaneMode.IsLaneDividerVisible && i <= LaneLayout.LANE_COUNT; i++)
            {
                var x = left + i * LaneLayout.LANE_WIDTH;
                var divider = CreateQuad($"LaneDivider_{i}", parent, material, layerId, -100, new Color(1f, 1f, 1f, 0.25f));
                SetSegment(divider, new Vector2(x, -FAR), new Vector2(x, FAR), 0.03f);
            }
            var judgeLine = CreateQuad("JudgeLine", parent, material, layerId, -90, new Color(1f, 1f, 1f, 0.9f));
            SetSegment(judgeLine, new Vector2(-FAR, LaneLayout.JUDGE_LINE_Y), new Vector2(FAR, LaneLayout.JUDGE_LINE_Y), 0.06f);
            JudgeLine = judgeLine;
            return root;
        }
    }
}
