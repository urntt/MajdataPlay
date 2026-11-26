#if UNITY_ANDROID && !UNITY_EDITOR
#define ANDROID_RUNTIME
#endif
using Cysharp.Threading.Tasks;
using MajdataPlay.Diagnostics;
using MajdataPlay.IO.Storage;
#if UNITY_ANDROID
using MajdataPlay.Platform.Android;
using MajdataPlay.Platform.Android.Runtime;
using MajdataPlay.Platform.Android.Runtime.Bindings.Android.Content;
using JavaUri = MajdataPlay.Platform.Android.Runtime.Bindings.Android.Net.Uri;
using MajdataPlay.Platform.Android.Storage;
#endif
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

#nullable enable
namespace MajdataPlay.Import
{
    /// <summary>How the import button lets the player choose what to import on this platform.</summary>
    internal enum ChartImportPickerMode
    {
        /// <summary>No system picker: the list shows the files and folders in the import folder (desktops).</summary>
        ImportFolder,

        /// <summary>One system picker for files and folders (iOS).</summary>
        FilesAndFolders,

        /// <summary>Separate system pickers for a file and for a folder (Android).</summary>
        FileOrFolder,
    }

    /// <summary>
    /// Imports charts from whatever the player hands the game: folders, ZIP archives under any name (.zip, .adx, .pez, ...),
    /// archives inside archives, simai and Phira charts, telling all of them apart by content rather than by name.
    /// </summary>
    /// <remarks>
    /// Sources are queued by the platforms (iOS "Open in" and document picker, Android share, shortcuts and storage picker, the
    /// desktop import folder) and imported in the Empty scene. Every chart is copied with its media renamed to what the game
    /// reads (<see cref="ChartResourceNormalizer"/>); Phira charts are converted with URC first. What was imported and why
    /// anything failed is collected in a <see cref="ChartImportReport"/>, shown on the loading screen and, through
    /// <see cref="TakeLastReport"/>, by the song list.
    /// </remarks>
    internal static class ChartImporter
    {
        /// <summary>The name of the folder below <see cref="MajEnv.RootDirectory"/> whose files and folders can be imported from the list on desktops.</summary>
        internal const string ImportFolderName = "ChartImports";

        /// <summary>The collection receiving charts that are not grouped in a named collection.</summary>
        private const string ImportCollectionName = "Import";

        /// <summary>The deepest nesting of archives inside archives that is unpacked.</summary>
        internal const int MaxNestedArchives = 3;

        /// <summary>The folder name iOS copies picked and opened files into; those copies are deleted after the import.</summary>
        private const string InboxFolderName = "maicharts_inbox";

        /// <summary>How long the loading screen shows the result, in milliseconds, plus <see cref="ResultDelayPerFailureMs"/> per failure.</summary>
        private const int ResultDelayMs = 3000;

        /// <summary>How much longer the loading screen shows the result for each failure, in milliseconds.</summary>
        private const int ResultDelayPerFailureMs = 1500;

        /// <summary>The longest the loading screen shows the result, in milliseconds.</summary>
        private const int MaxResultDelayMs = 9000;

#if UNITY_IOS && !UNITY_EDITOR
        /// <summary>Shows the iOS document picker (Assets/Plugins/iOS/MajDocumentPicker.mm); picked files and folders arrive through <c>GameManager.IOS_OnFileOpen</c>.</summary>
        [DllImport("__Internal")]
        private static extern void MajDocumentPicker_Show();
#endif

        /// <summary>Retains the collection handle receiving charts without an explicit collection.</summary>
        private static StorageDirectory s_importRoot = null!;
        private static ValueTask s_importTask = UniTask.CompletedTask;
        private static readonly ConcurrentQueue<PendingImport> s_pendingImportTasks = new ConcurrentQueue<PendingImport>();

        /// <summary>The report of the import that is running.</summary>
        private static ChartImportReport s_report = new();

        /// <summary>The report of the last finished import, until the song list takes it.</summary>
        private static ChartImportReport? s_lastReport;

        /// <summary>When the last import finished, in seconds of <see cref="Time.realtimeSinceStartup"/>.</summary>
        private static float s_lastReportTime;

        /// <summary>How long a report waits for the touch song list; the classic list does not show it, and it goes stale.</summary>
        private const float ReportLifetimeSec = 120f;

        /// <summary>Describes one queued import source and whether consuming it may delete the source.</summary>
        /// <remarks>
        /// A shared or platform-provided temporary file is deleted after the import, while a document the user picked
        /// through the storage picker belongs to its provider and must survive the import.
        /// </remarks>
        private readonly struct PendingImport
        {
            /// <summary>Gets the queued storage entry.</summary>
            public FileSystemEntry Entry { get; }

            /// <summary>Gets whether the source is a consumed temporary file that is deleted after the import.</summary>
            public bool DeleteSourceAfterImport { get; }

            /// <summary>Initializes one queued import source.</summary>
            /// <param name="entry">The storage entry to import.</param>
            /// <param name="deleteSourceAfterImport">Whether the source is a temporary file consumed by the import.</param>
            public PendingImport(FileSystemEntry entry, bool deleteSourceAfterImport)
            {
                Entry = entry;
                DeleteSourceAfterImport = deleteSourceAfterImport;
            }
        }

#if ANDROID_RUNTIME
        /// <summary>Identifies the launcher shortcut that requested an import.</summary>
        private enum ShortcutImportKind
        {
            /// <summary>No shortcut import is pending.</summary>
            None,

            /// <summary>The user asked to import a chart archive.</summary>
            Archive,

            /// <summary>The user asked to import a chart folder.</summary>
            Folder,
        }

        /// <summary>Matches the archive shortcut action declared in the shortcuts Android Library plug-in manifest.</summary>
        private const string ImportArchiveAction = "net.majdata.majdataplay.action.IMPORT_ARCHIVE";

        /// <summary>Matches the folder shortcut action declared in the shortcuts Android Library plug-in manifest.</summary>
        private const string ImportFolderAction = "net.majdata.majdataplay.action.IMPORT_FOLDER";

        /// <summary>Stores the shortcut import whose picker has not been shown yet.</summary>
        private static ShortcutImportKind s_pendingShortcut = ShortcutImportKind.None;

        /// <summary>Tracks the running shortcut picker so a second picker is never shown concurrently.</summary>
        private static ValueTask s_pickerTask = UniTask.CompletedTask;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
#if ANDROID_RUNTIME
            AndroidRuntime.OnNewIntent += OnNewIntent;
#endif
        }


        /// <summary>Processes queued imports and any pending startup shortcut.</summary>
        /// <returns>An operation that completes after the startup import flow.</returns>
        public static async ValueTask OnStartAsync()
        {
#if ANDROID_RUNTIME
            var intent = AndroidRuntime.CurrentActivity.GetIntent();
            if (intent != null)
            {
                OnNewIntent(null, intent);
            }
            if (s_pendingShortcut != ShortcutImportKind.None)
            {
                // A launcher shortcut must pick its source before the import can start.
                s_pickerTask = ShowShortcutPickerAsync();
                await s_pickerTask;
            }
#endif
            if (s_pendingImportTasks.Count != 0)
            {
                s_importTask = Import();
                await s_importTask;
            }
        }

        /// <summary>Gets how the import button lets the player choose what to import on this platform.</summary>
        internal static ChartImportPickerMode PickerMode
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return ChartImportPickerMode.FilesAndFolders;
#elif ANDROID_RUNTIME
                return ChartImportPickerMode.FileOrFolder;
#else
                return ChartImportPickerMode.ImportFolder;
#endif
            }
        }

        /// <summary>
        /// Shows the system picker so the player can choose files or folders to import, whatever they are named.
        /// </summary>
        /// <remarks>
        /// On iOS one picker takes files and folders; the native picker copies them and queues them through
        /// <c>GameManager.IOS_OnFileOpen</c>. On Android the storage picker for a file or for a folder runs from
        /// <see cref="OnUpdate"/>, like the launcher shortcuts. Desktops have no picker at run time and use
        /// <see cref="ListImportFolder"/> instead.
        /// </remarks>
        /// <param name="isFolder">On Android, whether to pick a folder rather than a file; ignored elsewhere.</param>
        /// <returns>True when a picker is shown; false when this platform has none.</returns>
        internal static bool ShowPicker(bool isFolder)
        {
#if UNITY_IOS && !UNITY_EDITOR
            MajDocumentPicker_Show();
            return true;
#elif ANDROID_RUNTIME
            // OnUpdate shows the storage picker on the next frame, exactly as for the launcher shortcuts.
            s_pendingShortcut = isFolder ? ShortcutImportKind.Folder : ShortcutImportKind.Archive;
            return true;
#else
            return false;
#endif
        }

        /// <summary>Gets the folder whose files and folders can be imported from the list, creating it when it is missing.</summary>
        /// <returns>The <see cref="ImportFolderName"/> folder below <see cref="MajEnv.RootDirectory"/>.</returns>
        internal static StorageDirectory GetImportFolder()
        {
            return MajEnv.RootDirectory.CreateDirectory(ImportFolderName);
        }

        /// <summary>Lists the files and folders in the import folder, newest first; what they hold is found out when they are imported.</summary>
        /// <returns>The entries; empty when the folder cannot be read.</returns>
        internal static List<FileSystemEntry> ListImportFolder()
        {
            var entries = new List<FileSystemEntry>();
            try
            {
                foreach (var entry in GetImportFolder().EnumerateEntries())
                {
                    if (!ChartPackageScanner.IsExcluded(entry))
                    {
                        entries.Add(entry);
                    }
                }
            }
            catch (Exception e)
            {
                MajDebug.LogWarning(nameof(ChartImporter), $"Failed to list the import folder: {e.Message}");
            }
            entries.Sort((x, y) => Nullable.Compare(y.LastWriteTimeUtc, x.LastWriteTimeUtc));
            return entries;
        }

        /// <summary>Queues a file or folder the player chose in the import folder; it is kept after the import.</summary>
        /// <param name="entry">The file or folder.</param>
        internal static void QueueImport(FileSystemEntry entry)
        {
            s_pendingImportTasks.Enqueue(new PendingImport(entry, deleteSourceAfterImport: false));
        }

        /// <summary>Takes the report of the last finished import, so that it is shown once.</summary>
        /// <returns>The report, or null when no import finished in the last <see cref="ReportLifetimeSec"/> seconds since the last call.</returns>
        internal static ChartImportReport? TakeLastReport()
        {
            var report = s_lastReport;
            s_lastReport = null;
            return report is not null && Time.realtimeSinceStartup - s_lastReportTime <= ReportLifetimeSec ? report : null;
        }

        /// <summary>Queues a file or folder that a platform handed to the game, such as an iOS "Open in" or picked document.</summary>
        /// <param name="tempFilePath">The local path of the file or folder.</param>
        public static void AddImportTask(string tempFilePath)
        {
            var entry = FileSystem.GetEntry(tempFilePath);
            if (entry != null)
            {
                // The iOS bridges copy what they receive into the inbox; the import consumes those copies.
                s_pendingImportTasks.Enqueue(new PendingImport(entry, deleteSourceAfterImport: IsInboxCopy(tempFilePath)));
            }
        }
        public static void OnUpdate()
        {
#if ANDROID_RUNTIME
            if (s_pendingShortcut != ShortcutImportKind.None && s_pickerTask.IsCompleted)
            {
                // A shortcut arrived while the player was already running; reuse this instance instead of a new activity.
                s_pickerTask = ShowShortcutPickerAsync();
                return;
            }
#endif
            if (SceneSwitcher.CurrentScene == MajScenes.Empty ||
                s_pendingImportTasks.Count == 0 ||
                !s_importTask.IsCompleted)
            {
                return;
            }
            s_importTask = Import();
        }
        /// <summary>Imports every queued source, refreshes the local chart list and shows what happened.</summary>
        /// <remarks>
        /// Each source is told apart by content (<see cref="ImportSourceAsync"/>). The result stays on the loading screen a
        /// little longer for each failure and is kept for the song list (<see cref="TakeLastReport"/>).
        /// </remarks>
        /// <returns>An operation that completes after importing the queued entries and refreshing the local chart list.</returns>
        private static async ValueTask Import()
        {
            var nextScene = (MajScenes?)MajScenes.List;
            if (SceneSwitcher.CurrentScene == MajScenes.Init)
            {
                await MajInstances.SceneSwitcher.FadeInAsync();
                nextScene = null;
            }
            else
            {
                while (SceneSwitcher.CurrentScene == MajScenes.Title)
                {
                    nextScene = MajScenes.Login;
                    await UniTask.Yield();
                }
                await MajInstances.SceneSwitcher.SwitchSceneAsync("Empty", false);
                await UniTask.Delay(300);
            }
            s_importRoot = MajEnv.ChartDirectory.CreateDirectory(ImportCollectionName);
            var report = new ChartImportReport();
            s_report = report;
            while (s_pendingImportTasks.TryDequeue(out var pending))
            {
                MajDebug.LogDebug(nameof(ChartImporter), "Got entry: " + pending.Entry.Location);
                await ImportSourceAsync(pending.Entry, pending.DeleteSourceAfterImport);
            }
            var progress = new Progress<string>();
            progress.ProgressChanged += (o, e) =>
            {
                MajInstances.SceneSwitcher.SetLoadingText(e);
            };
            var task = SongStorage.RefreshLocal(progress);
            while (!task.IsCompleted)
            {
                await UniTask.Yield();
            }
            MajDebug.LogInfo(nameof(ChartImporter), report.ToText(int.MaxValue));
            s_lastReport = report;
            s_lastReportTime = Time.realtimeSinceStartup;
            if (!task.IsCompletedSuccessfully)
            {
                MajInstances.SceneSwitcher.SetLoadingText($"{"MAJTEXT_ERR_SCAN_CHARTS_FAILED".i18n()}\n{report.ToLoadingText()}", Color.red);
            }
            else
            {
                MajInstances.SceneSwitcher.SetLoadingText(report.ToLoadingText(), report.Failures.Count > 0 ? new Color(1f, 0.75f, 0.4f) : Color.white);
            }
            await UniTask.Delay(Math.Min(ResultDelayMs + ResultDelayPerFailureMs * report.Failures.Count, MaxResultDelayMs));
            if (nextScene is MajScenes scene)
            {
                await MajInstances.SceneSwitcher.SwitchSceneAsync(scene.ToString());
            }
        }

        /// <summary>Imports one queued file or folder, recording what happened in the report.</summary>
        /// <param name="entry">The queued entry.</param>
        /// <param name="deleteSourceAfterImport">Whether the source is a consumed temporary copy that is deleted afterwards.</param>
        /// <returns>An operation that completes after the source has been imported or its failure recorded.</returns>
        /// <remarks>Any failure is recorded instead of thrown, so one bad source never stops the others.</remarks>
        private static async ValueTask ImportSourceAsync(FileSystemEntry entry, bool deleteSourceAfterImport)
        {
            try
            {
                var current = FileSystem.GetEntry(entry.Location);
                if (current is null)
                {
                    s_report.AddFailure(entry.Name, ChartImportFailure.NotFound);
                    return;
                }
                if (current.IsDirectory)
                {
                    await ImportFolderAsync(FileSystem.Directory.Open(current.Location), current.Name, current.Name, 0);
                }
                else
                {
                    await ImportFileAsync(FileSystem.File.Open(current.Location), current.Name, 0);
                }
            }
            catch (Exception e)
            {
                MajDebug.LogError(nameof(ChartImporter), $"Import failed: {entry.Location}\n{e}");
                s_report.AddFailure(entry.Name, ChartImportFailure.CopyFailed, e.Message);
            }
            finally
            {
                if (deleteSourceAfterImport)
                {
                    // Only a consumed temporary copy is deleted; a document the user picked must survive.
                    TryDeleteSource(entry);
                }
            }
        }

        /// <summary>Imports one file by what it holds: a ZIP archive is unpacked, anything else is explained.</summary>
        /// <param name="file">The file.</param>
        /// <param name="name">The file name, for the report.</param>
        /// <param name="depth">How many archives the file is nested in.</param>
        /// <returns>An operation that completes after the file has been imported or its failure recorded.</returns>
        private static async ValueTask ImportFileAsync(StorageFile file, string name, int depth)
        {
            var kind = ChartFileSniffer.Sniff(file, name);
            if (kind == ChartFileKind.Zip)
            {
                await ImportArchiveAsync(file, name, depth);
                return;
            }
            if (ChartFileSniffer.IsArchive(kind))
            {
                s_report.AddFailure(name, ChartImportFailure.UnsupportedArchive, ChartFileSniffer.GetFormatName(kind));
            }
            else if (ChartFileSniffer.IsChart(kind))
            {
                s_report.AddFailure(name, ChartImportFailure.LoneChartFile);
            }
            else if (ChartFileSniffer.IsAudio(kind) || ChartFileSniffer.IsImage(kind) || ChartFileSniffer.IsVideo(kind))
            {
                var kindKey = ChartFileSniffer.IsAudio(kind) ? "MAJTEXT_IMPORT_KIND_AUDIO" : ChartFileSniffer.IsImage(kind) ? "MAJTEXT_IMPORT_KIND_IMAGE" : "MAJTEXT_IMPORT_KIND_VIDEO";
                s_report.AddFailure(name, ChartImportFailure.MediaFile, kindKey.i18n());
            }
            else
            {
                s_report.AddFailure(name, ChartImportFailure.UnknownFile);
            }
        }

        /// <summary>Unpacks a ZIP archive (whatever its name) into a private temporary folder and imports what it holds.</summary>
        /// <param name="file">The archive.</param>
        /// <param name="name">The archive name, for the report and as the name of a chart at its root.</param>
        /// <param name="depth">How many archives the archive is nested in.</param>
        /// <returns>An operation that completes after the contents have been imported or the failure recorded.</returns>
        /// <remarks>The temporary folder is always removed.</remarks>
        private static async ValueTask ImportArchiveAsync(StorageFile file, string name, int depth)
        {
            if (depth >= MaxNestedArchives)
            {
                s_report.AddFailure(name, ChartImportFailure.TooDeep);
                return;
            }
            var outputDirectory = MajEnv.TempDirectory.CreateDirectory(Guid.NewGuid().ToString());
            try
            {
                string? encoding;
                try
                {
                    encoding = await ChartArchiveReader.ExtractAsync(file, outputDirectory, share =>
                    {
                        MajInstances.SceneSwitcher.SetLoadingText("MAJTEXT_IMPORT_EXTRACTING".i18n(name, (share * 100).ToString("F0")));
                    });
                }
                catch (ChartArchivePathException e)
                {
                    MajDebug.LogError(nameof(ChartImporter), e.Message);
                    s_report.AddFailure(name, ChartImportFailure.UnsafeArchive, e.EntryName);
                    return;
                }
                catch (InvalidDataException e)
                {
                    s_report.AddFailure(name, ChartImportFailure.BrokenArchive, e.Message);
                    return;
                }
                if (encoding is not null && encoding != "utf-8")
                {
                    s_report.AddWarning(name, ChartImportWarning.LegacyNames, encoding);
                }
                await ImportFolderAsync(outputDirectory, Path.GetFileNameWithoutExtension(name), name, depth + 1);
            }
            finally
            {
                TryDeleteDirectory(outputDirectory);
            }
        }

        /// <summary>Finds and imports every chart in a folder tree, and the archives in it.</summary>
        /// <param name="directory">The folder, which may belong to any storage backend.</param>
        /// <param name="rootName">The name a chart at the root of the folder gets.</param>
        /// <param name="sourceName">The name of the queued source, for the report.</param>
        /// <param name="depth">How many archives the folder is nested in.</param>
        /// <returns>An operation that completes after everything found has been imported or its failure recorded.</returns>
        private static async ValueTask ImportFolderAsync(StorageDirectory directory, string rootName, string sourceName, int depth)
        {
            MajInstances.SceneSwitcher.SetLoadingText("MAJTEXT_IMPORT_SCANNING".i18n(sourceName));
            await UniTask.Yield();
            var scan = ChartPackageScanner.Scan(directory, rootName);
            foreach (var chart in scan.Charts)
            {
                await ImportFoundChartAsync(chart);
            }
            foreach (var archive in scan.Archives)
            {
                await ImportArchiveAsync(archive.File, archive.Name, depth);
            }
            foreach (var archive in scan.UnsupportedArchives)
            {
                s_report.AddFailure(archive.Name, ChartImportFailure.UnsupportedArchive, ChartFileSniffer.GetFormatName(archive.Kind));
            }
            if (scan.Charts.Count == 0 && scan.Archives.Count == 0 && scan.UnsupportedArchives.Count == 0)
            {
                s_report.AddFailure(sourceName, ChartImportFailure.NoChart);
            }
        }

        /// <summary>Imports one chart folder into its collection.</summary>
        /// <param name="chart">The chart folder.</param>
        /// <returns>An operation that completes after the chart has been copied or its failure recorded.</returns>
        private static async ValueTask ImportFoundChartAsync(FoundChart chart)
        {
            var collectionName = chart.Collection is null ? ImportCollectionName : ChartArchiveReader.ToSafeName(chart.Collection);
            // A named collection is reused when it exists, so a package extends it instead of duplicating it.
            var destinationRoot = chart.Collection is null ? s_importRoot : MajEnv.ChartDirectory.CreateDirectory(collectionName);
            if (chart.Format == FoundChartFormat.Simai)
            {
                ImportSimaiChart(chart, destinationRoot, collectionName);
            }
            else
            {
                await ImportPhiraChartAsync(chart, destinationRoot, collectionName);
            }
            await UniTask.Yield();
        }

        /// <summary>Copies a simai chart folder, storing its chart text and media under the names the game reads.</summary>
        /// <param name="chart">The chart folder.</param>
        /// <param name="destinationRoot">The collection folder receiving the chart.</param>
        /// <param name="collectionName">The collection's name, for the report.</param>
        private static void ImportSimaiChart(FoundChart chart, StorageDirectory destinationRoot, string collectionName)
        {
            var chartFile = ChartResourceNormalizer.FindSimaiChart(chart.Files);
            if (chartFile is null)
            {
                s_report.AddFailure(chart.Name, ChartImportFailure.NoChart);
                return;
            }
            var track = ChartResourceNormalizer.PickTrack(chart.Files, null, out var failure, out var detail);
            if (track is null)
            {
                s_report.AddFailure(chart.Name, failure ?? ChartImportFailure.MissingMusic, detail);
                return;
            }
            var cover = ChartResourceNormalizer.PickCover(chart.Files, null);
            var video = ChartResourceNormalizer.PickVideo(chart.Files, out var unsupportedVideo);
            var picks = new[] { track, cover, video };
            var name = ToDirectoryName(chart.Name, "chart");
            MajInstances.SceneSwitcher.SetLoadingText("MAJTEXT_IMPORT_COPYING".i18n(name));
            var destination = CreateUniqueDirectory(destinationRoot, name, out var destinationName);
            string? convertedFrom;
            try
            {
                convertedFrom = ChartResourceNormalizer.CopySimai(chart, chartFile, picks, destination);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                TryDeleteDirectory(destination);
                s_report.AddFailure(chart.Name, ChartImportFailure.CopyFailed, e.Message);
                return;
            }
            s_report.AddChart(destinationName, collectionName);
            if (cover is null)
            {
                s_report.AddWarning(destinationName, ChartImportWarning.NoCover);
            }
            if (unsupportedVideo is not null)
            {
                s_report.AddWarning(destinationName, ChartImportWarning.UnsupportedVideo, unsupportedVideo);
            }
            if (ChartResourceNormalizer.DescribeRenames(chartFile, picks) is string renames)
            {
                s_report.AddWarning(destinationName, ChartImportWarning.Renamed, renames);
            }
            if (convertedFrom is not null)
            {
                s_report.AddWarning(destinationName, ChartImportWarning.ConvertedText, convertedFrom);
            }
        }

        /// <summary>
        /// Converts a Phira chart folder (RPE or PEC) to a simai chart with URC and stores it with its music and illustration.
        /// </summary>
        /// <remarks>
        /// The chart file is the one the info names, else the first file that is a chart. Official Phigros and binary (PBC)
        /// charts are reported before URC is called. The music and illustration are the files the chart or its info names,
        /// else the ones found by content (<see cref="ChartResourceNormalizer"/>). The folder is named after the chart's title.
        /// </remarks>
        /// <param name="chart">The chart folder.</param>
        /// <param name="destinationRoot">The collection folder receiving the chart.</param>
        /// <param name="collectionName">The collection's name, for the report.</param>
        /// <returns>An operation that completes after the converted chart has been stored or its failure recorded.</returns>
        private static async ValueTask ImportPhiraChartAsync(FoundChart chart, StorageDirectory destinationRoot, string collectionName)
        {
            var infoYml = FindFile(chart.Files, "info.yml") ?? FindFile(chart.Files, "info.yaml");
            var infoTxt = FindFile(chart.Files, "info.txt");
            var infoYmlText = infoYml?.File.ReadAllText();
            var infoTxtText = infoTxt?.File.ReadAllText();
            string? GetInfo(string key, string txtKey)
            {
                return ReadInfoValue(infoYmlText, key) ?? ReadInfoValue(infoTxtText, txtKey);
            }
            var chartFile = FindPhiraChart(chart.Files, GetInfo("chart", "chart"));
            if (chartFile is null)
            {
                s_report.AddFailure(chart.Name, ChartImportFailure.NoChart);
                return;
            }
            var bytes = chartFile.File.ReadAllBytes();
            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('﻿');
            }
            catch (DecoderFallbackException)
            {
                s_report.AddFailure(chart.Name, ChartImportFailure.BinaryChart);
                return;
            }
            var declaredFormat = GetInfo("format", "format")?.ToLowerInvariant();
            if (declaredFormat == "pbc")
            {
                s_report.AddFailure(chart.Name, ChartImportFailure.BinaryChart);
                return;
            }
            if (declaredFormat == "pgr" || (declaredFormat is not ("rpe" or "pec") && ChartFileSniffer.SniffText(text) == ChartFileKind.PhigrosChart))
            {
                s_report.AddFailure(chart.Name, ChartImportFailure.PhigrosChart);
                return;
            }
            MajInstances.SceneSwitcher.SetLoadingText($"{"MAJTEXT_IMPORT_CONVERTING_PEZ".i18n()}\n{chart.Name}");
            UrcSimaiResult result;
            try
            {
                result = await UrcConverter.PezToSimaiAsync(text, infoYmlText, infoTxtText);
            }
            catch (UrcConversionException e)
            {
                await UniTask.SwitchToMainThread();
                s_report.AddFailure(chart.Name, ChartImportFailure.ConvertFailed, e.Message);
                return;
            }
            await UniTask.SwitchToMainThread();

            var track = ChartResourceNormalizer.PickTrack(chart.Files, result.Music ?? GetInfo("music", "song"), out var failure, out var detail);
            if (track is null)
            {
                s_report.AddFailure(chart.Name, failure ?? ChartImportFailure.MissingMusic, detail);
                return;
            }
            var cover = ChartResourceNormalizer.PickCover(chart.Files, result.Illustration ?? GetInfo("illustration", "picture"));
            var title = GetInfo("name", "name") ?? result.Title ?? chart.Name;
            var name = ToDirectoryName(title, ToDirectoryName(chart.Name, "chart"));
            var destination = CreateUniqueDirectory(destinationRoot, name, out var destinationName);
            try
            {
                destination.CreateFile(SongStorage.ChartFileName).WriteAllText(result.Maidata, new UTF8Encoding(false));
                track.File.File.CopyTo(destination, track.TargetName, overwrite: true);
                cover?.File.File.CopyTo(destination, cover.TargetName, overwrite: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                TryDeleteDirectory(destination);
                s_report.AddFailure(chart.Name, ChartImportFailure.CopyFailed, e.Message);
                return;
            }
            s_report.AddChart(destinationName, collectionName);
            if (cover is null)
            {
                s_report.AddWarning(destinationName, ChartImportWarning.NoCover);
            }
        }

        /// <summary>Finds the chart file of a Phira chart folder.</summary>
        /// <param name="files">The files of the folder.</param>
        /// <param name="namedChart">The chart file the info names, or null.</param>
        /// <returns>The named file, else the first file that is a chart, else the first .json or .pec file, or null.</returns>
        private static ScannedFile? FindPhiraChart(IReadOnlyList<ScannedFile> files, string? namedChart)
        {
            if (namedChart is not null && FindFile(files, Path.GetFileName(namedChart.Replace('\\', '/'))) is ScannedFile named)
            {
                return named;
            }
            foreach (var file in files)
            {
                if ((file.Kind is ChartFileKind.RpeChart or ChartFileKind.PecChart or ChartFileKind.PhigrosChart) && !ChartPackageScanner.IsInfoFileName(file.Name))
                {
                    return file;
                }
            }
            foreach (var file in files)
            {
                var extension = Path.GetExtension(file.Name);
                if ((string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".pec", StringComparison.OrdinalIgnoreCase)) &&
                    !ChartPackageScanner.IsInfoFileName(file.Name))
                {
                    return file;
                }
            }
            return null;
        }

        /// <summary>Finds a file by name, ignoring case.</summary>
        /// <param name="files">The files.</param>
        /// <param name="name">The file name.</param>
        /// <returns>The file, or null when none has the name.</returns>
        private static ScannedFile? FindFile(IReadOnlyList<ScannedFile> files, string name)
        {
            foreach (var file in files)
            {
                if (string.Equals(file.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }
            return null;
        }

        /// <summary>Reads a <c>key: value</c> line of a Phira info file (info.yml or info.txt) the way URC's command line does.</summary>
        /// <param name="text">The text of the info file, or null when there is none.</param>
        /// <param name="key">The key, in any case.</param>
        /// <returns>The unquoted value, or null when the key is missing, empty or "null".</returns>
        internal static string? ReadInfoValue(string? text, string key)
        {
            if (text is null)
            {
                return null;
            }
            var match = Regex.Match(text, $"^{Regex.Escape(key)}\\s*:[ \\t]*(.*?)\\s*$",
                                    RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return null;
            }
            var raw = match.Groups[1].Value.Trim();
            if (raw.Length >= 1 && raw[0] == '"')
            {
                try
                {
                    raw = Newtonsoft.Json.JsonConvert.DeserializeObject<string>(raw) ?? string.Empty;
                }
                catch (Newtonsoft.Json.JsonException)
                {
                    raw = raw.Length >= 2 ? raw.Substring(1, raw.Length - 2) : string.Empty;
                }
            }
            else if (raw.Length >= 2 && raw[0] == '\'' && raw[raw.Length - 1] == '\'')
            {
                raw = raw.Substring(1, raw.Length - 2);
            }
            return raw.Length == 0 || raw == "null" ? null : raw;
        }

        /// <summary>Turns a chart title into a directory name that every platform accepts.</summary>
        /// <param name="title">The title.</param>
        /// <param name="fallback">The name used when nothing of the title is left.</param>
        /// <returns>The title without characters that are invalid in file names, at most 80 characters long.</returns>
        internal static string ToDirectoryName(string title, string fallback)
        {
            var builder = new StringBuilder(title.Length);
            foreach (var c in title)
            {
                builder.Append(c < 32 || "\\/:*?\"<>|".IndexOf(c) >= 0 ? '_' : c);
            }
            var name = builder.ToString().Trim().TrimEnd('.');
            if (name.Length > 80)
            {
                name = name.Substring(0, 80).TrimEnd();
            }
            return name.Length == 0 || name.StartsWith(".") ? fallback : name;
        }

        /// <summary>Creates a destination directory without colliding with an existing directory.</summary>
        /// <param name="root">The existing parent directory and its owning backend.</param>
        /// <param name="name">The requested child directory name.</param>
        /// <param name="createdName">The name the directory got.</param>
        /// <returns>The created directory, using a <c>name (i)</c> variant when the requested name is occupied.</returns>
        /// <exception cref="ArgumentException">The requested child name is invalid.</exception>
        /// <exception cref="DirectoryNotFoundException">The parent directory no longer exists.</exception>
        /// <exception cref="UnauthorizedAccessException">Querying or creating the destination was denied.</exception>
        /// <exception cref="NotSupportedException">The backend cannot create directories.</exception>
        /// <exception cref="IOException">Querying or creating the destination failed.</exception>
        private static StorageDirectory CreateUniqueDirectory(StorageDirectory root, string name, out string createdName)
        {
            var candidateName = name;
            for (var i = 1; root.FindDirectory(candidateName) is not null; i++)
            {
                candidateName = $"{name} ({i})";
            }
            createdName = candidateName;
            return root.CreateDirectory(candidateName);
        }

        /// <summary>Determines whether a path is a copy the iOS bridges made in the inbox.</summary>
        /// <param name="location">The path.</param>
        /// <returns>True for a path inside the <see cref="InboxFolderName"/> folder.</returns>
        private static bool IsInboxCopy(string location)
        {
            return location.Replace('\\', '/').IndexOf("/" + InboxFolderName + "/", StringComparison.Ordinal) >= 0;
        }

        /// <summary>Deletes a temporary directory without masking an import failure.</summary>
        /// <param name="directory">The temporary directory to remove.</param>
        private static void TryDeleteDirectory(StorageDirectory directory)
        {
            try
            {
                if (directory.Exists)
                {
                    directory.Delete(recursive: true);
                }
            }
            catch (Exception e)
            {
                MajDebug.LogWarning(nameof(ChartImporter), "Failed to clean up " + directory.Location + ": " + e.Message);
            }
        }

        /// <summary>Deletes a consumed temporary file or folder without masking an import failure.</summary>
        /// <param name="entry">The local path or content URI of the consumed source.</param>
        private static void TryDeleteSource(FileSystemEntry entry)
        {
            try
            {
                if (entry.IsDirectory)
                {
                    FileSystem.Directory.Open(entry.Location).Delete(recursive: true);
                }
                else
                {
                    FileSystem.File.Open(entry.Location).Delete();
                }
            }
            catch (Exception e)
            {
                MajDebug.LogWarning(nameof(ChartImporter), "Failed to delete " + entry.Location + ": " + e.Message);
            }
        }

#if ANDROID_RUNTIME
        private static void OnNewIntent(object? sender, Intent? intent)
        {
            if (intent is null)
            {
                return;
            }
            var action = intent.GetAction();
            if (action == ImportArchiveAction)
            {
                // The shortcut only requests the source; the picker itself runs from OnStartAsync/OnUpdate.
                s_pendingShortcut = ShortcutImportKind.Archive;
                return;
            }
            if (action == ImportFolderAction)
            {
                s_pendingShortcut = ShortcutImportKind.Folder;
                return;
            }
            if (action == Intent.ActionSendMultiple)
            {
                using var uriClass = JavaUri.Class;
                using var uris = intent.GetParcelableArrayListExtra(Intent.ExtraStream, uriClass);
                if (uris is null)
                {
                    return;
                }
                var count = uris.Size();
                for (var i = 0; i < count; i++)
                {
                    using var uriReference = uris.Get(i);
                    if (uriReference is null)
                    {
                        continue;
                    }
                    using var uri = uriReference.JavaReference.ToWrapper<JavaUri>(ownsReference: false)
                        ?? throw new InvalidOperationException("The shared item contains no Java URI reference.");
                    var entry = FileSystem.GetEntry(uri.ToString()!);
                    if (entry is null)
                    {
                        MajDebug.LogError(nameof(ChartImporter), $"GetEntry failed for {uri}");
                        continue;
                    }
                    // A shared URI is a temporary file provided for this import and is consumed afterwards.
                    s_pendingImportTasks.Enqueue(new PendingImport(entry, deleteSourceAfterImport: true));
                }
            }
            else if (action == Intent.ActionSend)
            {
                var uri = intent.GetDataString();
                var entry = FileSystem.GetEntry(uri!);
                if (entry is null)
                {
                    MajDebug.LogError(nameof(ChartImporter), $"GetEntry failed for {uri}");
                    return;
                }

                // A shared URI is a temporary file provided for this import and is consumed afterwards.
                s_pendingImportTasks.Enqueue(new PendingImport(entry, deleteSourceAfterImport: true));
            }
        }

        /// <summary>Shows the picker requested by a launcher shortcut and queues the selected entry for import.</summary>
        /// <remarks>
        /// At most one picker is shown per shortcut activation. A dismissed picker leaves the queue untouched, and a
        /// provider failure is logged instead of propagating into the player loop.
        /// </remarks>
        private static async ValueTask ShowShortcutPickerAsync()
        {
            var kind = s_pendingShortcut;
            s_pendingShortcut = ShortcutImportKind.None;
            if (kind == ShortcutImportKind.None)
            {
                return;
            }
            try
            {
                // The file picker lists every file; the import tells what the chosen one holds by its content.
                string? location;
                if (kind == ShortcutImportKind.Archive)
                {
                    var file = await AndroidStorageAccess.PickFileAsync("*/*", writable: false, persistPermission: false);
                    location = file?.Location;
                }
                else
                {
                    var directory = await AndroidStorageAccess.PickDirectoryAsync(writable: false, persistPermission: false);
                    location = directory?.Location;
                }
                if (location is null)
                {
                    // The user dismissed the picker; there is nothing to import.
                    return;
                }
                var entry = FileSystem.GetEntry(location);
                if (entry is null)
                {
                    MajDebug.LogError(nameof(ChartImporter), $"GetEntry failed for {location}");
                    return;
                }
                // The picked document belongs to its provider and must survive the import.
                s_pendingImportTasks.Enqueue(new PendingImport(entry, deleteSourceAfterImport: false));
            }
            catch (Exception e)
            {
                MajDebug.LogError($"[ChartImporter] Shortcut import failed: {e}");
            }
        }
#endif
    }
}
