using UnityEngine;
using UnityEngine.UI;
#nullable enable
namespace MajdataPlay
{
    /// <summary>
    /// 运行时生成的通用 UI 贴图：九宫格圆角矩形与抗锯齿圆形。整个程序只生成一份，供代码搭建的界面共用
    /// </summary>
    internal static class UiSprites
    {
        /// <summary>
        /// 圆角贴图的源半径（像素）；<see cref="ApplyRounded"/> 用 pixelsPerUnitMultiplier 换算成想要的半径
        /// </summary>
        const float ROUNDED_SOURCE_RADIUS = 24f;

        static Sprite? _rounded;
        static Sprite? _circle;
        static Sprite? _heartFilled;
        static Sprite? _heartOutline;

        /// <summary>
        /// 白色实心爱心（收藏）
        /// </summary>
        public static Sprite HeartFilled
        {
            get
            {
                if (_heartFilled == null)
                {
                    _heartFilled = CreateHeartSprite(true);
                }
                return _heartFilled;
            }
        }
        /// <summary>
        /// 白色空心爱心（未收藏）
        /// </summary>
        public static Sprite HeartOutline
        {
            get
            {
                if (_heartOutline == null)
                {
                    _heartOutline = CreateHeartSprite(false);
                }
                return _heartOutline;
            }
        }

        /// <summary>
        /// 白色圆角矩形，九宫格拉伸
        /// </summary>
        public static Sprite Rounded
        {
            get
            {
                if (_rounded == null)
                {
                    _rounded = CreateRoundedSprite();
                }
                return _rounded;
            }
        }
        /// <summary>
        /// 白色实心圆（128 像素，抗锯齿）
        /// </summary>
        public static Sprite Circle
        {
            get
            {
                if (_circle == null)
                {
                    _circle = CreateCircleSprite();
                }
                return _circle;
            }
        }
        /// <summary>
        /// 把 Image 变成半径为 <paramref name="radius"/> 的圆角矩形
        /// </summary>
        public static void ApplyRounded(Image image, float radius)
        {
            if (radius <= 0f)
            {
                return;
            }
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = ROUNDED_SOURCE_RADIUS / radius;
        }
        static Sprite CreateRoundedSprite()
        {
            const int SIZE = 64;
            const float RADIUS = ROUNDED_SOURCE_RADIUS;
            var texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false)
            {
                name = "UiRounded",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            var pixels = new Color32[SIZE * SIZE];
            for (var y = 0; y < SIZE; y++)
            {
                for (var x = 0; x < SIZE; x++)
                {
                    var cx = Mathf.Clamp(x + 0.5f, RADIUS, SIZE - RADIUS);
                    var cy = Mathf.Clamp(y + 0.5f, RADIUS, SIZE - RADIUS);
                    var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    var alpha = Mathf.Clamp01(RADIUS - distance + 0.5f);
                    pixels[y * SIZE + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                                       new Vector4(RADIUS, RADIUS, RADIUS, RADIUS));
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
        /// <summary>
        /// 心形线 (x² + y² − 1)³ − x²y³ ≤ 0，4×4 超采样抗锯齿；空心为外形减去缩小 76% 的内形
        /// </summary>
        static Sprite CreateHeartSprite(bool isFilled)
        {
            const int SIZE = 96;
            const int SAMPLES = 4;
            const float INNER_SCALE = 0.76f;
            const float CENTER_Y = 0.08f;
            var texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false)
            {
                name = isFilled ? "UiHeartFilled" : "UiHeartOutline",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            var pixels = new Color32[SIZE * SIZE];
            for (var y = 0; y < SIZE; y++)
            {
                for (var x = 0; x < SIZE; x++)
                {
                    var covered = 0;
                    for (var sy = 0; sy < SAMPLES; sy++)
                    {
                        for (var sx = 0; sx < SAMPLES; sx++)
                        {
                            var hx = (x + (sx + 0.5f) / SAMPLES) / SIZE * 2.7f - 1.35f;
                            var hy = (y + (sy + 0.5f) / SAMPLES) / SIZE * 2.7f - 1.25f;
                            if (!IsInHeart(hx, hy))
                            {
                                continue;
                            }
                            if (isFilled || !IsInHeart(hx / INNER_SCALE, (hy - CENTER_Y) / INNER_SCALE + CENTER_Y))
                            {
                                covered++;
                            }
                        }
                    }
                    pixels[y * SIZE + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * covered / (SAMPLES * SAMPLES)));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
        static bool IsInHeart(float x, float y)
        {
            var a = x * x + y * y - 1f;
            return a * a * a - x * x * y * y * y <= 0f;
        }
        static Sprite CreateCircleSprite()
        {
            const int SIZE = 128;
            var texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false)
            {
                name = "UiCircle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            var pixels = new Color32[SIZE * SIZE];
            var radius = SIZE / 2f;
            for (var y = 0; y < SIZE; y++)
            {
                for (var x = 0; x < SIZE; x++)
                {
                    var dx = x + 0.5f - radius;
                    var dy = y + 0.5f - radius;
                    var alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * SIZE + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
    }
}
