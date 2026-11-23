using Cysharp.Threading.Tasks;
using MajdataPlay.Diagnostics;
using MajdataPlay.IO.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using UnityEngine;
// UnityEngine also has a CompressionLevel; the archives use the one of System.IO.Compression.
using CompressionLevel = System.IO.Compression.CompressionLevel;

#nullable enable
namespace MajdataPlay.Import
{
    /// <summary>
    /// The archive formats a local chart can be exported to.
    /// </summary>
    internal enum ChartExportFormat
    {
        /// <summary>The whole chart folder in a .zip, as MajdataPlay and most players import it.</summary>
        Zip,

        /// <summary>An AstroDX .adx package: maidata.txt, the track, the cover and the video in one folder.</summary>
        Adx,

        /// <summary>A Phira .pez package of one difficulty, converted by Universal Rhythm Chart.</summary>
        Pez,
    }

    /// <summary>
    /// Packs a local chart into a .zip, .adx or .pez archive in the temporary folder, ready to be shared.
    /// </summary>
    /// <remarks>
    /// Archives are written to <c>MajEnv.TempDirectory/ChartExports</c>, which is emptied before each export. The
    /// identity marker folder (<c>.MajdataPlay</c>) and other hidden entries are left out, so a chart imported from an
    /// export gets its own identity.
    /// </remarks>
    internal static class ChartExporter
    {
        /// <summary>The temporary folder the archives are written to.</summary>
        private const string ExportFolderName = "ChartExports";

        /// <summary>The extensions that are already compressed and are stored without compressing them again.</summary>
        private static readonly HashSet<string> s_storedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".ogg", ".opus", ".aac", ".m4a", ".mp4", ".webm", ".mkv", ".mov", ".png", ".jpg", ".jpeg",
        };

        /// <summary>
        /// Gets the extension of the archive a format produces.
        /// </summary>
        /// <param name="format">The export format.</param>
        /// <returns>".zip", ".adx" or ".pez".</returns>
        internal static string GetExtension(ChartExportFormat format)
        {
            return format switch
            {
                ChartExportFormat.Adx => ".adx",
                ChartExportFormat.Pez => ".pez",
                _ => ".zip",
            };
        }

        /// <summary>
        /// Exports a local chart and opens the system share sheet for the archive (on desktops, its folder).
        /// </summary>
        /// <param name="song">The local chart.</param>
        /// <param name="format">The archive format.</param>
        /// <param name="level">The difficulty a .pez holds; ignored by the other formats.</param>
        /// <param name="token">Cancels the export between steps.</param>
        /// <returns>The local path of the archive.</returns>
        /// <exception cref="UrcConversionException">The chart could not be converted to .pez.</exception>
        /// <exception cref="IOException">The chart could not be read or the archive could not be written.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="token"/> was canceled.</exception>
        internal static async UniTask<string> ExportAndShareAsync(SongDetail song, ChartExportFormat format, ChartLevel level,
                                                                  CancellationToken token = default)
        {
            var path = await ExportAsync(song, format, level, token);
            await UniTask.SwitchToMainThread(token);
            var mimeType = format == ChartExportFormat.Zip ? "application/zip" : "application/octet-stream";
            NativeShare.ShareFiles(new[] { path }, mimeType, song.Title);
            return path;
        }

        /// <summary>
        /// Exports a local chart to an archive in the temporary export folder.
        /// </summary>
        /// <param name="song">The local chart.</param>
        /// <param name="format">The archive format.</param>
        /// <param name="level">The difficulty a .pez holds; ignored by the other formats.</param>
        /// <param name="token">Cancels the export between steps.</param>
        /// <returns>The local path of the archive.</returns>
        /// <exception cref="UrcConversionException">The chart could not be converted to .pez.</exception>
        /// <exception cref="IOException">The chart could not be read or the archive could not be written.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="token"/> was canceled.</exception>
        internal static async UniTask<string> ExportAsync(SongDetail song, ChartExportFormat format, ChartLevel level,
                                                          CancellationToken token = default)
        {
            var source = song.Directory;
            var name = ChartImporter.ToDirectoryName(song.Title, "chart");
            var folder = PrepareExportFolder();
            var archiveName = name + GetExtension(format);
            switch (format)
            {
                case ChartExportFormat.Pez:
                    {
                        var illustration = FindFile(source, SongStorage.SupportedCoverFileNames) is null
                            ? await CreatePlaceholderIllustrationAsync(token)
                            : null;
                        return await WritePezAsync(source, folder, archiveName, (int)level + 1, illustration, token);
                    }
                case ChartExportFormat.Adx:
                    await UniTask.SwitchToThreadPool();
                    return WriteArchive(folder, archiveName, archive => AddAdxEntries(archive, source, name, token));
                default:
                    await UniTask.SwitchToThreadPool();
                    return WriteArchive(folder, archiveName, archive => AddDirectory(archive, source, name + "/", token));
            }
        }

        /// <summary>
        /// Converts one difficulty to Phira and writes the .pez package.
        /// </summary>
        /// <param name="source">The chart folder.</param>
        /// <param name="folder">The export folder.</param>
        /// <param name="archiveName">The archive file name.</param>
        /// <param name="inote">The simai inote number of the difficulty.</param>
        /// <param name="placeholderIllustration">A PNG to use when the chart has no cover, or null when it has one.</param>
        /// <param name="token">Cancels the export between steps.</param>
        /// <returns>The local path of the archive.</returns>
        /// <exception cref="UrcConversionException">The chart could not be converted.</exception>
        /// <exception cref="IOException">The chart could not be read or the archive could not be written.</exception>
        private static async UniTask<string> WritePezAsync(StorageDirectory source, StorageDirectory folder, string archiveName,
                                                           int inote, byte[]? placeholderIllustration, CancellationToken token)
        {
            var maidataFile = source.FindFile(SongStorage.ChartFileName)
                ?? throw new IOException($"The chart has no {SongStorage.ChartFileName}.");
            var names = new List<string>();
            foreach (var entry in source.EnumerateEntries())
            {
                if (!entry.IsDirectory && !IsExcluded(entry))
                {
                    names.Add(entry.Name);
                }
            }
            var maidata = maidataFile.ReadAllText();
            var result = await UrcConverter.SimaiToPezAsync(maidata, inote, names, token);
            token.ThrowIfCancellationRequested();
            return WriteArchive(folder, archiveName, archive =>
            {
                AddText(archive, "chart.json", result.ChartJson);
                AddText(archive, "info.yml", result.InfoYaml);
                var music = source.FindFile(result.Music)
                    ?? throw new IOException($"The chart has no music file {result.Music}.");
                AddFile(archive, result.Music, music);
                if (source.FindFile(result.Illustration) is StorageFile illustration)
                {
                    AddFile(archive, result.Illustration, illustration);
                }
                else if (placeholderIllustration is not null)
                {
                    // Phira needs an illustration; a chart without a cover gets a plain dark one.
                    AddBytes(archive, result.Illustration, placeholderIllustration);
                }
            });
        }

        /// <summary>
        /// Adds the files of an AstroDX package: maidata.txt, the track, the cover and the video, in one folder.
        /// </summary>
        /// <param name="archive">The archive being written.</param>
        /// <param name="source">The chart folder.</param>
        /// <param name="name">The folder name inside the archive.</param>
        /// <param name="token">Cancels between files.</param>
        /// <exception cref="IOException">The chart has no maidata.txt or no track.</exception>
        private static void AddAdxEntries(ZipArchive archive, StorageDirectory source, string name, CancellationToken token)
        {
            var maidata = source.FindFile(SongStorage.ChartFileName)
                ?? throw new IOException($"The chart has no {SongStorage.ChartFileName}.");
            AddFile(archive, $"{name}/{SongStorage.ChartFileName}", maidata);
            var track = FindFile(source, SongStorage.SupportedTrackFileNames)
                ?? throw new IOException("The chart has no supported track.");
            AddFile(archive, $"{name}/{track.Name}", track.File);
            foreach (var fileNames in new[] { SongStorage.SupportedCoverFileNames, SongStorage.SupportedVideoFileNames })
            {
                token.ThrowIfCancellationRequested();
                if (FindFile(source, fileNames) is { } found)
                {
                    AddFile(archive, $"{name}/{found.Name}", found.File);
                }
            }
        }

        /// <summary>
        /// Adds a folder and its subfolders to an archive, leaving out hidden entries and the identity marker.
        /// </summary>
        /// <param name="archive">The archive being written.</param>
        /// <param name="directory">The folder to add.</param>
        /// <param name="prefix">The path of the folder inside the archive, ending with a slash.</param>
        /// <param name="token">Cancels between files.</param>
        private static void AddDirectory(ZipArchive archive, StorageDirectory directory, string prefix, CancellationToken token)
        {
            foreach (var entry in directory.EnumerateEntries())
            {
                token.ThrowIfCancellationRequested();
                if (IsExcluded(entry))
                {
                    continue;
                }
                if (entry.IsDirectory)
                {
                    AddDirectory(archive, new StorageDirectory(directory.FileSystem, entry.Location), $"{prefix}{entry.Name}/", token);
                    continue;
                }
                AddFile(archive, prefix + entry.Name, new StorageFile(directory.FileSystem, entry.Location));
            }
        }

        /// <summary>
        /// Creates an archive in the export folder and fills it.
        /// </summary>
        /// <param name="folder">The export folder.</param>
        /// <param name="archiveName">The archive file name.</param>
        /// <param name="fill">Adds the entries.</param>
        /// <returns>The local path of the archive.</returns>
        /// <exception cref="IOException">The archive could not be written.</exception>
        private static string WriteArchive(StorageDirectory folder, string archiveName, Action<ZipArchive> fill)
        {
            var file = folder.CreateFile(archiveName);
            try
            {
                using (var stream = file.OpenWrite())
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    fill(archive);
                }
            }
            catch
            {
                TryDelete(file);
                throw;
            }
            MajDebug.LogInfo(nameof(ChartExporter), $"Exported {file.Location}");
            return file.Location;
        }

        /// <summary>
        /// Adds a file to an archive, storing media that is already compressed as it is.
        /// </summary>
        /// <param name="archive">The archive being written.</param>
        /// <param name="entryName">The path inside the archive.</param>
        /// <param name="file">The file to add.</param>
        private static void AddFile(ZipArchive archive, string entryName, StorageFile file)
        {
            var level = s_storedExtensions.Contains(Path.GetExtension(entryName)) ? CompressionLevel.NoCompression : CompressionLevel.Optimal;
            var entry = archive.CreateEntry(entryName, level);
            using var output = entry.Open();
            using var input = file.OpenRead();
            input.CopyTo(output);
        }

        /// <summary>
        /// Adds UTF-8 text without a byte order mark to an archive.
        /// </summary>
        /// <param name="archive">The archive being written.</param>
        /// <param name="entryName">The path inside the archive.</param>
        /// <param name="text">The text.</param>
        private static void AddText(ZipArchive archive, string entryName, string text)
        {
            AddBytes(archive, entryName, new UTF8Encoding(false).GetBytes(text));
        }

        /// <summary>
        /// Adds bytes to an archive.
        /// </summary>
        /// <param name="archive">The archive being written.</param>
        /// <param name="entryName">The path inside the archive.</param>
        /// <param name="bytes">The content.</param>
        private static void AddBytes(ZipArchive archive, string entryName, byte[] bytes)
        {
            var level = s_storedExtensions.Contains(Path.GetExtension(entryName)) ? CompressionLevel.NoCompression : CompressionLevel.Optimal;
            var entry = archive.CreateEntry(entryName, level);
            using var output = entry.Open();
            output.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Finds the first of several file names in a folder, ignoring case.
        /// </summary>
        /// <param name="directory">The folder.</param>
        /// <param name="fileNames">The names to look for, in order of preference.</param>
        /// <returns>The file and its actual name, or null when none is there.</returns>
        private static (StorageFile File, string Name)? FindFile(StorageDirectory directory, string[] fileNames)
        {
            foreach (var wanted in fileNames)
            {
                foreach (var entry in directory.EnumerateEntries())
                {
                    if (!entry.IsDirectory && string.Equals(entry.Name, wanted, StringComparison.OrdinalIgnoreCase))
                    {
                        return (new StorageFile(directory.FileSystem, entry.Location), entry.Name);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Creates the export folder and removes the archives of earlier exports.
        /// </summary>
        /// <returns>The empty export folder.</returns>
        private static StorageDirectory PrepareExportFolder()
        {
            var folder = MajEnv.TempDirectory.CreateDirectory(ExportFolderName);
            foreach (var entry in folder.EnumerateEntries())
            {
                try
                {
                    if (entry.IsDirectory)
                    {
                        new StorageDirectory(folder.FileSystem, entry.Location).Delete(recursive: true);
                    }
                    else
                    {
                        new StorageFile(folder.FileSystem, entry.Location).Delete();
                    }
                }
                catch (Exception e)
                {
                    MajDebug.LogWarning(nameof(ChartExporter), $"Failed to remove an earlier export {entry.Location}: {e.Message}");
                }
            }
            return folder;
        }

        /// <summary>
        /// Creates a plain dark 16:9 PNG for a chart that has no cover.
        /// </summary>
        /// <param name="token">Cancels switching to the main thread.</param>
        /// <returns>The PNG bytes.</returns>
        private static async UniTask<byte[]> CreatePlaceholderIllustrationAsync(CancellationToken token)
        {
            await UniTask.SwitchToMainThread(token);
            var texture = new Texture2D(320, 180, TextureFormat.RGB24, false);
            try
            {
                var pixels = new Color32[texture.width * texture.height];
                var color = new Color32(0x20, 0x26, 0x30, 0xFF);
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = color;
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        /// <summary>
        /// Determines whether an entry is left out of exports.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <returns>True for hidden and system entries and names starting with a dot, such as the identity marker.</returns>
        private static bool IsExcluded(FileSystemEntry entry)
        {
            return entry.IsHidden || entry.IsSystem || entry.Name.StartsWith(".");
        }

        /// <summary>
        /// Deletes a partly written archive without masking the error that stopped it.
        /// </summary>
        /// <param name="file">The archive.</param>
        private static void TryDelete(StorageFile file)
        {
            try
            {
                file.Delete();
            }
            catch (Exception e)
            {
                MajDebug.LogWarning(nameof(ChartExporter), $"Failed to remove {file.Location}: {e.Message}");
            }
        }
    }
}
