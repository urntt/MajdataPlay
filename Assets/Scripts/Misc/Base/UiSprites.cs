using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#nullable enable
namespace MajdataPlay
{
    /// <summary>
    /// 运行时生成的通用 UI 贴图（整个程序各生成一份，供代码搭建的界面共用）：
    /// 九宫格圆角矩形、等宽圆角描边、圆角外的“角帽”、柔和投影、圆形与爱心。
    /// 都由圆角矩形的距离场生成，开启 mipmap，缩放到任意大小都不出锯齿
    /// </summary>
    internal static class UiSprites
    {
        /// <summary>
        /// 九宫格贴图的源半径（像素）；用 pixelsPerUnitMultiplier = 源半径 / 目标半径 换算到想要的圆角
        /// </summary>
        const float SOURCE_RADIUS = 64f;
        /// <summary>
        /// 九宫格中间可拉伸部分的宽度（像素）
        /// </summary>
        const int CENTER = 4;

        static Sprite? _rounded;
        static Sprite? _inverseRounded;
        static Sprite? _circle;
        static Sprite? _heartFilled;
        static Sprite? _heartOutline;
        static Sprite? _magnifier;
        static Sprite? _share;
        static Sprite? _person;
        static readonly Dictionary<int, Sprite> _rings = new();
        static readonly Dictionary<int, Sprite> _shadows = new();

        /// <summary>
        /// 白色圆角矩形，九宫格拉伸
        /// </summary>
        public static Sprite Rounded => _rounded != null ? _rounded : (_rounded = CreateRoundedSprite(false));
        /// <summary>
        /// 圆角矩形以外的部分（四个角帽）：盖在方形图片上并设为背景色，得到抗锯齿的圆角，不需要遮罩
        /// </summary>
        public static Sprite InverseRounded => _inverseRounded != null ? _inverseRounded : (_inverseRounded = CreateRoundedSprite(true));
        /// <summary>
        /// 白色实心圆（256 像素，抗锯齿，带 mipmap）
        /// </summary>
        public static Sprite Circle => _circle != null ? _circle : (_circle = CreateCircleSprite());
        /// <summary>
        /// 白色实心爱心（收藏）
        /// </summary>
        public static Sprite HeartFilled => _heartFilled != null ? _heartFilled : (_heartFilled = CreateHeartSprite(true));
        /// <summary>
        /// 白色空心爱心（未收藏）
        /// </summary>
        public static Sprite HeartOutline => _heartOutline != null ? _heartOutline : (_heartOutline = CreateHeartSprite(false));
        /// <summary>
        /// Gets the white magnifier icon used by the search button.
        /// </summary>
        public static Sprite Magnifier => _magnifier != null ? _magnifier : (_magnifier = CreateIconSprite("UiMagnifier", MagnifierDistance));
        /// <summary>
        /// Gets the white share icon (an open box with an upward arrow).
        /// </summary>
        public static Sprite Share => _share != null ? _share : (_share = CreateIconSprite("UiShare", ShareDistance));
        /// <summary>
        /// Gets the white person silhouette used when the player has no avatar.
        /// </summary>
        public static Sprite Person => _person != null ? _person : (_person = CreateIconSprite("UiPerson", PersonDistance));

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
            image.pixelsPerUnitMultiplier = SOURCE_RADIUS / radius;
        }
        /// <summary>
        /// 圆角外的角帽（颜色设为背后的底色），盖在方形内容上得到圆角
        /// </summary>
        public static void ApplyInverseRounded(Image image, float radius)
        {
            image.sprite = InverseRounded;
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            image.pixelsPerUnitMultiplier = SOURCE_RADIUS / Mathf.Max(0.5f, radius);
        }
        /// <summary>
        /// 圆角描边：与同一矩形、同一半径的圆角底板对齐，直边与拐角一样粗
        /// </summary>
        public static void ApplyRing(Image image, float radius, float stroke)
        {
            radius = Mathf.Max(0.5f, radius);
            var ratio = Mathf.Clamp(stroke / radius, 0.01f, 0.95f);
            image.sprite = GetRing(ratio);
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            image.pixelsPerUnitMultiplier = SOURCE_RADIUS / radius;
        }
        /// <summary>
        /// 柔和投影：Image 的矩形应比目标四周各大 <paramref name="blur"/>，圆角与目标相同
        /// </summary>
        public static void ApplySoftShadow(Image image, float radius, float blur)
        {
            radius = Mathf.Max(1f, radius);
            var ratio = Mathf.Clamp(blur / radius, 0.1f, 3f);
            image.sprite = GetShadow(ratio);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = SOURCE_RADIUS / radius;
        }

        static Sprite GetRing(float ratio)
        {
            var key = Mathf.RoundToInt(ratio * 200f);
            if (_rings.TryGetValue(key, out var sprite) && sprite != null)
            {
                return sprite;
            }
            var stroke = key / 200f * SOURCE_RADIUS;
            sprite = CreateSlicedSprite($"UiRing{key}", 0f, distance =>
            {
                // 外缘到内缘之间：外圆角覆盖率减去内收 stroke 后的覆盖率
                return Coverage(distance) - Coverage(distance + stroke);
            });
            _rings[key] = sprite;
            return sprite;
        }
        static Sprite GetShadow(float ratio)
        {
            var key = Mathf.RoundToInt(ratio * 20f);
            if (_shadows.TryGetValue(key, out var sprite) && sprite != null)
            {
                return sprite;
            }
            var blur = key / 20f * SOURCE_RADIUS;
            // 形状本身内收 blur，外面一圈平滑衰减到 0
            sprite = CreateSlicedSprite($"UiShadow{key}", blur, distance =>
            {
                if (distance <= 0f)
                {
                    return 1f;
                }
                var t = distance / blur;
                return Mathf.Exp(-t * t * 4f);
            });
            _shadows[key] = sprite;
            return sprite;
        }
        static Sprite CreateRoundedSprite(bool isInverse)
        {
            return CreateSlicedSprite(isInverse ? "UiRoundedInverse" : "UiRounded", 0f,
                                      distance => isInverse ? 1f - Coverage(distance) : Coverage(distance));
        }
        /// <summary>
        /// 1 像素抗锯齿的覆盖率（距离为到圆角矩形边界的有向距离，内部为负）
        /// </summary>
        static float Coverage(float distance)
        {
            return Mathf.Clamp01(0.5f - distance);
        }
        /// <summary>
        /// 按圆角矩形（源半径 64，外侧留 <paramref name="padding"/>）的有向距离生成九宫格贴图，带 mipmap
        /// </summary>
        static Sprite CreateSlicedSprite(string name, float padding, System.Func<float, float> alphaFromDistance)
        {
            var pad = Mathf.CeilToInt(padding);
            var border = Mathf.CeilToInt(SOURCE_RADIUS) + pad;
            var size = border * 2 + CENTER;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            var pixels = new Color32[size * size];
            var half = size * 0.5f;
            var innerHalf = half - pad;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = RoundedRectDistance(x + 0.5f - half, y + 0.5f - half, innerHalf, SOURCE_RADIUS);
                    var alpha = Mathf.Clamp01(alphaFromDistance(distance));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                                       new Vector4(border, border, border, border));
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
        /// <summary>
        /// 点到中心在原点、半宽 <paramref name="halfSize"/>、圆角 <paramref name="radius"/> 的正方形圆角矩形边界的有向距离
        /// </summary>
        static float RoundedRectDistance(float px, float py, float halfSize, float radius)
        {
            var qx = Mathf.Abs(px) - (halfSize - radius);
            var qy = Mathf.Abs(py) - (halfSize - radius);
            var outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            var inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - radius;
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
        /// <summary>
        /// Rasterizes an icon from its signed distance field with one pixel of anti-aliasing and mipmaps.
        /// </summary>
        /// <param name="name">The texture name.</param>
        /// <param name="distance">
        /// The signed distance to the icon edge in icon units, where the icon spans -1 to 1 on both axes with y pointing up;
        /// negative inside the icon.
        /// </param>
        /// <returns>A white sprite whose alpha is the icon coverage.</returns>
        static Sprite CreateIconSprite(string name, System.Func<Vector2, float> distance)
        {
            const int SIZE = 128;
            const float PIXELS_PER_UNIT = SIZE * 0.5f;
            var texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            var pixels = new Color32[SIZE * SIZE];
            for (var y = 0; y < SIZE; y++)
            {
                for (var x = 0; x < SIZE; x++)
                {
                    var point = new Vector2((x + 0.5f) / PIXELS_PER_UNIT - 1f, (y + 0.5f) / PIXELS_PER_UNIT - 1f);
                    var alpha = Mathf.Clamp01(0.5f - distance(point) * PIXELS_PER_UNIT);
                    pixels[y * SIZE + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
        /// <summary>
        /// Gets the distance from a point to a line segment.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="start">The start of the segment.</param>
        /// <param name="end">The end of the segment.</param>
        /// <returns>The shortest distance between the point and the segment.</returns>
        static float SegmentDistance(Vector2 point, Vector2 start, Vector2 end)
        {
            var fromStart = point - start;
            var segment = end - start;
            var t = Mathf.Clamp01(Vector2.Dot(fromStart, segment) / Vector2.Dot(segment, segment));
            return (fromStart - segment * t).magnitude;
        }
        /// <summary>
        /// Gets the signed distance to the magnifier icon: a ring with a handle pointing to the bottom right.
        /// </summary>
        /// <param name="point">The point in icon units.</param>
        /// <returns>The signed distance in icon units.</returns>
        static float MagnifierDistance(Vector2 point)
        {
            const float RING_RADIUS = 0.46f;
            const float STROKE = 0.11f;
            var center = new Vector2(-0.14f, 0.14f);
            var ring = Mathf.Abs((point - center).magnitude - RING_RADIUS) - STROKE;
            var direction = new Vector2(0.70710678f, -0.70710678f);
            var handleStart = center + direction * (RING_RADIUS + STROKE * 0.5f);
            var handle = SegmentDistance(point, handleStart, new Vector2(0.74f, -0.74f)) - STROKE * 1.15f;
            return Mathf.Min(ring, handle);
        }
        /// <summary>
        /// Gets the signed distance to the share icon: an open-topped box with an arrow pointing up out of it.
        /// </summary>
        /// <param name="point">The point in icon units.</param>
        /// <returns>The signed distance in icon units.</returns>
        static float ShareDistance(Vector2 point)
        {
            const float STROKE = 0.085f;
            var box = Mathf.Min(
                Mathf.Min(SegmentDistance(point, new Vector2(-0.3f, 0.2f), new Vector2(-0.6f, 0.2f)),
                          SegmentDistance(point, new Vector2(-0.6f, 0.2f), new Vector2(-0.6f, -0.8f))),
                Mathf.Min(SegmentDistance(point, new Vector2(-0.6f, -0.8f), new Vector2(0.6f, -0.8f)),
                          Mathf.Min(SegmentDistance(point, new Vector2(0.6f, -0.8f), new Vector2(0.6f, 0.2f)),
                                    SegmentDistance(point, new Vector2(0.6f, 0.2f), new Vector2(0.3f, 0.2f)))));
            var tip = new Vector2(0f, 0.86f);
            var arrow = Mathf.Min(
                SegmentDistance(point, new Vector2(0f, -0.3f), tip),
                Mathf.Min(SegmentDistance(point, tip, new Vector2(-0.3f, 0.56f)),
                          SegmentDistance(point, tip, new Vector2(0.3f, 0.56f))));
            return Mathf.Min(box, arrow) - STROKE;
        }
        /// <summary>
        /// Gets the signed distance to the person silhouette: a round head above rounded shoulders.
        /// </summary>
        /// <param name="point">The point in icon units.</param>
        /// <returns>The signed distance in icon units.</returns>
        static float PersonDistance(Vector2 point)
        {
            var head = (point - new Vector2(0f, 0.3f)).magnitude - 0.32f;
            var shoulders = (point - new Vector2(0f, -1.08f)).magnitude - 0.8f;
            return Mathf.Min(head, shoulders);
        }
        static Sprite CreateCircleSprite()
        {
            const int SIZE = 256;
            var texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, true)
            {
                name = "UiCircle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
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
                    var alpha = Mathf.Clamp01(radius - 0.5f - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * SIZE + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
    }
}
