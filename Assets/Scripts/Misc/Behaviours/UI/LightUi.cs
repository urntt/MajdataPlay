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
        public const string REPOSITORY_URL = "https://github.com/urntt/MajdataPlay";
        static readonly Color COVER_OUTLINE = new Color(0f, 0f, 0f, 0.12f);

        /// <summary>
        /// 圆角曲绘：圆角占位底、方形曲绘、盖在四角的“角帽”（背后底色 <paramref name="backgroundColor"/>）和一圈描边。
        /// 不用遮罩，圆角边缘抗锯齿；返回放曲绘的 Image（加载前透明），<paramref name="frame"/> 用来摆放
        /// </summary>
        public static Image CreateRoundedCover(Transform parent, float radius, Color backgroundColor, out RectTransform frame)
        {
            frame = CreateRect("Cover", parent);
            var placeholder = CreateImage(frame, LINE, radius);
            placeholder.gameObject.name = "Placeholder";
            Stretch(placeholder.rectTransform);
            var cover = CreateImage(frame, new Color(1f, 1f, 1f, 0f), 0f);
            cover.gameObject.name = "CoverImage";
            cover.preserveAspect = true;
            Stretch(cover.rectTransform);
            var caps = CreateImage(frame, backgroundColor, 0f);
            caps.gameObject.name = "Corners";
            UiSprites.ApplyInverseRounded(caps, radius);
            Stretch(caps.rectTransform);
            AddRing(frame, COVER_OUTLINE, radius, 2f);
            return cover;
        }
        /// <summary>
        /// 圆角描边：铺满父节点（不参与布局），与父节点同一圆角，直边与拐角一样粗
        /// </summary>
        public static Image AddRing(Transform parent, Color color, float radius, float stroke)
        {
            var rect = CreateRect("Ring", parent);
            var ring = rect.gameObject.AddComponent<Image>();
            ring.color = color;
            ring.raycastTarget = false;
            UiSprites.ApplyRing(ring, radius, stroke);
            Stretch(rect);
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return ring;
        }
        /// <summary>
        /// 带柔和投影的容器：投影与容器同一圆角、中心对齐、四周等宽外扩 <paramref name="blur"/>；按钮等放进容器里铺满
        /// </summary>
        public static RectTransform CreateShadowHolder(Transform parent, float radius, float blur, float opacity = 0.16f)
        {
            var holder = CreateRect("ShadowHolder", parent);
            var shadow = CreateImage(holder, new Color(0.1f, 0.13f, 0.25f, opacity), 0f);
            shadow.gameObject.name = "Shadow";
            UiSprites.ApplySoftShadow(shadow, radius, blur);
            Stretch(shadow.rectTransform, -blur);
            return holder;
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
        /// <summary>
        /// The opacity of an icon button and its icon while the button cannot be pressed.
        /// </summary>
        const float DISABLED_ICON_ALPHA = 0.4f;
        /// <summary>
        /// The size of the floating icon buttons at the bottom right of the detail and result pages on screens tall enough.
        /// </summary>
        public const float ACTION_BUTTON_SIZE = 120f;
        /// <summary>
        /// The largest share of the canvas height a floating icon button takes on short screens.
        /// </summary>
        const float ACTION_BUTTON_MAX_HEIGHT_SHARE = 0.12f;
        /// <summary>
        /// The smallest size of a floating icon button.
        /// </summary>
        const float ACTION_BUTTON_MIN_SIZE = 48f;
        /// <summary>
        /// The smallest distance of the floating icon buttons from the bottom of the safe area.
        /// </summary>
        public const float ACTION_BUTTON_MIN_BOTTOM = 8f;
        /// <summary>
        /// The blur of the soft shadow under a floating icon button.
        /// </summary>
        const float ACTION_BUTTON_SHADOW_BLUR = 16f;
        /// <summary>
        /// Gets the size of the floating icon buttons for a canvas: <see cref="ACTION_BUTTON_SIZE"/>, but at most 12% of the
        /// canvas height so they stay small on short screens such as phones in landscape.
        /// </summary>
        /// <param name="canvasHeight">The height of the canvas area the buttons float over.</param>
        /// <returns>The width and height of one button.</returns>
        public static float GetActionButtonSize(float canvasHeight)
        {
            return Mathf.Clamp(canvasHeight * ACTION_BUTTON_MAX_HEIGHT_SHARE, ACTION_BUTTON_MIN_SIZE, ACTION_BUTTON_SIZE);
        }
        /// <summary>
        /// Gets the gap between two floating icon buttons: a quarter of their size.
        /// </summary>
        /// <param name="buttonSize">The size of the buttons, from <see cref="GetActionButtonSize"/>.</param>
        /// <returns>The gap.</returns>
        public static float GetActionButtonGap(float buttonSize)
        {
            return buttonSize * 0.25f;
        }
        /// <summary>
        /// Gets the distance of the floating icon buttons from the bottom of the safe area, so that their distance from the bottom
        /// of the screen equals the page margin they keep from the right; at least <see cref="ACTION_BUTTON_MIN_BOTTOM"/>, so they
        /// stay clear of a home indicator taller than the margin.
        /// </summary>
        /// <param name="margin">The page margin, which is also the distance of the buttons from the right edge.</param>
        /// <param name="bottomInset">The height of the screen below the safe area, in canvas units.</param>
        /// <returns>The distance from the bottom of the safe area.</returns>
        public static float GetActionButtonBottom(float margin, float bottomInset)
        {
            return Mathf.Max(ACTION_BUTTON_MIN_BOTTOM, margin - Mathf.Max(0f, bottomInset));
        }
        /// <summary>
        /// Gets the distance of the floating icon buttons from the bottom of a safe root, see <see cref="GetActionButtonBottom(float, float)"/>.
        /// </summary>
        /// <param name="safeRoot">The safe-area root from <see cref="CreateSafeRoot"/>, a child of the full-screen canvas root.</param>
        /// <param name="margin">The page margin, which is also the distance of the buttons from the right edge.</param>
        /// <returns>The distance from the bottom of the safe area.</returns>
        public static float GetActionButtonBottom(RectTransform safeRoot, float margin)
        {
            var screenHeight = safeRoot.parent is RectTransform canvasRoot ? canvasRoot.rect.height : 0f;
            return GetActionButtonBottom(margin, safeRoot.anchorMin.y * screenHeight);
        }
        /// <summary>
        /// Gets how much empty space a scroll view needs at its bottom so its last content can scroll above the floating buttons.
        /// </summary>
        /// <param name="buttonSize">The size of the buttons, from <see cref="GetActionButtonSize"/>.</param>
        /// <param name="bottom">The distance of the buttons from the bottom, from <see cref="GetActionButtonBottom(RectTransform, float)"/>.</param>
        /// <returns>The height to add below the content.</returns>
        public static float GetActionButtonClearance(float buttonSize, float bottom)
        {
            return buttonSize + bottom + 34f;
        }
        /// <summary>
        /// Creates a floating icon button with a soft shadow of the same rounded shape; place it with <see cref="PlaceActionButton"/>.
        /// </summary>
        /// <param name="parent">The parent the button floats over.</param>
        /// <param name="icon">The white icon sprite.</param>
        /// <param name="fill">The background color.</param>
        /// <param name="iconColor">The icon color.</param>
        /// <param name="shadowOpacity">The opacity of the shadow.</param>
        /// <param name="onClick">The action run when the button is tapped.</param>
        /// <param name="button">The created button.</param>
        /// <param name="iconImage">The icon image of the button.</param>
        /// <returns>The holder of the shadow and the button; this is what <see cref="PlaceActionButton"/> moves and sizes.</returns>
        public static RectTransform CreateActionButton(Transform parent, Sprite icon, Color fill, Color iconColor, float shadowOpacity,
                                                       UnityAction onClick, out Button button, out Image iconImage)
        {
            var holder = CreateShadowHolder(parent, ACTION_BUTTON_SIZE * 0.25f, ACTION_BUTTON_SHADOW_BLUR, shadowOpacity);
            button = CreateIconButton(holder, icon, fill, iconColor, ACTION_BUTTON_SIZE * 0.5f, onClick, out iconImage, ACTION_BUTTON_SIZE * 0.25f);
            Stretch((RectTransform)button.transform);
            return holder;
        }
        /// <summary>
        /// Places a floating icon button at the bottom right and sizes it: the corner radius is a quarter of the size and the icon
        /// half of it, so it keeps its proportions when <see cref="GetActionButtonSize"/> shrinks it.
        /// </summary>
        /// <param name="holder">The holder returned by <see cref="CreateActionButton"/>.</param>
        /// <param name="iconImage">The icon image of the button.</param>
        /// <param name="right">The distance from the right edge.</param>
        /// <param name="bottom">The distance from the bottom edge, from <see cref="GetActionButtonBottom(RectTransform, float)"/>.</param>
        /// <param name="size">The width and height of the button.</param>
        public static void PlaceActionButton(RectTransform holder, Image iconImage, float right, float bottom, float size)
        {
            PlaceBottomRight(holder, right, bottom, size, size);
            var radius = size * 0.25f;
            if (holder.Find("Shadow") is RectTransform shadowRect && shadowRect.TryGetComponent<Image>(out var shadow))
            {
                UiSprites.ApplySoftShadow(shadow, radius, ACTION_BUTTON_SHADOW_BLUR);
                Stretch(shadowRect, -ACTION_BUTTON_SHADOW_BLUR);
            }
            if (holder.Find("IconButton") is RectTransform backgroundRect && backgroundRect.TryGetComponent<Image>(out var background))
            {
                UiSprites.ApplyRounded(background, radius);
            }
            iconImage.rectTransform.sizeDelta = new Vector2(size * 0.5f, size * 0.5f);
        }
        /// <summary>
        /// Creates a button with a rounded background and a centered icon and no text.
        /// </summary>
        /// <param name="parent">The parent the button is created in; place or stretch the button afterwards.</param>
        /// <param name="icon">The white icon sprite, tinted with <paramref name="iconColor"/>.</param>
        /// <param name="fill">The background color.</param>
        /// <param name="iconColor">The icon color.</param>
        /// <param name="iconSize">The width and height of the icon.</param>
        /// <param name="onClick">The action run when the button is tapped.</param>
        /// <param name="iconImage">The icon image, passed to <see cref="SetIconButtonInteractable"/>.</param>
        /// <param name="radius">The corner radius of the background.</param>
        /// <returns>The button.</returns>
        public static Button CreateIconButton(Transform parent, Sprite icon, Color fill, Color iconColor, float iconSize, UnityAction onClick,
                                              out Image iconImage, float radius = 20f)
        {
            var background = CreateImage(parent, fill, radius);
            background.gameObject.name = "IconButton";
            background.raycastTarget = true;
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            SetButtonColors(button);
            button.onClick.AddListener(onClick);
            iconImage = CreateImage(background.transform, iconColor, 0f);
            iconImage.gameObject.name = "Icon";
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            var iconRect = iconImage.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            iconRect.anchoredPosition = Vector2.zero;
            return button;
        }
        /// <summary>
        /// Enables or disables an icon button created by <see cref="CreateIconButton"/>; a disabled button fades its icon
        /// together with its background.
        /// </summary>
        /// <param name="button">The button.</param>
        /// <param name="iconImage">The icon image of the button.</param>
        /// <param name="interactable">Whether the button can be pressed.</param>
        public static void SetIconButtonInteractable(Button button, Image iconImage, bool interactable)
        {
            if (button.interactable == interactable)
            {
                return;
            }
            button.interactable = interactable;
            var color = iconImage.color;
            color.a = interactable ? 1f : DISABLED_ICON_ALPHA;
            iconImage.color = color;
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
        /// Creates the player nickname and round avatar shown at the right end of a top bar.
        /// </summary>
        /// <param name="parent">The parent the view is created in; place the returned rectangle afterwards.</param>
        /// <param name="height">The height of the view, which is also the avatar diameter.</param>
        /// <param name="backgroundColor">The color behind the view, used to round the avatar corners.</param>
        /// <returns>The rectangle of the view, with the nickname right-aligned to the left of the avatar.</returns>
        public RectTransform CreatePlayerProfile(Transform parent, float height, Color backgroundColor)
        {
            const float GAP = 12f;
            const float ICON_INSET_RATIO = 0.18f;
            var root = CreateRect("PlayerProfile", parent);
            var avatar = CreateRoundedCover(root, height * 0.5f, backgroundColor, out var avatarFrame);
            avatarFrame.anchorMin = new Vector2(1f, 0.5f);
            avatarFrame.anchorMax = new Vector2(1f, 0.5f);
            avatarFrame.pivot = new Vector2(1f, 0.5f);
            avatarFrame.sizeDelta = new Vector2(height, height);
            avatarFrame.anchoredPosition = Vector2.zero;
            var placeholderIcon = CreateImage(avatarFrame, MUTED, 0f);
            placeholderIcon.gameObject.name = "PersonIcon";
            placeholderIcon.sprite = UiSprites.Person;
            placeholderIcon.preserveAspect = true;
            Stretch(placeholderIcon.rectTransform, height * ICON_INSET_RATIO);
            var nameText = CreateText(root, PlayerProfile.GUEST_NAME, 20f, TEXT, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            Stretch(nameText.rectTransform);
            nameText.rectTransform.offsetMax = new Vector2(-(height + GAP), 0f);
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 14f;
            nameText.fontSizeMax = 20f;
            var view = root.gameObject.AddComponent<PlayerProfileView>();
            view.Init(nameText, avatar, placeholderIcon);
            return root;
        }
        /// <summary>
        /// 左栏顶部的标题栏：圆角应用图标、大字标题、小字副标题；返回标题栏占用的高度
        /// </summary>
        public float CreateSidebarHeader(RectTransform parent, string title, string subtitle)
        {
            const float ICON = 64f;
            const float LEFT = 26f;
            const float TOP = 24f;
            var icon = CreateRoundedCover(parent, 15f, PAPER, out var iconFrame);
            PlaceTopLeft(iconFrame, LEFT, TOP, ICON, ICON);
            var iconSprite = AppIcon;
            if (iconSprite != null)
            {
                SetCover(icon, iconSprite);
            }
            else
            {
                icon.color = ACCENT;
            }
            // 点图标打开本项目的 GitHub 仓库
            var hit = iconFrame.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            var iconButton = iconFrame.gameObject.AddComponent<Button>();
            iconButton.transition = Selectable.Transition.None;
            iconButton.onClick.AddListener(() => Application.OpenURL(REPOSITORY_URL));
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
        /// <summary>
        /// Gets the color of a difficulty in the light interfaces and the share image; the only table of these colors.
        /// </summary>
        /// <param name="level">The difficulty.</param>
        /// <returns>The color: Re:Master is pink so it stands apart from the purple Master; UTAGE is magenta.</returns>
        public static Color GetDifficultyColor(ChartLevel level)
        {
            return level switch
            {
                ChartLevel.Easy => Hex(0x0E9BEE),
                ChartLevel.Basic => Hex(0x22BB5B),
                ChartLevel.Advance => Hex(0xF08A12),
                ChartLevel.Expert => Hex(0xF64861),
                ChartLevel.Master => Hex(0x9E45E2),
                ChartLevel.ReMaster => Hex(0xEE5DB0),
                _ => Hex(0xE8379C),
            };
        }
        /// <summary>
        /// Gets the DX star count as in the original game: 1 to 5 stars above 85% / 90% / 93% / 95% / 97% of the maximum DX score.
        /// </summary>
        /// <param name="dxScore">The DX score.</param>
        /// <param name="totalDxScore">The maximum DX score of the chart.</param>
        /// <returns>The number of stars, 0 to 5; 0 when the maximum is not positive.</returns>
        public static int GetDxStarCount(long dxScore, long totalDxScore)
        {
            if (totalDxScore <= 0)
            {
                return 0;
            }
            var percent = dxScore / (double)totalDxScore;
            return percent switch
            {
                > 0.97 => 5,
                > 0.95 => 4,
                > 0.93 => 3,
                > 0.90 => 2,
                > 0.85 => 1,
                _ => 0,
            };
        }
        /// <summary>
        /// Adds the gold DX score bar on a grey track, filled by the share of the maximum DX score, as on the result page.
        /// </summary>
        /// <param name="parent">The card the bar is placed in.</param>
        /// <param name="left">The distance from the left edge of the card.</param>
        /// <param name="top">The distance from the top edge of the card.</param>
        /// <param name="right">The distance from the right edge of the card.</param>
        /// <param name="height">The height of the bar.</param>
        /// <param name="dxScore">The DX score.</param>
        /// <param name="totalDxScore">The maximum DX score of the chart.</param>
        public static void AddDxProgressBar(RectTransform parent, float left, float top, float right, float height, long dxScore, long totalDxScore)
        {
            var ratio = Mathf.Clamp01((float)(dxScore / (double)Math.Max(1L, totalDxScore)));
            var track = CreateImage(parent, LINE, height * 0.5f);
            track.gameObject.name = "DxTrack";
            PlaceRow(track.rectTransform, left, top, right, height);
            var fill = CreateImage(track.transform, GOLD, height * 0.5f);
            fill.gameObject.name = "DxFill";
            var fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(ratio, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.enabled = ratio > 0f;
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
            // 手机上按物理尺寸放大，平板与电脑保持 1920×1080 参考布局
            AdaptiveCanvasScaler.Attach(scaler);
            canvasObject.AddComponent<GraphicRaycaster>();
            group = canvasObject.AddComponent<CanvasGroup>();
            return (RectTransform)canvasObject.transform;
        }
        /// <summary>
        /// Fills a canvas with a page background and returns a child fitted into the safe area of the screen for the content.
        /// </summary>
        /// <param name="canvasRoot">The full-screen root of the canvas.</param>
        /// <param name="background">The page color drawn under the notch and the home indicator too.</param>
        /// <returns>The safe-area child; put everything except full-screen dimming inside it.</returns>
        public static RectTransform CreateSafeRoot(RectTransform canvasRoot, Color background)
        {
            var page = CreateImage(canvasRoot, background, 0f);
            page.gameObject.name = "PageBackground";
            Stretch(page.rectTransform);
            return SafeAreaFitter.Create(canvasRoot);
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
