using MajdataPlay.Diagnostics;
using MajdataPlay.IO.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

#nullable enable
namespace MajdataPlay.Import
{
    /// <summary>The chart format a found chart folder holds.</summary>
    internal enum FoundChartFormat
    {
        /// <summary>A simai chart (maidata.txt and its media).</summary>
        Simai,

        /// <summary>A Phira chart (RPE, PEC or official Phigros, with info.yml or info.txt and its media).</summary>
        Phira,
    }

    /// <summary>One file of a scanned folder, with its kind judged from its content.</summary>
    internal sealed class ScannedFile
    {
        /// <summary>Initializes a new instance of the <see cref="ScannedFile"/> class.</summary>
        /// <param name="file">The file.</param>
        /// <param name="name">The file name.</param>
        /// <param name="length">The size in bytes, or 0 when unknown.</param>
        /// <param name="kind">What the file holds.</param>
        public ScannedFile(StorageFile file, string name, long length, ChartFileKind kind)
        {
            File = file;
            Name = name;
            Length = length;
            Kind = kind;
        }

        /// <summary>Gets the file.</summary>
        public StorageFile File { get; }

        /// <summary>Gets the file name.</summary>
        public string Name { get; }

        /// <summary>Gets the size in bytes, or 0 when unknown.</summary>
        public long Length { get; }

        /// <summary>Gets what the file holds.</summary>
        public ChartFileKind Kind { get; }
    }

    /// <summary>A chart folder found while scanning.</summary>
    internal sealed class FoundChart
    {
        /// <summary>Initializes a new instance of the <see cref="FoundChart"/> class.</summary>
        /// <param name="directory">The chart folder.</param>
        /// <param name="name">The name the imported chart folder starts from.</param>
        /// <param name="collection">The collection the chart goes to, or null for the Import collection.</param>
        /// <param name="format">The chart format.</param>
        /// <param name="files">The files directly in the folder, metadata left out.</param>
        /// <param name="subdirectories">The folders directly in the folder, metadata left out.</param>
        public FoundChart(StorageDirectory directory, string name, string? collection, FoundChartFormat format,
                          IReadOnlyList<ScannedFile> files, IReadOnlyList<FileSystemEntry> subdirectories)
        {
            Directory = directory;
            Name = name;
            Collection = collection;
            Format = format;
            Files = files;
            Subdirectories = subdirectories;
        }

        /// <summary>Gets the chart folder.</summary>
        public StorageDirectory Directory { get; }

        /// <summary>Gets the name the imported chart folder starts from.</summary>
        public string Name { get; }

        /// <summary>Gets the collection the chart goes to, or null for the Import collection.</summary>
        public string? Collection { get; }

        /// <summary>Gets the chart format.</summary>
        public FoundChartFormat Format { get; }

        /// <summary>Gets the files directly in the folder, metadata left out.</summary>
        public IReadOnlyList<ScannedFile> Files { get; }

        /// <summary>Gets the folders directly in the folder, metadata left out.</summary>
        public IReadOnlyList<FileSystemEntry> Subdirectories { get; }
    }

    /// <summary>What a scan found.</summary>
    internal sealed class ChartScanResult
    {
        /// <summary>Gets the chart folders, in the order they were found.</summary>
        public List<FoundChart> Charts { get; } = new();

        /// <summary>Gets the ZIP archives found outside chart folders (packs of .adx files, a .pez in a folder, ...).</summary>
        public List<ScannedFile> Archives { get; } = new();

        /// <summary>Gets the archives the game cannot unpack (RAR, 7z, ...).</summary>
        public List<ScannedFile> UnsupportedArchives { get; } = new();
    }

    /// <summary>
    /// Finds chart folders in a folder tree by what their files hold, whatever the files and folders are named.
    /// </summary>
    /// <remarks>
    /// A folder is a simai chart when it holds maidata.txt (any case) or another text file that is a simai chart. It is a
    /// Phira chart when it holds info.yml, info.yaml or info.txt, or a file that is an RPE, PEC or official Phigros chart.
    /// A chart at the root, or in a folder directly under it, goes to the Import collection; a chart deeper down goes to the
    /// collection named after its parent folder, unless that folder only wraps this one chart. The tree is walked up to
    /// <see cref="MaxDepth"/> folders deep, leaving out hidden folders and macOS metadata, and chart folders are not searched
    /// further.
    /// </remarks>
    internal static class ChartPackageScanner
    {
        /// <summary>The deepest folder level searched below the root.</summary>
        internal const int MaxDepth = 8;

        /// <summary>Matches the info file of a Phira package.</summary>
        private static readonly Regex s_infoFileName = new(@"^info\.(ya?ml|txt)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Scans a folder tree.
        /// </summary>
        /// <param name="root">The folder to scan.</param>
        /// <param name="rootName">The name a chart found at the root gets, such as the archive name without extension.</param>
        /// <returns>The chart folders and archives found.</returns>
        internal static ChartScanResult Scan(StorageDirectory root, string rootName)
        {
            var result = new ChartScanResult();
            Visit(root, rootName, null, 0, result);
            return result;
        }

        /// <summary>Determines whether an entry is left out of scans and copies.</summary>
        /// <param name="entry">The entry.</param>
        /// <returns>True for hidden and system entries, names starting with a dot, <c>__MACOSX</c> and <c>Thumbs.db</c>.</returns>
        internal static bool IsExcluded(FileSystemEntry entry)
        {
            return entry.IsHidden || entry.IsSystem || entry.Name.StartsWith(".", StringComparison.Ordinal) ||
                   string.Equals(entry.Name, "__MACOSX", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(entry.Name, "Thumbs.db", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(entry.Name, "desktop.ini", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Determines whether a file name is a Phira info file.</summary>
        /// <param name="name">The file name.</param>
        /// <returns>True for info.yml, info.yaml and info.txt in any case.</returns>
        internal static bool IsInfoFileName(string name)
        {
            return s_infoFileName.IsMatch(name);
        }

        /// <summary>
        /// Lists and sniffs the files and folders directly in a folder.
        /// </summary>
        /// <param name="directory">The folder.</param>
        /// <param name="files">The files, metadata left out.</param>
        /// <param name="subdirectories">The folders, metadata left out.</param>
        internal static void ReadFolder(StorageDirectory directory, out List<ScannedFile> files, out List<FileSystemEntry> subdirectories)
        {
            files = new List<ScannedFile>();
            subdirectories = new List<FileSystemEntry>();
            foreach (var entry in directory.EnumerateEntries())
            {
                if (IsExcluded(entry))
                {
                    continue;
                }
                if (entry.IsDirectory)
                {
                    subdirectories.Add(entry);
                    continue;
                }
                var file = new StorageFile(directory.FileSystem, entry.Location);
                files.Add(new ScannedFile(file, entry.Name, entry.Length ?? 0, ChartFileSniffer.Sniff(file, entry.Name)));
            }
        }

        /// <summary>Tells whether a folder's files make it a chart folder.</summary>
        /// <param name="files">The files directly in the folder.</param>
        /// <returns>The chart format, or null when the folder is not a chart.</returns>
        internal static FoundChartFormat? GetChartFormat(IReadOnlyList<ScannedFile> files)
        {
            var isPhira = false;
            foreach (var file in files)
            {
                if (string.Equals(file.Name, SongStorage.ChartFileName, StringComparison.OrdinalIgnoreCase) || file.Kind == ChartFileKind.Simai)
                {
                    return FoundChartFormat.Simai;
                }
                if (IsInfoFileName(file.Name) || file.Kind is ChartFileKind.RpeChart or ChartFileKind.PecChart or ChartFileKind.PhigrosChart)
                {
                    isPhira = true;
                }
            }
            return isPhira ? FoundChartFormat.Phira : null;
        }

        /// <summary>Scans one folder and the folders below it.</summary>
        /// <param name="directory">The folder.</param>
        /// <param name="name">The folder's name.</param>
        /// <param name="parent">The parent folder's name and whether it only wraps this folder, or null for the root.</param>
        /// <param name="depth">The folder's depth below the root.</param>
        /// <param name="result">Receives what is found.</param>
        private static void Visit(StorageDirectory directory, string name, ParentInfo? parent, int depth, ChartScanResult result)
        {
            List<ScannedFile> files;
            List<FileSystemEntry> subdirectories;
            try
            {
                ReadFolder(directory, out files, out subdirectories);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                MajDebug.LogWarning(nameof(ChartPackageScanner), $"Cannot read {directory.Location}: {e.Message}");
                return;
            }
            if (GetChartFormat(files) is FoundChartFormat format)
            {
                result.Charts.Add(new FoundChart(directory, name, GetCollection(parent, depth), format, files, subdirectories));
                return;
            }
            foreach (var file in files)
            {
                if (file.Kind == ChartFileKind.Zip)
                {
                    result.Archives.Add(file);
                }
                else if (ChartFileSniffer.IsArchive(file.Kind))
                {
                    result.UnsupportedArchives.Add(file);
                }
            }
            if (depth >= MaxDepth)
            {
                return;
            }
            // A folder that holds nothing but one folder only wraps it, like "Song/Song/maidata.txt".
            var isWrapper = files.Count == 0 && subdirectories.Count == 1;
            foreach (var subdirectory in subdirectories)
            {
                var child = new StorageDirectory(directory.FileSystem, subdirectory.Location);
                Visit(child, subdirectory.Name, new ParentInfo(name, isWrapper), depth + 1, result);
            }
        }

        /// <summary>Gets the collection a chart folder goes to.</summary>
        /// <param name="parent">The chart folder's parent, or null when the chart is the root.</param>
        /// <param name="depth">The chart folder's depth below the root.</param>
        /// <returns>The parent's name for a chart two or more levels down, unless the parent only wraps it; null for the Import collection.</returns>
        private static string? GetCollection(ParentInfo? parent, int depth)
        {
            if (parent is null || depth <= 1 || parent.IsWrapper)
            {
                return null;
            }
            return parent.Name;
        }

        /// <summary>The name of a chart folder's parent and whether it only wraps the chart.</summary>
        private sealed class ParentInfo
        {
            /// <summary>Initializes a new instance of the <see cref="ParentInfo"/> class.</summary>
            /// <param name="name">The parent folder's name.</param>
            /// <param name="isWrapper">Whether the parent holds nothing but this one folder.</param>
            public ParentInfo(string name, bool isWrapper)
            {
                Name = name;
                IsWrapper = isWrapper;
            }

            /// <summary>Gets the parent folder's name.</summary>
            public string Name { get; }

            /// <summary>Gets a value indicating whether the parent holds nothing but this one folder.</summary>
            public bool IsWrapper { get; }
        }
    }
}
