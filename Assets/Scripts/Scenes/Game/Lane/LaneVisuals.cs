using System;
using UnityEngine;
using NoteLayer = MajdataPlay.Scenes.Game.Utils.NoteHelper.Sorting.NoteLayer;
using Sorting = MajdataPlay.Scenes.Game.Utils.NoteHelper.Sorting;
#nullable enable
namespace MajdataPlay.Scenes.Game.Lane
{
    /// <summary>
    /// 下落式模式在运行时创建的简单图形（轨道线、判定线、Slide 箭头）
    /// </summary>
    internal static class LaneVisuals
    {
        /// <summary>
        /// 音符按深度（z）排序，越小越靠前。音符根节点 Notes 在世界 z = 100，圆形判定圈 Outline 在 108
        /// </summary>
        const float DEFAULT_NOTES_DEPTH = 100f;
        /// <summary>
        /// 轨道底色等放在判定圈之后：与原先画在 Outline 层、排序在判定圈之下一致
        /// </summary>
        const float BACKGROUND_DEPTH_BEHIND_OUTLINE = 1f;
        const float DEFAULT_OUTLINE_DEPTH = 108f;

        /// <summary>
        /// 横贯整个屏幕的线条与底色使用的长度，任何宽高比都能盖满
        /// </summary>
        const float FAR = 50f;
        const int FADE_TEXTURE_WIDTH = 128;

        static Sprite? _whiteSprite;
        static Sprite? _laneShadeSprite;
        static Sprite? _laneHighlightSprite;

        /// <summary>
        /// 圆形模式“外圈”遮罩（NoteMaskA，按灰度取透明度、截断 0.35）从这个半径起遮住音符，直到判定圈以外
        /// </summary>
        const float OUTER_MASK_CUTOFF_DISTANCE = 2.55f;
        /// <summary>
        /// 圆形模式“内圈”遮罩（NoteMaskB）遮住这个半径以内的音符
        /// </summary>
        const float INNER_MASK_CUTOFF_DISTANCE = 2.87f;
        /// <summary>
        /// Shows the Mod note mask in falling mode as a rectangle across the screen that covers the same stretch of the notes'
        /// path as the circle mask does: "Outer" hides notes from the matching height down to below the judge line, so they
        /// vanish before arriving; "Inner" hides them from the top of the lanes down to the matching height, so they appear late.
        /// </summary>
        /// <param name="mask">The sprite mask of the game scene; notes are visible outside it.</param>
        /// <param name="option">The NoteMask setting: "Outer", "Inner" or "Disable".</param>
        public static void ConfigureNoteMask(SpriteMask mask, string option)
        {
            float bottom;
            float top;
            switch (option)
            {
                case "Outer":
                    bottom = LaneLayout.BOTTOM_Y - FAR;
                    top = LaneLayout.GetY(OUTER_MASK_CUTOFF_DISTANCE);
                    break;
                case "Inner":
                    bottom = LaneLayout.GetY(INNER_MASK_CUTOFF_DISTANCE);
                    top = LaneLayout.TOP_Y + FAR;
                    break;
                default:
                    mask.gameObject.SetActive(false);
                    return;
            }
            mask.gameObject.SetActive(true);
            mask.sprite = WhiteSprite;
            var maskTransform = mask.transform;
            var parentScale = maskTransform.parent != null ? maskTransform.parent.lossyScale : Vector3.one;
            maskTransform.SetPositionAndRotation(new Vector3(0f, (bottom + top) * 0.5f, maskTransform.position.z), Quaternion.identity);
            maskTransform.localScale = new Vector3(FAR * 2f / SafeScale(parentScale.x), (top - bottom) / SafeScale(parentScale.y), 1f);
        }
        /// <summary>
        /// Avoids dividing by a zero scale.
        /// </summary>
        /// <param name="scale">A scale component.</param>
        /// <returns>The scale, or 1 when it is zero.</returns>
        static float SafeScale(float scale)
        {
            return Mathf.Abs(scale) < 1e-5f ? 1f : scale;
        }
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
                    var featherRatio = LaneLayout.LaneWidth / LaneShadeWidth;
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
        static float LaneShadeWidth => LaneLayout.LaneAreaWidth + LaneLayout.LaneWidth * 2f;
        /// <summary>
        /// 点亮手指所在列的光柱：自下而上渐隐的竖向渐变
        /// </summary>
        static Sprite LaneHighlightSprite
        {
            get
            {
                if (_laneHighlightSprite == null)
                {
                    var texture = new Texture2D(1, FADE_TEXTURE_WIDTH, TextureFormat.RGBA32, false)
                    {
                        name = "LaneHighlight",
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear
                    };
                    var pixels = new Color32[FADE_TEXTURE_WIDTH];
                    for (var i = 0; i < FADE_TEXTURE_WIDTH; i++)
                    {
                        var v = (i + 0.5f) / FADE_TEXTURE_WIDTH;
                        var alpha = (1f - v) * (1f - v);
                        pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                    }
                    texture.SetPixels32(pixels);
                    texture.Apply(false, true);
                    // 底边在判定线上：轴心放在底部
                    _laneHighlightSprite = Sprite.Create(texture,
                                                         new Rect(0, 0, 1, FADE_TEXTURE_WIDTH),
                                                         new Vector2(0.5f, 0f),
                                                         1,
                                                         0,
                                                         SpriteMeshType.FullRect);
                    _laneHighlightSprite.name = "LaneHighlight";
                }
                return _laneHighlightSprite;
            }
        }
        /// <summary>
        /// 当前游戏的点亮手指所在列的光柱，按列（1~8）存放；未开启时为空。离开游戏场景后随场景销毁
        /// </summary>
        public static SpriteRenderer[] LaneHighlights { get; private set; } = Array.Empty<SpriteRenderer>();
        /// <summary>
        /// 当前游戏的判定线，颜色由 LaneHud 按设置更新；离开游戏场景后随场景销毁
        /// </summary>
        public static SpriteRenderer? JudgeLine { get; private set; }
        /// <summary>
        /// Slide 样式为悬空式时，判定线上方给星星箭头用的第二条判定线；颜色随判定线变化但更浅。其他样式为 null
        /// </summary>
        public static SpriteRenderer? SlideJudgeLine { get; private set; }
        /// <summary>
        /// 第二条判定线相对判定线的不透明度
        /// </summary>
        public const float SLIDE_JUDGE_LINE_ALPHA = 0.45f;
        /// <summary>
        /// Break 转 Flick、星星尾转 Flick 的染色。星星用黄色的 Each 贴图来染：默认的星星是青色，染红后会发暗
        /// </summary>
        public static readonly Color FlickColor = new Color(1f, 0.22f, 0.22f);
        /// <summary>
        /// 当前游戏音符根节点的世界深度，由 <see cref="CreateBackground"/> 记录
        /// </summary>
        public static float NotesDepth { get; private set; } = DEFAULT_NOTES_DEPTH;
        /// <summary>
        /// Slide 箭头与音符的 Slide 同组（在所有 Tap、Hold、星星头之后）
        /// </summary>
        public static float GetSlidePathDepth(int sortOrder) => NotesDepth + Sorting.GetZIndex(NoteLayer.Slides, sortOrder);
        /// <summary>
        /// 星星尾转 Flick 的标记画在所有 Tap、Hold、星星头之上（TouchHold 边框组），与它们叠在一起时也能看清
        /// </summary>
        public static float GetFlickMarkerDepth(int sortOrder) => NotesDepth + Sorting.GetZIndex(NoteLayer.TouchHoldBorder, sortOrder);

        public static SpriteRenderer CreateQuad(string name, Transform parent, Material material, float depth, Color color)
        {
            var renderer = CreateSprite(name, parent, WhiteSprite, material, depth);
            renderer.color = color;
            return renderer;
        }
        /// <summary>
        /// 创建一个精灵：与音符一样放在 Default 排序层、顺序为 0，前后由世界深度 <paramref name="depth"/> 决定（越小越靠前）
        /// </summary>
        public static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, Material material, float depth)
        {
            var obj = new GameObject(name);
            obj.layer = MajEnv.DEFAULT_LAYER;
            obj.transform.SetParent(parent, false);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = 0;
            renderer.sortingOrder = 0;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            SetDepth(obj.transform, depth);
            return renderer;
        }
        /// <summary>
        /// 只改世界深度，不动 X、Y
        /// </summary>
        public static void SetDepth(Transform transform, float depth)
        {
            var position = transform.position;
            position.z = depth;
            transform.position = position;
        }
        /// <summary>
        /// 设置 X、Y，保留世界深度
        /// </summary>
        static void SetPositionKeepDepth(Transform transform, Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }
        /// <summary>
        /// 把 <paramref name="renderer"/> 摆成从 <paramref name="from"/> 到 <paramref name="to"/>、宽 <paramref name="width"/> 的线段
        /// </summary>
        public static void SetSegment(SpriteRenderer renderer, Vector2 from, Vector2 to, float width)
        {
            var transform = renderer.transform;
            var delta = to - from;
            var length = delta.magnitude;
            SetPositionKeepDepth(transform, (from + to) / 2f);
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
        /// <param name="notesDepth">音符根节点的世界深度</param>
        /// <param name="outlineDepth">圆形判定圈的世界深度，没有时为 null；轨道底色等都画在它后面</param>
        public static GameObject CreateBackground(Material material, float notesDepth, float? outlineDepth)
        {
            NotesDepth = notesDepth;
            var root = new GameObject("LaneBackground");
            var parent = root.transform;
            // 由远到近：底色、可按区域、光柱、分隔线、判定线
            var depth = (outlineDepth ?? notesDepth + (DEFAULT_OUTLINE_DEPTH - DEFAULT_NOTES_DEPTH)) + BACKGROUND_DEPTH_BEHIND_OUTLINE;
            var left = LaneLayout.Left;
            var right = LaneLayout.Right;
            var bottom = LaneLayout.BOTTOM_Y;

            // 底色与分隔线上下贯穿屏幕，屏幕较窄、相机拉远时也不会露出边缘
            var shade = CreateSprite("LaneShade", parent, LaneShadeSprite, material, depth + 0.5f);
            shade.color = new Color(0f, 0f, 0f, 0.35f);
            SetPositionKeepDepth(shade.transform, new Vector2((left + right) / 2f, 0));
            // 贴图高 1 像素，即 1 / FADE_TEXTURE_WIDTH 世界单位
            shade.transform.localScale = new Vector3(LaneShadeWidth, FAR * 2f * FADE_TEXTURE_WIDTH, 1f);

            if (!LaneMode.IsVerticalJudge)
            {
                var band = CreateQuad("JudgeBand", parent, material, depth + 0.4f, new Color(1f, 1f, 1f, 0.08f));
                var bandBottom = Mathf.Max(bottom, LaneLayout.JUDGE_LINE_Y - LaneLayout.JUDGE_BAND_HALF_HEIGHT);
                var bandTop = LaneLayout.JUDGE_LINE_Y + LaneLayout.JUDGE_BAND_HALF_HEIGHT;
                SetPositionKeepDepth(band.transform, new Vector2((left + right) / 2f, (bandBottom + bandTop) / 2f));
                band.transform.localScale = new Vector3(right - left, bandTop - bandBottom, 1f);
            }
            LaneHighlights = Array.Empty<SpriteRenderer>();
            if (LaneMode.IsLaneHighlightEnabled)
            {
                // 在底色之上、分隔线与音符之下；平时透明，由 LaneHud 按手指所在列点亮
                var highlights = new SpriteRenderer[LaneLayout.LANE_COUNT];
                for (var lane = 1; lane <= LaneLayout.LANE_COUNT; lane++)
                {
                    var highlight = CreateSprite($"LaneHighlight_{lane}", parent, LaneHighlightSprite, material, depth + 0.3f);
                    highlight.color = new Color(1f, 1f, 1f, 0f);
                    SetPositionKeepDepth(highlight.transform, new Vector2(LaneLayout.GetLaneX(lane), LaneLayout.JUDGE_LINE_Y));
                    // 贴图高 FADE_TEXTURE_WIDTH 像素、每像素 1 世界单位
                    highlight.transform.localScale = new Vector3(LaneLayout.LaneWidth, LaneLayout.TRAVEL_LENGTH * 0.6f / FADE_TEXTURE_WIDTH, 1f);
                    highlights[lane - 1] = highlight;
                }
                LaneHighlights = highlights;
            }
            for (var i = 0; LaneMode.IsLaneDividerVisible && i <= LaneLayout.LANE_COUNT; i++)
            {
                var x = left + i * LaneLayout.LaneWidth;
                var divider = CreateQuad($"LaneDivider_{i}", parent, material, depth + 0.2f, new Color(1f, 1f, 1f, 0.25f));
                SetSegment(divider, new Vector2(x, -FAR), new Vector2(x, FAR), 0.03f);
            }
            var judgeLine = CreateQuad("JudgeLine", parent, material, depth + 0.1f, new Color(1f, 1f, 1f, 0.9f));
            SetSegment(judgeLine, new Vector2(-FAR, LaneLayout.JUDGE_LINE_Y), new Vector2(FAR, LaneLayout.JUDGE_LINE_Y), 0.06f);
            JudgeLine = judgeLine;
            SlideJudgeLine = null;
            if (LaneMode.IsSlideFloating)
            {
                var slideLine = CreateQuad("SlideJudgeLine", parent, material, depth + 0.1f, new Color(1f, 1f, 1f, 0.9f * SLIDE_JUDGE_LINE_ALPHA));
                SetSegment(slideLine,
                           new Vector2(-FAR, LaneLayout.FLOAT_SLIDE_LINE_Y),
                           new Vector2(FAR, LaneLayout.FLOAT_SLIDE_LINE_Y),
                           0.05f);
                SlideJudgeLine = slideLine;
            }
            return root;
        }
    }
}
