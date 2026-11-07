using MajdataPlay.Diagnostics;
using MajdataPlay.i18n;
using MajdataPlay.IO;
using MajdataPlay.Misc.Base;
using MajdataPlay.Settings;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#nullable enable
namespace MajdataPlay.Scenes.Setting
{
    /// <summary>
    /// 列表式选歌时使用的新设置界面：左侧分类，右侧每行一个选项（名称、说明、当前值与 −/+）。
    /// 选项的取值、显示文字和副作用全部沿用原设置页的 <see cref="OptionData"/>；原界面停止渲染和响应输入
    /// </summary>
    internal sealed class SettingBrowser : MonoBehaviour
    {
        public static bool IsOpen { get; private set; } = false;

        const float TOP_PADDING = 22f;
        const float TOPBAR_HEIGHT = 60f;
        const float LIST_TOP = 104f;
        const float SCROLLBAR_WIDTH = 16f;
        /// <summary>
        /// The widest the value area (−, the value and +) of an option row becomes.
        /// </summary>
        const float VALUE_AREA_WIDTH = 392f;
        /// <summary>
        /// The narrowest the value area of an option row becomes on narrow pages.
        /// </summary>
        const float MIN_VALUE_AREA_WIDTH = 240f;
        /// <summary>
        /// The width the name and description of an option keep before the value area is narrowed.
        /// </summary>
        const float MIN_TEXT_COLUMN_WIDTH = 200f;
        /// <summary>
        /// The gap between the sidebar (or the left edge) and the options.
        /// </summary>
        const float MAIN_LEFT_PADDING = 32f;
        /// <summary>
        /// The width of the Categories button that opens the sidebar drawer on narrow pages.
        /// </summary>
        const float MENU_BUTTON_WIDTH = 96f;
        /// <summary>
        /// The space the Calibrate and Done buttons take at the right of the top bar.
        /// </summary>
        const float TOP_BUTTONS_WIDTH = 280f;
        const float STEP_BUTTON_WIDTH = 60f;
        const float CARD_RADIUS = 15f;
        const int RENDERING_ADVANCED_TAP_COUNT = 10;
        const float RENDERING_ADVANCED_TAP_GAP_SEC = 1.5f;

        /// <summary>
        /// 一个选项的卡片
        /// </summary>
        sealed class RowView
        {
            public required OptionData Data { get; init; }
            public required RectTransform Root { get; init; }
            public required Image Border { get; init; }
            public required TextMeshProUGUI Value { get; init; }
            public string ValueText { get; set; } = string.Empty;
        }

        SettingManager _manager = null!;
        LightUi _ui = null!;
        /// <summary>
        /// The full-screen root of the canvas, filled with the page background.
        /// </summary>
        RectTransform _canvasRoot = null!;
        /// <summary>
        /// The safe area of the screen, which holds the sidebar and the options.
        /// </summary>
        RectTransform _root = null!;
        CanvasGroup _group = null!;
        /// <summary>
        /// Docks the category sidebar on wide pages and makes it a drawer on narrow ones.
        /// </summary>
        SidebarDrawer _drawer = null!;
        /// <summary>
        /// The Categories button at the left of the top bar, shown while the sidebar is a drawer.
        /// </summary>
        RectTransform _menuButton = null!;
        /// <summary>
        /// The area holding the option list, whose width decides the width of the value areas.
        /// </summary>
        RectTransform _listPane = null!;
        /// <summary>
        /// The width of the value area of the option rows, see <see cref="GetValueAreaWidth"/>.
        /// </summary>
        float _valueAreaWidth = VALUE_AREA_WIDTH;
        /// <summary>
        /// The page size the layout was last applied for.
        /// </summary>
        Vector2 _lastPageSize;
        readonly SceneCanvasHider _canvasHider = new();
        RectTransform _sidebarContent = null!;
        TextMeshProUGUI _categoryTitle = null!;
        RectTransform _listContent = null!;
        ScrollRect _listScroll = null!;

        readonly List<RowView> _rows = new();
        int _menuIndex = 0;
        int _selectedRow = 0;
        bool _isLeaving = false;
        bool _needsRebuild = false;
        OffsetUnitOption _offsetUnit;
        /// <summary>
        /// 当前分类各选项与各分类上次的可见状态：设置变化后（如渲染档位、录制方式）可见项不同时重建
        /// </summary>
        readonly List<bool> _optionVisibleState = new();
        readonly List<bool> _menuVisibleState = new();
        int _renderingTapCount = 0;
        float _lastRenderingTapTime = -10f;
        bool _advancedInputHeld = false;

        InputRepeatState _categoryInput;
        InputRepeatState _optionInput;
        InputRepeatState _valueInput;

        public static SettingBrowser? Create(SettingManager manager, int menuIndex)
        {
            try
            {
                var obj = new GameObject("SettingBrowser");
                var browser = obj.AddComponent<SettingBrowser>();
                browser.Init(manager, menuIndex);
                return browser;
            }
            catch (Exception e)
            {
                MajDebug.LogError(e);
                IsOpen = false;
                InputManager.SuppressTouchPanelSimulation = false;
                return null;
            }
        }

        #region Lifecycle
        void Init(SettingManager manager, int menuIndex)
        {
            _manager = manager;
            _ui = new LightUi(LightUi.FindFont());
            _offsetUnit = MajEnv.Settings.Debug.OffsetUnit;
            LightUi.EnsureEventSystem();

            _canvasRoot = LightUi.CreateOverlayCanvas(transform, "SettingBrowserCanvas", 30, out _group);
            // 背景铺满全屏，内容放在安全区域内，避开刘海、圆角和底部横条
            _root = LightUi.CreateSafeRoot(_canvasRoot, LightUi.PAGE);
            LightUi.FollowTransition(_group, false);
            BuildLayout();
            UpdateLayout();

            // 原界面：旧选项不再刷新（避免重复播放音量试听声），画布停止渲染，SettingManager.Update 不再处理输入
            foreach (var menu in _manager.Menus)
            {
                menu.gameObject.SetActive(false);
            }
            _canvasHider.Hide(gameObject);
            IsOpen = true;
            InputManager.SuppressTouchPanelSimulation = true;
            Localization.OnLanguageChanged += OnLanguageChanged;

            SelectMenu(Mathf.Clamp(menuIndex, 0, _manager.Menus.Count - 1), _manager.Config.SelectedOption);
            _categoryInput.SuppressUntilRelease();
            _optionInput.SuppressUntilRelease();
            _valueInput.SuppressUntilRelease();
        }
        void OnDestroy()
        {
            IsOpen = false;
            InputManager.SuppressTouchPanelSimulation = false;
            Localization.OnLanguageChanged -= OnLanguageChanged;
            _drawer?.Cancel();
        }
        void LateUpdate()
        {
            if (!_isLeaving)
            {
                // 屏幕方向、尺寸或安全区变化时重新排列
                UpdateLayout();
            }
        }
        void OnLanguageChanged(object sender, Language language)
        {
            // 语言切换发生在选项的 MoveNext 里：先标记，下一帧再重建，免得在按钮回调中途销毁它
            _needsRebuild = true;
        }
        void Update()
        {
            LightUi.FollowTransition(_group, _isLeaving);
            if (_isLeaving)
            {
                return;
            }
            if (_needsRebuild)
            {
                _needsRebuild = false;
                foreach (var menu in _manager.Menus)
                {
                    foreach (var data in menu.Options)
                    {
                        data.RefreshLocalization();
                    }
                }
                RenderSidebar();
                RenderRows(true);
            }
            // 偏移单位变化：换算所有偏移（与原设置页相同），再刷新偏移类选项与说明
            if (_manager.UpdateOffsetUnitIfNeeded() || _offsetUnit != MajEnv.Settings.Debug.OffsetUnit)
            {
                _offsetUnit = MajEnv.Settings.Debug.OffsetUnit;
                foreach (var menu in _manager.Menus)
                {
                    foreach (var data in menu.Options)
                    {
                        if (data.IsOffsetOption)
                        {
                            data.Refresh();
                        }
                    }
                }
                RenderRows(true);
            }
            // 选项的显示条件（ShowInSettingUIWhen）变化后，可见的选项或分类不同
            if (HasVisibilityChanged(_optionVisibleState, GetOptionVisibility(CurrentMenu)))
            {
                RenderRows(true);
            }
            if (HasVisibilityChanged(_menuVisibleState, GetMenuVisibility()))
            {
                RenderSidebar();
            }
            RefreshValues();
            if (_manager.IsReady)
            {
                HandleCabinetInput();
            }
        }
        #endregion

        #region Layout
        Menu CurrentMenu => _manager.Menus[_menuIndex];

        void BuildLayout()
        {
            // 左栏：标题栏与分类；宽屏时停靠在左边，窄屏时改为从左侧滑出的抽屉，宽度由 SidebarDrawer 决定
            var sidebar = LightUi.CreateRect("Sidebar", _root);
            sidebar.gameObject.AddComponent<Image>().color = LightUi.PAPER;
            var sidebarLine = LightUi.CreateImage(sidebar, LightUi.LINE, 0f);
            sidebarLine.rectTransform.anchorMin = new Vector2(1f, 0f);
            sidebarLine.rectTransform.anchorMax = Vector2.one;
            sidebarLine.rectTransform.offsetMin = new Vector2(-1f, 0f);
            sidebarLine.rectTransform.offsetMax = Vector2.zero;
            var headerHeight = _ui.CreateSidebarHeader(sidebar, "MAJTEXT_SETTING_TITLE".i18n(), "MajdataPlay · urntt edition");
            _sidebarContent = LightUi.CreateScroll(sidebar, 0f, headerHeight + 16f, 0f, 0f, out _);
            var sidebarLayout = _sidebarContent.gameObject.AddComponent<VerticalLayoutGroup>();
            sidebarLayout.padding = new RectOffset(16, 16, 0, 24);
            sidebarLayout.spacing = 4f;
            sidebarLayout.childControlWidth = true;
            sidebarLayout.childControlHeight = false;
            sidebarLayout.childForceExpandWidth = true;
            sidebarLayout.childForceExpandHeight = false;
            _sidebarContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 右侧：分类名、校准与完成，下面是选项列表（左边距由 SidebarDrawer 设置）
            var main = LightUi.CreateRect("Main", _root);
            main.anchorMin = Vector2.zero;
            main.anchorMax = Vector2.one;
            main.offsetMin = new Vector2(MAIN_LEFT_PADDING, 0f);
            main.offsetMax = new Vector2(-20f, 0f);
            var topRow = LightUi.CreateRect("TopRow", main);
            LightUi.PlaceRow(topRow, 0f, TOP_PADDING, 0f, TOPBAR_HEIGHT);
            // 窄屏时最左边是“分类”键，点开分类抽屉
            var menuButton = _ui.CreateButton(topRow, "MAJTEXT_LIST_CATEGORY".i18n(), LightUi.PAPER, LightUi.TEXT, 20f, ToggleDrawer);
            _menuButton = (RectTransform)menuButton.transform;
            LightUi.PlaceTopLeft(_menuButton, 0f, 2f, MENU_BUTTON_WIDTH, 56f);
            _menuButton.gameObject.SetActive(false);
            _categoryTitle = _ui.CreateText(topRow, string.Empty, 30f, LightUi.TEXT, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            LightUi.Stretch(_categoryTitle.rectTransform);
            _categoryTitle.rectTransform.offsetMax = new Vector2(-TOP_BUTTONS_WIDTH, 0f);
            _categoryTitle.enableAutoSizing = true;
            _categoryTitle.fontSizeMin = 18f;
            _categoryTitle.fontSizeMax = 30f;
            var doneButton = _ui.CreateButton(topRow, "MAJTEXT_SETTING_DONE".i18n(), LightUi.ACCENT, Color.white, 20f, RequestExit);
            LightUi.PlaceTopRight((RectTransform)doneButton.transform, 0f, 2f, 124f, 56f);
            var calibrateButton = _ui.CreateButton(topRow, "MAJTEXT_SETTING_CALIBRATE".i18n(), LightUi.PAPER, LightUi.TEXT, 20f, RequestCalibrator);
            LightUi.PlaceTopRight((RectTransform)calibrateButton.transform, 136f, 2f, 124f, 56f);

            _listPane = LightUi.CreateRect("ListPane", main);
            LightUi.Stretch(_listPane);
            _listPane.offsetMax = new Vector2(0f, -LIST_TOP);
            _listContent = LightUi.CreateScroll(_listPane, 0f, 0f, 0f, 0f, out _listScroll);
            var listLayout = _listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.padding = new RectOffset(0, (int)(SCROLLBAR_WIDTH + 14f), 4, 48);
            listLayout.spacing = 12f;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            _listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            LightUi.AddScrollbar(_listScroll, SCROLLBAR_WIDTH);

            // 分类栏放在最上层，窄屏时作为抽屉盖住选项
            _drawer = new SidebarDrawer(_root, sidebar, main, MAIN_LEFT_PADDING);
        }
        /// <summary>
        /// Lays the page out for its current size: docks the sidebar or turns it into a drawer, and narrows the value areas of the
        /// option rows on narrow pages. Does nothing while the size is unchanged.
        /// </summary>
        void UpdateLayout()
        {
            var size = _root.rect.size;
            if (size.x <= 0f || (Mathf.Abs(size.x - _lastPageSize.x) <= 1f && Mathf.Abs(size.y - _lastPageSize.y) <= 1f))
            {
                return;
            }
            _lastPageSize = size;
            _drawer.Apply(size.x, size.y);
            _menuButton.gameObject.SetActive(_drawer.IsCompact);
            _categoryTitle.rectTransform.offsetMin = new Vector2(_drawer.IsCompact ? MENU_BUTTON_WIDTH + 12f : 0f, 0f);
            var valueAreaWidth = GetValueAreaWidth(_listPane.rect.width);
            if (Mathf.Abs(valueAreaWidth - _valueAreaWidth) > 0.5f)
            {
                _valueAreaWidth = valueAreaWidth;
                if (_rows.Count > 0)
                {
                    RenderRows(true);
                }
            }
        }
        /// <summary>
        /// Gets the width of the value area of the option rows: <see cref="VALUE_AREA_WIDTH"/>, narrowed on narrow pages so the name
        /// and description keep <see cref="MIN_TEXT_COLUMN_WIDTH"/>, but not below <see cref="MIN_VALUE_AREA_WIDTH"/>.
        /// </summary>
        /// <param name="listWidth">The width of the option list, including the scrollbar.</param>
        /// <returns>The width of the value area.</returns>
        static float GetValueAreaWidth(float listWidth)
        {
            // 卡片左右内边距 24 + 18、文字与取值之间 20，再减去滚动条
            var rowWidth = listWidth - (SCROLLBAR_WIDTH + 14f);
            var available = rowWidth - 24f - 18f - 20f - MIN_TEXT_COLUMN_WIDTH;
            return Mathf.Clamp(available, MIN_VALUE_AREA_WIDTH, VALUE_AREA_WIDTH);
        }
        /// <summary>
        /// Opens or closes the category drawer from the Categories button.
        /// </summary>
        void ToggleDrawer()
        {
            _drawer.Toggle();
        }
        void RenderSidebar()
        {
            LightUi.ClearChildren(_sidebarContent);
            var menus = _manager.Menus;
            _menuVisibleState.Clear();
            _menuVisibleState.AddRange(GetMenuVisibility());
            for (var i = 0; i < menus.Count; i++)
            {
                if (!_menuVisibleState[i])
                {
                    continue;
                }
                var index = i;
                var isSelected = i == _menuIndex;
                var name = GetMenuTitle(menus[i]);
                var songTitle = menus[i].Name == "ChartSetting" ? FindSelectedSongTitle() : null;
                var item = _ui.CreateButton(_sidebarContent, string.Empty, isSelected ? LightUi.ACCENT_SOFT : LightUi.PAPER, LightUi.TEXT, 19f, () => SelectMenu(index, null));
                var itemRect = (RectTransform)item.transform;
                itemRect.sizeDelta = new Vector2(0f, string.IsNullOrEmpty(songTitle) ? 56f : 76f);
                var label = item.GetComponentInChildren<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.color = isSelected ? LightUi.ACCENT : LightUi.TEXT;
                label.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
                label.rectTransform.offsetMin = new Vector2(18f, 4f);
                label.rectTransform.offsetMax = new Vector2(-14f, -4f);
                if (string.IsNullOrEmpty(songTitle))
                {
                    label.text = name;
                }
                else
                {
                    // 谱面设置：下面用小字写当前这首歌
                    label.enableWordWrapping = false;
                    label.text = $"{name}\n<size=75%><color=#788391>{EscapeRichText(songTitle!)}</color></size>";
                }
            }
        }
        static string GetMenuTitle(Menu menu)
        {
            return menu.GetLocalizedTitle();
        }
        /// <summary>
        /// 分类是否显示（与原设置页相同：当前分类总是显示，其余按 ShowInSettingUIWhen 条件）
        /// </summary>
        bool IsMenuVisible(int index)
        {
            var menus = _manager.Menus;
            return index == _menuIndex || menus[index].Visibility.IsVisible(MajEnv.Settings);
        }
        IEnumerable<bool> GetMenuVisibility()
        {
            for (var i = 0; i < _manager.Menus.Count; i++)
            {
                yield return IsMenuVisible(i);
            }
        }
        IEnumerable<bool> GetOptionVisibility(Menu menu)
        {
            foreach (var data in menu.Options)
            {
                yield return IsVisible(menu, data.PropertyInfo);
            }
        }
        static bool HasVisibilityChanged(List<bool> last, IEnumerable<bool> current)
        {
            var index = 0;
            var isChanged = false;
            foreach (var isVisible in current)
            {
                if (index >= last.Count || last[index] != isVisible)
                {
                    isChanged = true;
                    break;
                }
                index++;
            }
            if (!isChanged && index != last.Count)
            {
                isChanged = true;
            }
            if (isChanged)
            {
                last.Clear();
                last.AddRange(current);
            }
            return isChanged;
        }
        /// <summary>
        /// 从当前分类按方向找下一个显示的分类（到头循环）
        /// </summary>
        int FindVisibleMenu(int from, int direction)
        {
            var count = _manager.Menus.Count;
            var index = from;
            for (var step = 0; step < count; step++)
            {
                index = ((index + direction) % count + count) % count;
                if (_manager.Menus[index].Visibility.IsVisible(MajEnv.Settings))
                {
                    return index;
                }
            }
            return from;
        }
        static string? FindSelectedSongTitle()
        {
            return SongStorage.FindSelectedSong()?.Title;
        }
        static string EscapeRichText(string text)
        {
            return text.Replace("<", "<​");
        }
        void SelectMenu(int index, string? selectedOption)
        {
            var menus = _manager.Menus;
            if (menus.Count == 0)
            {
                return;
            }
            // 窄屏时选完分类自动收起抽屉
            _drawer.Close();
            _menuIndex = (index % menus.Count + menus.Count) % menus.Count;
            _manager.Config.SelectedMenu = CurrentMenu.Name;
            _selectedRow = 0;
            ResetRenderingGesture();
            RenderSidebar();
            RenderRows(false);
            if (!string.IsNullOrEmpty(selectedOption))
            {
                var rowIndex = _rows.FindIndex(x => x.Data.PropertyInfo.Name == selectedOption);
                if (rowIndex >= 0)
                {
                    SelectRow(rowIndex, true);
                }
            }
        }
        /// <summary>
        /// 重新生成当前分类的选项卡片
        /// </summary>
        /// <param name="isKeepingScroll">语言、单位或渲染档位变化时保留滚动位置和选中的选项；切换分类时回到顶部</param>
        void RenderRows(bool isKeepingScroll)
        {
            var scrollY = _listContent.anchoredPosition.y;
            var selectedName = _selectedRow < _rows.Count ? _rows[_selectedRow].Data.PropertyInfo.Name : null;
            LightUi.ClearChildren(_listContent);
            _rows.Clear();

            var menu = CurrentMenu;
            var title = GetMenuTitle(menu);
            if (menu.Name == "ChartSetting" && FindSelectedSongTitle() is string songTitle)
            {
                title = $"{title} · {songTitle}";
            }
            _categoryTitle.text = title;
            _optionVisibleState.Clear();
            _optionVisibleState.AddRange(GetOptionVisibility(menu));
            foreach (var data in menu.Options)
            {
                if (!IsVisible(menu, data.PropertyInfo))
                {
                    continue;
                }
                data.RefreshIfChanged();
                _rows.Add(CreateRow(data, _rows.Count));
            }

            _selectedRow = 0;
            if (isKeepingScroll && selectedName is not null)
            {
                var index = _rows.FindIndex(x => x.Data.PropertyInfo.Name == selectedName);
                _selectedRow = Math.Max(0, index);
            }
            UpdateRowHighlight();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_listContent);
            _listScroll.StopMovement();
            SetListScroll(isKeepingScroll ? scrollY : 0f);
        }
        /// <summary>
        /// 选项的可见规则与原设置页相同：按选项的 ShowInSettingUIWhen 条件（渲染分类的兼容模式、质量档位等也由它声明）
        /// </summary>
        static bool IsVisible(Menu menu, PropertyInfo property)
        {
            return SettingReflectionCache.GetVisibility(property).IsVisible(menu.Instance);
        }
        RowView CreateRow(OptionData data, int rowIndex)
        {
            // 卡片：边框与底色不参与布局，内容用 HorizontalLayoutGroup 撑开高度（说明多长卡片就多高）
            var row = LightUi.CreateRect("Option", _listContent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 18, 16, 16);
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // 白底与一圈等宽描边，同一矩形、同一圆角（选中时描边为主题色、稍粗）
            var background = LightUi.CreateImage(row, LightUi.PAPER, CARD_RADIUS);
            background.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            LightUi.Stretch(background.rectTransform);
            var border = LightUi.AddRing(row, LightUi.LINE, CARD_RADIUS, 1.5f);
            // 点卡片空白处选中这一行；“渲染质量”连点 10 次进入自定义
            background.raycastTarget = true;
            var rowButton = background.gameObject.AddComponent<Button>();
            rowButton.transition = Selectable.Transition.None;
            rowButton.onClick.AddListener(() => OnRowTapped(rowIndex));

            var textColumn = LightUi.CreateRect("Text", row);
            var textLayout = textColumn.gameObject.AddComponent<VerticalLayoutGroup>();
            textLayout.spacing = 4f;
            textLayout.childAlignment = TextAnchor.MiddleLeft;
            textLayout.childControlWidth = true;
            textLayout.childControlHeight = true;
            textLayout.childForceExpandWidth = true;
            textLayout.childForceExpandHeight = false;
            var textElement = textColumn.gameObject.AddComponent<LayoutElement>();
            textElement.flexibleWidth = 1f;
            textElement.minWidth = 160f;
            var name = _ui.CreateText(textColumn, data.LocalizedName, 22f, LightUi.TEXT, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            name.gameObject.AddComponent<LayoutElement>().minHeight = 33f;
            var description = GetDescription(data);
            if (!string.IsNullOrEmpty(description))
            {
                var descriptionText = _ui.CreateText(textColumn, description, 16f, LightUi.MUTED, TextAlignmentOptions.TopLeft, FontStyles.Normal);
                descriptionText.enableWordWrapping = true;
                descriptionText.overflowMode = TextOverflowModes.Overflow;
            }

            // 右侧：−　当前值　+
            var valueArea = LightUi.CreateRect("Value", row);
            var valueLayout = valueArea.gameObject.AddComponent<HorizontalLayoutGroup>();
            valueLayout.spacing = 8f;
            valueLayout.childAlignment = TextAnchor.MiddleCenter;
            valueLayout.childControlWidth = true;
            valueLayout.childControlHeight = true;
            valueLayout.childForceExpandWidth = false;
            valueLayout.childForceExpandHeight = false;
            var valueElement = valueArea.gameObject.AddComponent<LayoutElement>();
            valueElement.minWidth = _valueAreaWidth;
            valueElement.preferredWidth = _valueAreaWidth;
            var isReadOnly = data.IsReadOnly;
            CreateStepButton(valueArea, "−", rowIndex, -1, isReadOnly);
            var value = _ui.CreateText(valueArea, data.LocalizedValueText, 22f, LightUi.ACCENT, TextAlignmentOptions.Center, FontStyles.Bold);
            var valueTextElement = value.gameObject.AddComponent<LayoutElement>();
            valueTextElement.preferredWidth = _valueAreaWidth - STEP_BUTTON_WIDTH * 2f - 16f;
            valueTextElement.minHeight = 52f;
            value.gameObject.AddComponent<TMPMarqueeText>();
            CreateStepButton(valueArea, "+", rowIndex, 1, isReadOnly);

            return new RowView()
            {
                Data = data,
                Root = row,
                Border = border,
                Value = value,
                ValueText = data.LocalizedValueText,
            };
        }
        void CreateStepButton(RectTransform parent, string label, int rowIndex, int direction, bool isReadOnly)
        {
            var button = _ui.CreateButton(parent, label, LightUi.ACCENT_SOFT, LightUi.ACCENT, 30f, null, 12f);
            var element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = STEP_BUTTON_WIDTH;
            element.preferredHeight = 52f;
            if (isReadOnly)
            {
                // 只读选项不显示 −/+（保留位置，值文字仍居中）
                var group = button.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                button.interactable = false;
                return;
            }
            // 由 RepeatButton 处理按下与长按连发，Button 只负责按下的颜色变化
            var repeat = button.gameObject.AddComponent<RepeatButton>();
            repeat.Target = button;
            repeat.OnRepeat = () => ChangeValue(rowIndex, direction);
        }
        static string GetDescription(OptionData data)
        {
            if (!data.HasDescription)
            {
                return string.Empty;
            }
            var description = data.DescriptionKey.i18n();
            if (data.IsOffsetOption)
            {
                description += $"\n{$"MAJTEXT_SETTING_OFFSETUNIT_{MajEnv.Settings.Debug.OffsetUnit}".i18n()}";
            }
            return description;
        }
        #endregion

        #region Values & selection
        void ChangeValue(int rowIndex, int direction)
        {
            if (_isLeaving || rowIndex >= _rows.Count)
            {
                return;
            }
            SelectRow(rowIndex, false);
            var row = _rows[rowIndex];
            row.Data.RefreshIfChanged();
            var hasChanged = direction > 0 ? row.Data.MoveNext() : row.Data.MovePrevious();
            if (hasChanged)
            {
                UpdateValueText(row);
            }
        }
        /// <summary>
        /// 每帧同步当前分类的值：分享/导出状态、备份文件、联动修改的其他选项都靠它更新
        /// </summary>
        void RefreshValues()
        {
            foreach (var row in _rows)
            {
                row.Data.RefreshIfChanged();
                UpdateValueText(row);
            }
        }
        static void UpdateValueText(RowView row)
        {
            var text = row.Data.LocalizedValueText;
            if (!ReferenceEquals(text, row.ValueText) && text != row.ValueText)
            {
                row.ValueText = text;
                row.Value.text = text;
            }
        }
        void SelectRow(int index, bool isScrolling)
        {
            if (_rows.Count == 0)
            {
                return;
            }
            var newIndex = (index % _rows.Count + _rows.Count) % _rows.Count;
            if (newIndex != _selectedRow)
            {
                ResetRenderingGesture();
            }
            _selectedRow = newIndex;
            _manager.Config.SelectedOption = _rows[_selectedRow].Data.PropertyInfo.Name;
            UpdateRowHighlight();
            if (isScrolling)
            {
                ScrollToRow(_rows[_selectedRow]);
            }
        }
        void UpdateRowHighlight()
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                var isSelected = i == _selectedRow;
                var border = _rows[i].Border;
                border.color = isSelected ? LightUi.ACCENT : LightUi.LINE;
                UiSprites.ApplyRing(border, CARD_RADIUS, isSelected ? 2.5f : 1.5f);
            }
        }
        void ScrollToRow(RowView row)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_listContent);
            var viewportHeight = ((RectTransform)_listContent.parent).rect.height;
            var top = -row.Root.anchoredPosition.y - row.Root.rect.height * (1f - row.Root.pivot.y);
            var bottom = top + row.Root.rect.height;
            var scrollY = _listContent.anchoredPosition.y;
            if (top < scrollY + 8f)
            {
                SetListScroll(top - 8f);
            }
            else if (bottom > scrollY + viewportHeight - 8f)
            {
                SetListScroll(bottom - viewportHeight + 8f);
            }
        }
        void SetListScroll(float y)
        {
            var viewportHeight = ((RectTransform)_listContent.parent).rect.height;
            var maxY = Mathf.Max(0f, _listContent.rect.height - viewportHeight);
            var position = _listContent.anchoredPosition;
            position.y = Mathf.Clamp(y, 0f, maxY);
            _listContent.anchoredPosition = position;
        }
        void OnRowTapped(int rowIndex)
        {
            if (rowIndex >= _rows.Count)
            {
                return;
            }
            SelectRow(rowIndex, false);
            if (_rows[rowIndex].Data.PropertyInfo.Name == nameof(RenderingOptions.RenderQuality))
            {
                CountRenderingGesture();
            }
        }
        /// <summary>
        /// “渲染质量”连点（或 A8 连按）10 次进入自定义档位，与原设置页相同
        /// </summary>
        void CountRenderingGesture()
        {
            if (CurrentMenu.Instance is not RenderingOptions rendering || rendering.CompatibilityMode ||
                rendering.RenderQuality == RenderingQualityOption.Custom)
            {
                ResetRenderingGesture();
                return;
            }
            var now = Time.unscaledTime;
            if (now - _lastRenderingTapTime > RENDERING_ADVANCED_TAP_GAP_SEC)
            {
                _renderingTapCount = 0;
            }
            _lastRenderingTapTime = now;
            _renderingTapCount++;
            if (_renderingTapCount < RENDERING_ADVANCED_TAP_COUNT)
            {
                return;
            }
            ResetRenderingGesture();
            if (rendering.EnterCustomMode())
            {
                GameManager.ApplyRenderingSettings();
                GameManager.RequestSave(this);
                RenderRows(true);
            }
        }
        void ResetRenderingGesture()
        {
            _renderingTapCount = 0;
        }
        #endregion

        #region Input & navigation
        /// <summary>
        /// 机台按键（实体按键与机台触摸；屏幕触摸已不模拟按键）：A2/A7 切分类，A3/A6 选选项，
        /// E4/B4/B3 与 E6/B5/B6 改值，A4/A5 返回，A1 校准，A8 为渲染质量的隐藏手势
        /// </summary>
        void HandleCabinetInput()
        {
            if (InputManager.IsButtonClickCompletedInThisFrame(ButtonZone.A4) ||
                InputManager.IsButtonClickCompletedInThisFrame(ButtonZone.A5))
            {
                RequestExit();
                return;
            }
            if (InputManager.IsButtonClickCompletedInThisFrame(ButtonZone.A1) ||
                InputManager.IsSensorClickCompletedInThisFrame(SensorArea.A1))
            {
                RequestCalibrator();
                return;
            }
            var deltaTime = Time.unscaledDeltaTime;
            var advancedPressed = InputManager.IsButtonPressedInThisFrame(ButtonZone.A8) ||
                                  InputManager.IsSensorPressedInThisFrame(SensorArea.A8);
            var advancedHeld = InputManager.CheckButtonStatusInThisFrame(ButtonZone.A8, SwitchStatus.On) ||
                               InputManager.CheckSensorStatusInThisFrame(SensorArea.A8, SwitchStatus.On);
            if (advancedPressed && !_advancedInputHeld && _selectedRow < _rows.Count &&
                _rows[_selectedRow].Data.PropertyInfo.Name == nameof(RenderingOptions.RenderQuality))
            {
                CountRenderingGesture();
            }
            _advancedInputHeld = advancedHeld;

            var nextCategory = InputManager.CheckButtonStatusInThisFrame(ButtonZone.A2, SwitchStatus.On);
            var previousCategory = InputManager.CheckButtonStatusInThisFrame(ButtonZone.A7, SwitchStatus.On);
            if (_categoryInput.Update(nextCategory, previousCategory, deltaTime, 0.7f, 0.2f, out var categoryDirection))
            {
                SelectMenu(FindVisibleMenu(_menuIndex, categoryDirection), null);
                return;
            }
            var nextOption = InputManager.CheckButtonStatusInThisFrame(ButtonZone.A3, SwitchStatus.On);
            var previousOption = InputManager.CheckButtonStatusInThisFrame(ButtonZone.A6, SwitchStatus.On);
            if (_optionInput.Update(nextOption, previousOption, deltaTime, 0.7f, 0.2f, out var optionDirection))
            {
                SelectRow(_selectedRow + optionDirection, true);
                return;
            }
            var increase = InputManager.CheckSensorStatusInThisFrame(SensorArea.E4, SwitchStatus.On) ||
                           InputManager.CheckSensorStatusInThisFrame(SensorArea.B4, SwitchStatus.On) ||
                           InputManager.CheckSensorStatusInThisFrame(SensorArea.B3, SwitchStatus.On);
            var decrease = InputManager.CheckSensorStatusInThisFrame(SensorArea.E6, SwitchStatus.On) ||
                           InputManager.CheckSensorStatusInThisFrame(SensorArea.B5, SwitchStatus.On) ||
                           InputManager.CheckSensorStatusInThisFrame(SensorArea.B6, SwitchStatus.On);
            var speed = MajEnv.Settings.Debug.MenuOptionIterationSpeed;
            var interval = 1f / (speed is 0 ? 15 : speed);
            if (_valueInput.Update(increase, decrease, deltaTime, 0.4f, interval, out var valueDirection))
            {
                ChangeValue(_selectedRow, valueDirection);
            }
        }
        void RequestExit()
        {
            if (_isLeaving || !_manager.IsReady)
            {
                return;
            }
            _isLeaving = true;
            _manager.RequestExit();
        }
        void RequestCalibrator()
        {
            if (_isLeaving || !_manager.IsReady)
            {
                return;
            }
            _isLeaving = true;
            _manager.RequestCalibrator();
        }
        #endregion
    }
}
