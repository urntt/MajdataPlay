using MajdataPlay.Misc.Base;
using MajdataPlay.Scenes.Game;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
#nullable enable
namespace MajdataPlay
{
    /// <summary>
    /// 浅色风格界面（新设置、新结算等）共用的控件与配色：与列表式选歌一致，代码搭建，文字不带描边，圆角来自 <see cref="UiSprites"/>
    /// </summary>
    internal sealed class LightUi
    {
        public static readonly Color PAGE = Hex(0xF5F6F8);
        public static readonly Color PAPER = Color.white;
        public static readonly Color TEXT = Hex(0x202630);
        public static readonly Color MUTED = Hex(0x788391);
        public static readonly Color LINE = Hex(0xE4E7EC);
        public static readonly Color ACCENT = Hex(0x586FDD);
        public static readonly Color ACCENT_SOFT = Hex(0xEEF1FF);
        public static readonly Color FAVORITE = Hex(0xE0566F);
        public static readonly Color GOLD = Hex(0xC98A00);
        public static readonly Color ORANGE = Hex(0xE8680F);
        public static readonly Color YELLOW = Hex(0xB59F00);
        public static readonly Color BLUE = Hex(0x2F6FD6);
        public static readonly Color GREEN = Hex(0x1E9E52);
        public static readonly Color RED = Hex(0xD64545);

        public TMP_FontAsset? Font { get; }

        public LightUi(TMP_FontAsset? font)
        {
            Font = font;
        }

        #region Builders
        public static RectTransform CreateRect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return (RectTransform)obj.transform;
        }
        public static Image CreateImage(Transform parent, Color color, float radius)
        {
            var rect = CreateRect("Image", parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            UiSprites.ApplyRounded(image, radius);
            return image;
        }
        /// <summary>
        /// 圆角曲绘：外层是圆角底（未加载时显示为占位色）并作遮罩，返回里面放曲绘的 Image（加载前透明）
        /// </summary>
        public static Image CreateRoundedCover(Transform parent, float radius, out RectTransform frame)
        {
            var mask = CreateImage(parent, LINE, radius);
            mask.gameObject.name = "Cover";
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var cover = CreateImage(mask.transform, new Color(1f, 1f, 1f, 0f), 0f);
            cover.gameObject.name = "CoverImage";
            cover.preserveAspect = true;
            Stretch(cover.rectTransform);
            frame = mask.rectTransform;
            return cover;
        }
        public static void SetCover(Image image, Sprite? sprite)
        {
            if (image == null || sprite == null)
            {
                return;
            }
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
        }
        /// <summary>
        /// 文字框高度请留到字号的 1.5 倍左右，否则 TMP 会把放不下的整行省略掉
        /// </summary>
        public TextMeshProUGUI CreateText(Transform parent, string content, float fontSize, Color color, TextAlignmentOptions alignment, FontStyles style)
        {
            var rect = CreateRect("Text", parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null)
            {
                text.font = Font;
            }
            CleanTmpMaterial.Apply(text);
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }
        public Button CreateButton(Transform parent, string label, Color fill, Color textColor, float fontSize, UnityAction? onClick, float radius = 10f)
        {
            var background = CreateImage(parent, fill, radius);
            background.gameObject.name = "Button";
            background.raycastTarget = true;
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            SetButtonColors(button);
            if (onClick is not null)
            {
                button.onClick.AddListener(onClick);
            }
            var text = CreateText(background.transform, label, fontSize, textColor, TextAlignmentOptions.Center, FontStyles.Normal);
            Stretch(text.rectTransform, 4f);
            return button;
        }
        public static void SetButtonColors(Selectable selectable)
        {
            var colors = selectable.colors;
            colors.highlightedColor = new Color(0.97f, 0.97f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.86f, 0.88f, 0.96f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            selectable.colors = colors;
        }
        /// <summary>
        /// 徽章：颜色 14% 透明度的底与该颜色的粗体文字，宽度跟随文字（放在 HorizontalLayoutGroup 里）
        /// </summary>
        public void AddBadge(RectTransform parent, string text, Color color, float fontSize)
        {
            var badge = CreateImage(parent, WithAlpha(color, 0.14f), 8f);
            badge.gameObject.name = "Badge";
            var label = CreateText(badge.transform, text, fontSize, color, TextAlignmentOptions.Center, FontStyles.Bold);
            label.overflowMode = TextOverflowModes.Overflow;
            Stretch(label.rectTransform);
            var element = badge.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = label.GetPreferredValues(text, float.PositiveInfinity, float.PositiveInfinity).x + fontSize;
            element.preferredHeight = fontSize * 1.7f;
        }
        /// <summary>
        /// 宽高由所在的 HorizontalLayoutGroup 按文字的首选尺寸决定
        /// </summary>
        public TextMeshProUGUI AddFittedText(RectTransform parent, string text, float fontSize, Color color, FontStyles style)
        {
            var label = CreateText(parent, text, fontSize, color, TextAlignmentOptions.MidlineLeft, style);
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }
        public static RectTransform CreateRow(RectTransform parent, TextAnchor alignment, float spacing)
        {
            var row = CreateRect("Row", parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }
        /// <summary>
        /// 竖向滚动区域，返回内容节点（宽度跟随视口）
        /// </summary>
        public static RectTransform CreateScroll(Transform parent, float left, float top, float right, float bottom, out ScrollRect scrollRect)
        {
            var scroll = CreateRect("Scroll", parent);
            scroll.anchorMin = Vector2.zero;
            scroll.anchorMax = Vector2.one;
            scroll.offsetMin = new Vector2(left, bottom);
            scroll.offsetMax = new Vector2(-right, -top);
            scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 30f;
            var viewport = CreateRect("Viewport", scroll);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            // 透明背景接收拖动
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            return content;
        }
        /// <summary>
        /// 列表右侧可拖动的竖向滚动条，内容放得下时自动隐藏
        /// </summary>
        public static void AddScrollbar(ScrollRect scrollRect, float width)
        {
            var bar = CreateRect("Scrollbar", scrollRect.transform);
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 0.5f);
            bar.offsetMin = new Vector2(-width, 8f);
            bar.offsetMax = new Vector2(0f, -4f);
            var track = bar.gameObject.AddComponent<Image>();
            UiSprites.ApplyRounded(track, width * 0.5f);
            track.color = WithAlpha(LINE, 0.7f);
            var slidingArea = CreateRect("SlidingArea", bar);
            Stretch(slidingArea);
            var handle = CreateImage(slidingArea, WithAlpha(MUTED, 0.65f), width * 0.5f);
            handle.raycastTarget = true;
            Stretch(handle.rectTransform);
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            SetButtonColors(scrollbar);
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }
        /// <summary>
        /// 左栏顶部的标题栏：圆角应用图标、大字标题、小字副标题；返回标题栏占用的高度
        /// </summary>
        public float CreateSidebarHeader(RectTransform parent, string title, string subtitle)
        {
            const float ICON = 64f;
            const float LEFT = 26f;
            const float TOP = 24f;
            var icon = CreateRoundedCover(parent, 15f, out var iconFrame);
            PlaceTopLeft(iconFrame, LEFT, TOP, ICON, ICON);
            var iconSprite = AppIcon;
            if (iconSprite != null)
            {
                SetCover(icon, iconSprite);
            }
            else
            {
                iconFrame.GetComponent<Image>().color = ACCENT;
            }
            var titleText = CreateText(parent, title, 26f, TEXT, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            PlaceRow(titleText.rectTransform, LEFT + ICON + 16f, TOP - 2f, 16f, 40f);
            var subtitleText = CreateText(parent, subtitle, 16f, MUTED, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            PlaceRow(subtitleText.rectTransform, LEFT + ICON + 16f, TOP + 38f, 16f, 26f);
            var line = CreateImage(parent, LINE, 0f);
            PlaceRow(line.rectTransform, 20f, TOP + ICON + 20f, 20f, 1f);
            return TOP + ICON + 21f;
        }
        #endregion

        #region Placement
        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
        /// <summary>
        /// 以父节点左上角为原点摆放固定大小的矩形
        /// </summary>
        public static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
        /// <summary>
        /// 以父节点右上角为原点摆放固定大小的矩形（<paramref name="x"/> 为距右边的距离）
        /// </summary>
        public static void PlaceTopRight(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
        /// <summary>
        /// 以父节点右下角为原点摆放固定大小的矩形
        /// </summary>
        public static void PlaceBottomRight(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
        public static void PlaceMiddleRight(RectTransform rect, float x, float width, float height)
        {
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-x, 0f);
            rect.sizeDelta = new Vector2(width, height);
        }
        /// <summary>
        /// 横向撑满父节点（左右留边）、距顶部 <paramref name="top"/>、高 <paramref name="height"/> 的一行
        /// </summary>
        public static void PlaceRow(RectTransform rect, float left, float top, float right, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }
        public static void ClearChildren(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                child.SetParent(null, false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
        #endregion

        #region Data helpers
        public static Color GetDifficultyColor(ChartLevel level)
        {
            return level switch
            {
                ChartLevel.Easy => Hex(0x0E9BEE),
                ChartLevel.Basic => Hex(0x22BB5B),
                ChartLevel.Advance => Hex(0xF08A12),
                ChartLevel.Expert => Hex(0xF64861),
                ChartLevel.Master => Hex(0x9E45E2),
                ChartLevel.ReMaster => Hex(0xB15CF0),
                _ => Hex(0xE8379C),
            };
        }
        public static string GetGrade(double achievement)
        {
            return achievement switch
            {
                >= 100.5 => "SSS+",
                >= 100 => "SSS",
                >= 99.5 => "SS+",
                >= 99 => "SS",
                >= 98 => "S+",
                >= 97 => "S",
                >= 94 => "AAA",
                >= 90 => "AA",
                >= 80 => "A",
                >= 75 => "BBB",
                >= 70 => "BB",
                >= 60 => "B",
                >= 50 => "C",
                _ => "D",
            };
        }
        /// <summary>
        /// 评级颜色：SSS 段金色，SS 段橙色，S 段黄色，A 段蓝色，其余灰色
        /// </summary>
        public static Color GetGradeColor(string grade)
        {
            return grade switch
            {
                "SSS+" or "SSS" => GOLD,
                "SS+" or "SS" => ORANGE,
                "S+" or "S" => YELLOW,
                "AAA" or "AA" or "A" => BLUE,
                _ => MUTED,
            };
        }
        public static string GetComboText(ComboState state)
        {
            return state switch
            {
                ComboState.APPlus => "AP+",
                ComboState.AP => "AP",
                ComboState.FCPlus => "FC+",
                ComboState.FC => "FC",
                _ => string.Empty,
            };
        }
        /// <summary>
        /// AP 金色，FC 绿色
        /// </summary>
        public static Color GetComboColor(ComboState state)
        {
            return state is ComboState.APPlus or ComboState.AP ? GOLD : GREEN;
        }
        public static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }
        public static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
        #endregion

        #region Environment
        static Sprite? _appIcon;
        static bool _isAppIconLoaded;
        /// <summary>
        /// 应用图标（Resources/UI/AppIcon）；读不到时为空
        /// </summary>
        public static Sprite? AppIcon
        {
            get
            {
                if (!_isAppIconLoaded || _appIcon == null)
                {
                    _isAppIconLoaded = true;
                    _appIcon = Resources.Load<Sprite>("UI/AppIcon");
                }
                return _appIcon;
            }
        }
        /// <summary>
        /// 使用当前场景里文字的字体（含中日文），找不到时用 TMP 的默认字体
        /// </summary>
        public static TMP_FontAsset? FindFont()
        {
            var text = UnityEngine.Object.FindFirstObjectByType<TMP_Text>();
            if (text != null && text.font != null)
            {
                return text.font;
            }
            return TMP_Settings.defaultFontAsset;
        }
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }
            var obj = new GameObject("LightUiEventSystem", typeof(EventSystem));
            var module = obj.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
        /// <summary>
        /// 新建全屏 Overlay 画布（1920×1080 参考分辨率），带 CanvasGroup 用于跟随转场淡入淡出
        /// </summary>
        public static RectTransform CreateOverlayCanvas(Transform parent, string name, int sortingOrder, out CanvasGroup group)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            group = canvasObject.AddComponent<CanvasGroup>();
            return (RectTransform)canvasObject.transform;
        }
        /// <summary>
        /// 跟着游戏转场淡入淡出（转场画在 GlobalCanvas 上，被 Overlay 画布挡住，只能这样配合）；转场进行中或正在离开时不接受点击
        /// </summary>
        public static void FollowTransition(CanvasGroup group, bool isLeaving)
        {
            var progress = SceneSwitcher.TransitionProgress;
            group.alpha = 1f - progress;
            group.blocksRaycasts = !isLeaving && progress < 0.05f;
        }
        #endregion
    }
    /// <summary>
    /// 停止渲染某个场景自己的画布（新界面打开期间），之后可恢复；只关 Canvas 组件，对象和脚本照常运行
    /// </summary>
    internal sealed class SceneCanvasHider
    {
        readonly List<Canvas> _hidden = new();

        public void Hide(GameObject owner)
        {
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.gameObject.scene != owner.scene || canvas.transform.IsChildOf(owner.transform) || !canvas.enabled)
                {
                    continue;
                }
                canvas.enabled = false;
                _hidden.Add(canvas);
            }
        }
        public void Restore()
        {
            foreach (var canvas in _hidden)
            {
                if (canvas != null)
                {
                    canvas.enabled = true;
                }
            }
            _hidden.Clear();
        }
    }
}
