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
        const float VALUE_AREA_WIDTH = 392f;
        const float STEP_BUTTON_WIDTH = 60f;
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
        RectTransform _root = null!;
        CanvasGroup _group = null!;
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
        bool _lastCompatibilityMode;
        RenderingQualityOption _lastRenderQuality;
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

            _root = LightUi.CreateOverlayCanvas(transform, "SettingBrowserCanvas", 30, out _group);
            LightUi.FollowTransition(_group, false);
            BuildLayout();

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
            // 渲染分类：兼容模式或质量档位变化后，可见的选项不同
            if (CurrentMenu.Instance is RenderingOptions rendering &&
                (rendering.CompatibilityMode != _lastCompatibilityMode || rendering.RenderQuality != _lastRenderQuality))
            {
                RenderRows(true);
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
            var page = LightUi.CreateImage(_root, LightUi.PAGE, 0f);
            LightUi.Stretch(page.rectTransform);

            // 左栏：标题栏与分类
            var sidebar = LightUi.CreateRect("Sidebar", _root);
            sidebar.anchorMin = Vector2.zero;
            sidebar.anchorMax = new Vector2(0.25f, 1f);
            sidebar.offsetMin = Vector2.zero;
            sidebar.offsetMax = Vector2.zero;
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

            // 右侧：分类名、校准与完成，下面是选项列表
            var main = LightUi.CreateRect("Main", _root);
            main.anchorMin = new Vector2(0.25f, 0f);
            main.anchorMax = Vector2.one;
            main.offsetMin = new Vector2(32f, 0f);
            main.offsetMax = new Vector2(-20f, 0f);
            var topRow = LightUi.CreateRect("TopRow", main);
            LightUi.PlaceRow(topRow, 0f, TOP_PADDING, 0f, TOPBAR_HEIGHT);
            _categoryTitle = _ui.CreateText(topRow, string.Empty, 30f, LightUi.TEXT, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            LightUi.Stretch(_categoryTitle.rectTransform);
            _categoryTitle.rectTransform.offsetMax = new Vector2(-280f, 0f);
            var doneButton = _ui.CreateButton(topRow, "MAJTEXT_SETTING_DONE".i18n(), LightUi.ACCENT, Color.white, 20f, RequestExit);
            LightUi.PlaceTopRight((RectTransform)doneButton.transform, 0f, 2f, 124f, 56f);
            var calibrateButton = _ui.CreateButton(topRow, "MAJTEXT_SETTING_CALIBRATE".i18n(), LightUi.PAPER, LightUi.TEXT, 20f, RequestCalibrator);
            LightUi.PlaceTopRight((RectTransform)calibrateButton.transform, 136f, 2f, 124f, 56f);

            var listPane = LightUi.CreateRect("ListPane", main);
            LightUi.Stretch(listPane);
            listPane.offsetMax = new Vector2(0f, -LIST_TOP);
            _listContent = LightUi.CreateScroll(listPane, 0f, 0f, 0f, 0f, out _listScroll);
            var listLayout = _listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.padding = new RectOffset(0, (int)(SCROLLBAR_WIDTH + 14f), 4, 48);
            listLayout.spacing = 12f;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            _listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            LightUi.AddScrollbar(_listScroll, SCROLLBAR_WIDTH);
        }
        void RenderSidebar()
        {
            LightUi.ClearChildren(_sidebarContent);
            var menus = _manager.Menus;
            for (var i = 0; i < menus.Count; i++)
            {
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
            return $"MAJSETTING_CATEGORY_{menu.Name}".i18n();
        }
        static string? FindSelectedSongTitle()
        {
            var hash = MajEnv.RuntimeConfig?.List.SelectedSongHash;
            if (string.IsNullOrEmpty(hash))
            {
                return null;
            }
            foreach (var collection in SongStorage.Collections)
            {
                foreach (var song in collection)
                {
                    if (song.Hash == hash)
                    {
                        return song.Title;
                    }
                }
            }
            return null;
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
            if (menu.Instance is RenderingOptions rendering)
            {
                _lastCompatibilityMode = rendering.CompatibilityMode;
                _lastRenderQuality = rendering.RenderQuality;
            }
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
        /// 渲染分类的可见规则与原设置页相同：兼容模式只显示常用项；普通档位只显示常用项与渲染质量；自定义显示全部
        /// </summary>
        static bool IsVisible(Menu menu, PropertyInfo property)
        {
            if (menu.Instance is not RenderingOptions rendering)
            {
                return true;
            }
            var name = property.Name;
            var isCommon = name is nameof(RenderingOptions.FPSLimit) or nameof(RenderingOptions.VSync) or nameof(RenderingOptions.CompatibilityMode);
            if (rendering.CompatibilityMode)
            {
                return isCommon;
            }
            if (rendering.RenderQuality != RenderingQualityOption.Custom)
            {
                return isCommon || name == nameof(RenderingOptions.RenderQuality);
            }
            return true;
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

            var border = LightUi.CreateImage(row, LightUi.LINE, 15f);
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            LightUi.Stretch(border.rectTransform, -1.5f);
            var background = LightUi.CreateImage(row, LightUi.PAPER, 14f);
            background.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            LightUi.Stretch(background.rectTransform);
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
            valueElement.minWidth = VALUE_AREA_WIDTH;
            valueElement.preferredWidth = VALUE_AREA_WIDTH;
            var isReadOnly = data.IsReadOnly;
            CreateStepButton(valueArea, "−", rowIndex, -1, isReadOnly);
            var value = _ui.CreateText(valueArea, data.LocalizedValueText, 22f, LightUi.ACCENT, TextAlignmentOptions.Center, FontStyles.Bold);
            var valueTextElement = value.gameObject.AddComponent<LayoutElement>();
            valueTextElement.preferredWidth = VALUE_AREA_WIDTH - STEP_BUTTON_WIDTH * 2f - 16f;
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
                LightUi.Stretch(border.rectTransform, isSelected ? -2.5f : -1.5f);
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
                SelectMenu(_menuIndex + categoryDirection, null);
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
