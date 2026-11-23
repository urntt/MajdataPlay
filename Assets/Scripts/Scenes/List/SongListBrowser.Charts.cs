#nullable enable
using System;
using Cysharp.Threading.Tasks;
using MajdataPlay.Diagnostics;
using MajdataPlay.Import;
using MajdataPlay.IO.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MajdataPlay.Scenes.List
{
    internal sealed partial class SongListBrowser
    {
        /// <summary>
        /// The height of the sidebar footer holding Import Charts and Refresh Charts, margins included.
        /// </summary>
        const float SIDEBAR_FOOTER_HEIGHT = 86f;
        /// <summary>
        /// The height of the buttons in the sidebar footer.
        /// </summary>
        const float SIDEBAR_ACTION_HEIGHT = 54f;
        /// <summary>
        /// The width of the export and import dialogs.
        /// </summary>
        const float CHART_DIALOG_WIDTH = 480f;
        /// <summary>
        /// The height of one option row in the export and import dialogs, gap included.
        /// </summary>
        const float CHART_OPTION_HEIGHT = 64f;
        /// <summary>
        /// The most archives the import dialog shows without scrolling.
        /// </summary>
        const int IMPORT_VISIBLE_ROWS = 5;
        /// <summary>
        /// How long the toast stays while an export runs; it is replaced when the export ends.
        /// </summary>
        const float EXPORT_TOAST_DURATION_SEC = 3600f;

        /// <summary>
        /// The formats offered by the export dialog, in the order shown.
        /// </summary>
        static readonly ChartExportFormat[] EXPORT_FORMATS = { ChartExportFormat.Zip, ChartExportFormat.Adx, ChartExportFormat.Pez };

        /// <summary>
        /// The holder of the export button on the detail page, left of the feedback button; shown only for local charts.
        /// </summary>
        RectTransform _detailExportHolder = null!;
        /// <summary>
        /// The icon of the export button on the detail page.
        /// </summary>
        Image _detailExportIcon = null!;
        /// <summary>
        /// Whether a chart is being exported; a second export waits until it ends.
        /// </summary>
        bool _isExporting;

        /// <summary>
        /// Creates the footer at the bottom of the sidebar with Import Charts and Refresh Charts side by side.
        /// </summary>
        /// <param name="sidebar">The sidebar; its category list must leave <see cref="SIDEBAR_FOOTER_HEIGHT"/> free at the bottom.</param>
        void BuildSidebarFooter(RectTransform sidebar)
        {
            var footer = CreateRect("Footer", sidebar);
            footer.anchorMin = Vector2.zero;
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.offsetMin = Vector2.zero;
            footer.offsetMax = new Vector2(0f, SIDEBAR_FOOTER_HEIGHT);
            var line = CreateImage(footer, LINE_COLOR, 0f);
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = new Vector2(1f, 1f);
            line.rectTransform.offsetMin = new Vector2(16f, -1f);
            line.rectTransform.offsetMax = new Vector2(-17f, 0f);

            var margin = (SIDEBAR_FOOTER_HEIGHT - SIDEBAR_ACTION_HEIGHT) * 0.5f;
            var import = CreateSidebarAction(footer, UiSprites.Import, "MAJTEXT_LIST_IMPORT_CHARTS".i18n(), ImportCharts);
            var importRect = (RectTransform)import.transform;
            importRect.anchorMin = Vector2.zero;
            importRect.anchorMax = new Vector2(0.5f, 0f);
            importRect.pivot = new Vector2(0.5f, 0f);
            importRect.offsetMin = new Vector2(16f, margin);
            importRect.offsetMax = new Vector2(-4f, margin + SIDEBAR_ACTION_HEIGHT);
            var refresh = CreateSidebarAction(footer, UiSprites.Retry, "MAJTEXT_LIST_REFRESH_CHARTS".i18n(), RefreshCharts);
            var refreshRect = (RectTransform)refresh.transform;
            refreshRect.anchorMin = new Vector2(0.5f, 0f);
            refreshRect.anchorMax = new Vector2(1f, 0f);
            refreshRect.pivot = new Vector2(0.5f, 0f);
            refreshRect.offsetMin = new Vector2(4f, margin);
            refreshRect.offsetMax = new Vector2(-17f, margin + SIDEBAR_ACTION_HEIGHT);
        }
        /// <summary>
        /// Creates a sidebar footer button with an icon on the left and a label that shrinks to fit.
        /// </summary>
        /// <param name="parent">The footer; place the button afterwards.</param>
        /// <param name="icon">The white icon, tinted with the accent color.</param>
        /// <param name="label">The label.</param>
        /// <param name="onClick">Called when the button is tapped.</param>
        /// <returns>The button.</returns>
        Button CreateSidebarAction(RectTransform parent, Sprite icon, string label, UnityAction onClick)
        {
            const float ICON_SIZE = 24f;
            const float ICON_LEFT = 12f;
            var button = CreateButton(parent, label, PAGE_COLOR, TEXT_COLOR, 17f, onClick);
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableAutoSizing = true;
            text.fontSizeMin = 11f;
            text.fontSizeMax = 17f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.offsetMin = new Vector2(ICON_LEFT + ICON_SIZE + 8f, 4f);
            text.rectTransform.offsetMax = new Vector2(-6f, -4f);
            var image = CreateImage(button.transform, ACCENT_COLOR, 0f);
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
            PlaceMiddleLeft(image.rectTransform, ICON_LEFT, ICON_SIZE, ICON_SIZE);
            return button;
        }
        /// <summary>
        /// Opens the system file picker for chart archives, or on desktops the dialog listing the import folder.
        /// </summary>
        void ImportCharts()
        {
            if (_isLeaving)
            {
                return;
            }
            if (ChartImporter.TryShowPicker())
            {
                return;
            }
            ShowImportFolderModal();
        }
        /// <summary>
        /// Shows the import dialog of desktops: the .zip, .adx and .pez files in the import folder, newest first, each imported
        /// with a tap, and Open Folder to put new files there.
        /// </summary>
        void ShowImportFolderModal()
        {
            var archives = ChartImporter.ListImportFolder();
            var folder = ChartImporter.GetImportFolder();
            var rows = Mathf.Clamp(archives.Count, 1, IMPORT_VISIBLE_ROWS);
            var listTop = 86f + 40f;
            var listHeight = rows * CHART_OPTION_HEIGHT;
            var height = listTop + listHeight + 16f + 74f;
            var panel = CreateModalPanel(CHART_DIALOG_WIDTH, height);
            var title = CreateText(panel, "MAJTEXT_LIST_IMPORT_CHARTS".i18n(), 22f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            PlaceRow(title.rectTransform, 28f, 24f, 28f, 38f);
            var hint = CreateText(panel, "MAJTEXT_LIST_IMPORT_FOLDER_HINT".i18n(), 16f, MUTED_COLOR, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 12f;
            hint.fontSizeMax = 16f;
            PlaceRow(hint.rectTransform, 28f, 70f, 28f, 50f);

            if (archives.Count == 0)
            {
                var empty = CreateText(panel, "MAJTEXT_LIST_IMPORT_FOLDER_EMPTY".i18n(), 18f, MUTED_COLOR, TextAlignmentOptions.Center, FontStyles.Normal);
                PlaceRow(empty.rectTransform, 28f, listTop, 28f, listHeight);
            }
            else
            {
                var listHolder = CreateRect("Archives", panel);
                PlaceRow(listHolder, 28f, listTop, 28f, listHeight);
                var content = CreateScroll(listHolder, 0f, 0f, 0f, 0f, out _);
                var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = CHART_OPTION_HEIGHT - 54f;
                layout.childControlWidth = true;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                foreach (var archive in archives)
                {
                    var entry = archive;
                    var item = CreateButton(content, entry.Name, PAGE_COLOR, TEXT_COLOR, 18f, () => ImportArchive(entry));
                    ((RectTransform)item.transform).sizeDelta = new Vector2(0f, 54f);
                    var label = item.GetComponentInChildren<TextMeshProUGUI>();
                    label.alignment = TextAlignmentOptions.MidlineLeft;
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                    label.rectTransform.offsetMin = new Vector2(16f, 4f);
                    label.rectTransform.offsetMax = new Vector2(-16f, -4f);
                }
            }

            var buttonWidth = (CHART_DIALOG_WIDTH - 28f * 2f - 12f) * 0.5f;
            var buttonTop = height - 74f;
            var cancel = CreateButton(panel, "MAJTEXT_LIST_CANCEL".i18n(), PAGE_COLOR, TEXT_COLOR, 18f, HideModal);
            PlaceTopLeft((RectTransform)cancel.transform, 28f, buttonTop, buttonWidth, 50f);
            var open = CreateButton(panel, "MAJTEXT_LIST_OPEN_FOLDER".i18n(), ACCENT_SOFT_COLOR, ACCENT_COLOR, 18f, () => NativeShare.OpenFolder(folder.Location));
            open.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            PlaceTopLeft((RectTransform)open.transform, 28f + buttonWidth + 12f, buttonTop, buttonWidth, 50f);
        }
        /// <summary>
        /// Queues an archive from the import folder; the importer then leaves the list, imports it and comes back.
        /// </summary>
        /// <param name="entry">The archive in the import folder.</param>
        void ImportArchive(FileSystemEntry entry)
        {
            if (_isLeaving)
            {
                return;
            }
            HideModal();
            SaveState(null, _detailLevel);
            LeaveList();
            ChartImporter.QueueImport(entry);
        }
        /// <summary>
        /// Rescans the local and online charts; the list scene is reloaded at the same category and page.
        /// </summary>
        void RefreshCharts()
        {
            if (_isLeaving)
            {
                return;
            }
            HideModal();
            SaveState(null, _detailLevel);
            LeaveList();
            _listManager.RefreshCharts();
        }
        /// <summary>
        /// Creates the export button of the detail page, hidden until a local chart is shown.
        /// </summary>
        void BuildDetailExportButton()
        {
            _detailExportHolder = LightUi.CreateActionButton(_detailView, UiSprites.Share, PAPER_COLOR, ACCENT_COLOR, 0.16f, ShowExportModal,
                                                             out _, out _detailExportIcon);
            _detailExportHolder.gameObject.SetActive(false);
        }
        /// <summary>
        /// Shows the export dialog of the chart on the detail page: ZIP, ADX, or PEZ of the chosen difficulty.
        /// </summary>
        void ShowExportModal()
        {
            if (_detailSong is not SongDetail song || _isDetailClosing)
            {
                return;
            }
            if (_isExporting)
            {
                ShowToast("MAJTEXT_LIST_EXPORTING".i18n(), EXPORT_TOAST_DURATION_SEC);
                return;
            }
            var level = _detailLevel;
            var height = 86f + EXPORT_FORMATS.Length * CHART_OPTION_HEIGHT + 16f + 74f;
            var panel = CreateModalPanel(CHART_DIALOG_WIDTH, height);
            var title = CreateText(panel, "MAJTEXT_LIST_EXPORT_CHART".i18n(), 22f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            PlaceRow(title.rectTransform, 28f, 24f, 28f, 38f);
            for (var i = 0; i < EXPORT_FORMATS.Length; i++)
            {
                var format = EXPORT_FORMATS[i];
                var labelText = format == ChartExportFormat.Pez
                    ? $"MAJTEXT_LIST_EXPORT_FORMAT_{format}".i18n(ScoreExchange.GetDifficultyName(level))
                    : $"MAJTEXT_LIST_EXPORT_FORMAT_{format}".i18n();
                var option = CreateButton(panel, labelText, PAGE_COLOR, TEXT_COLOR, 19f, () =>
                {
                    HideModal();
                    ExportChartAsync(song, format, level).Forget();
                });
                var label = option.GetComponentInChildren<TextMeshProUGUI>();
                label.enableAutoSizing = true;
                label.fontSizeMin = 13f;
                label.fontSizeMax = 19f;
                PlaceRow((RectTransform)option.transform, 28f, 86f + i * CHART_OPTION_HEIGHT, 28f, 54f);
            }
            var cancel = CreateButton(panel, "MAJTEXT_LIST_CANCEL".i18n(), PAPER_COLOR, MUTED_COLOR, 18f, HideModal);
            PlaceRow((RectTransform)cancel.transform, 28f, height - 74f, 28f, 50f);
        }
        /// <summary>
        /// Exports a local chart and opens the share sheet, with a toast while it runs and a dialog with the reason when it fails.
        /// </summary>
        /// <param name="song">The local chart.</param>
        /// <param name="format">The archive format.</param>
        /// <param name="level">The difficulty a .pez holds.</param>
        /// <returns>A task that completes when the export has ended.</returns>
        async UniTaskVoid ExportChartAsync(SongDetail song, ChartExportFormat format, ChartLevel level)
        {
            var token = this.GetCancellationTokenOnDestroy();
            _isExporting = true;
            ShowToast((format == ChartExportFormat.Pez ? "MAJTEXT_LIST_EXPORTING_PEZ" : "MAJTEXT_LIST_EXPORTING").i18n(), EXPORT_TOAST_DURATION_SEC);
            try
            {
                var path = await ChartExporter.ExportAndShareAsync(song, format, level, token);
                await UniTask.SwitchToMainThread(token);
                MajDebug.LogInfo(nameof(SongListBrowser), $"Exported {song.Title} as {format} to {path}");
                ShowToast("MAJTEXT_LIST_EXPORT_DONE".i18n());
            }
            catch (OperationCanceledException)
            {
                // The list was left while exporting; nothing is shown any more.
            }
            catch (Exception e)
            {
                await UniTask.SwitchToMainThread();
                if (this == null)
                {
                    return;
                }
                MajDebug.LogError(nameof(SongListBrowser), $"Failed to export {song.Title} as {format}: {e}");
                _toast.gameObject.SetActive(false);
                _toastRemaining = 0f;
                ShowMessageModal("MAJTEXT_LIST_EXPORT_FAILED".i18n(), e.Message);
            }
            finally
            {
                _isExporting = false;
            }
        }
        /// <summary>
        /// Shows a dialog with a title, a message that may span several lines and an OK button.
        /// </summary>
        /// <param name="titleText">The title.</param>
        /// <param name="message">The message, such as the reason an export failed.</param>
        void ShowMessageModal(string titleText, string message)
        {
            const float HEIGHT = 300f;
            var panel = CreateModalPanel(CHART_DIALOG_WIDTH, HEIGHT);
            var title = CreateText(panel, titleText, 22f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            PlaceRow(title.rectTransform, 28f, 24f, 28f, 38f);
            var body = CreateText(panel, message, 17f, MUTED_COLOR, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            body.richText = false;
            body.enableAutoSizing = true;
            body.fontSizeMin = 11f;
            body.fontSizeMax = 17f;
            body.overflowMode = TextOverflowModes.Ellipsis;
            PlaceRow(body.rectTransform, 28f, 74f, 28f, HEIGHT - 74f - 90f);
            var ok = CreateButton(panel, "MAJTEXT_LIST_OK".i18n(), ACCENT_COLOR, Color.white, 18f, HideModal);
            ok.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            PlaceRow((RectTransform)ok.transform, 28f, HEIGHT - 74f, 28f, 50f);
        }
    }
}
