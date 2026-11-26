using MajdataPlay.IO.Storage;
using MajdataPlay.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

#nullable enable
namespace MajdataPlay.Import
{
    /// <summary>A file chosen for one role of a chart and the name it is stored under.</summary>
    internal sealed class ResourcePick
    {
        /// <summary>Initializes a new instance of the <see cref="ResourcePick"/> class.</summary>
        /// <param name="file">The chosen file.</param>
        /// <param name="targetName">The name the file is stored under in the imported chart.</param>
        public ResourcePick(ScannedFile file, string targetName)
        {
            File = file;
            TargetName = targetName;
        }

        /// <summary>Gets the chosen file.</summary>
        public ScannedFile File { get; }

        /// <summary>Gets the name the file is stored under in the imported chart.</summary>
        public string TargetName { get; }

        /// <summary>Gets a value indicating whether the file is renamed.</summary>
        public bool IsRenamed => !string.Equals(File.Name, TargetName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Chooses a chart's music, cover and video by content and name, and copies the chart under the names the game reads:
    /// <c>maidata.txt</c>, <c>track.*</c>, <c>bg.png</c> or <c>bg.jpg</c>, and <c>pv.*</c>.
    /// </summary>
    /// <remarks>
    /// A file that already has a name the game reads is kept as it is. Otherwise the file whose name starts with one of the
    /// usual words for its role (song, cover, jacket, ...) is chosen, else the largest one. Music and video get the extension of
    /// their real format; a cover becomes <c>bg.jpg</c> when it is a JPEG and <c>bg.png</c> otherwise, because the game decodes
    /// covers by content.
    /// </remarks>
    internal static class ChartResourceNormalizer
    {
        /// <summary>The usual names of a chart's music, most likely first.</summary>
        private static readonly string[] s_trackStems = { "track", "song", "music", "audio", "bgm", "sound" };

        /// <summary>The usual names of a chart's cover, most likely first.</summary>
        private static readonly string[] s_coverStems = { "bg", "cover", "jacket", "illustration", "background", "image", "picture", "pic", "art" };

        /// <summary>The usual names of a chart's video, most likely first.</summary>
        private static readonly string[] s_videoStems = { "pv", "bg", "mv", "movie", "video" };

        /// <summary>
        /// Finds the chart text of a simai chart folder.
        /// </summary>
        /// <param name="files">The files of the folder.</param>
        /// <returns>maidata.txt in any case, else the first file that is a simai chart, or null when there is none.</returns>
        internal static ScannedFile? FindSimaiChart(IReadOnlyList<ScannedFile> files)
        {
            foreach (var file in files)
            {
                if (string.Equals(file.Name, SongStorage.ChartFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }
            foreach (var file in files)
            {
                if (file.Kind == ChartFileKind.Simai)
                {
                    return file;
                }
            }
            return null;
        }

        /// <summary>
        /// Chooses a chart's music.
        /// </summary>
        /// <param name="files">The files of the chart folder.</param>
        /// <param name="namedFile">The music file the chart or its info names, or null.</param>
        /// <param name="failure">Why no playable music was chosen, or null on success.</param>
        /// <param name="detail">The format of music the game cannot play, or null.</param>
        /// <returns>The music and its name as <c>track.*</c>, or null when there is no music the game plays.</returns>
        internal static ResourcePick? PickTrack(IReadOnlyList<ScannedFile> files, string? namedFile, out ChartImportFailure? failure, out string? detail)
        {
            failure = null;
            detail = null;
            foreach (var file in files)
            {
                if (Array.IndexOf(SongStorage.SupportedTrackFileNames, file.Name.ToLowerInvariant()) >= 0 && IsPlayableAudio(file))
                {
                    return new ResourcePick(file, file.Name);
                }
            }
            var chosen = FindNamed(files, namedFile, IsAudioCandidate) ?? Choose(files, s_trackStems, IsAudioCandidate);
            if (chosen is null)
            {
                failure = ChartImportFailure.MissingMusic;
                return null;
            }
            var extension = GetPlayableAudioExtension(chosen);
            if (extension is null)
            {
                failure = ChartImportFailure.UnsupportedMusic;
                detail = ChartFileSniffer.GetFormatName(chosen.Kind);
                return null;
            }
            return new ResourcePick(chosen, "track" + extension);
        }

        /// <summary>
        /// Chooses a chart's cover.
        /// </summary>
        /// <param name="files">The files of the chart folder.</param>
        /// <param name="namedFile">The image the chart or its info names, or null.</param>
        /// <returns>The cover and its name as <c>bg.png</c> or <c>bg.jpg</c>, or null when the folder has no image.</returns>
        internal static ResourcePick? PickCover(IReadOnlyList<ScannedFile> files, string? namedFile)
        {
            foreach (var file in files)
            {
                if (Array.IndexOf(SongStorage.SupportedCoverFileNames, file.Name.ToLowerInvariant()) >= 0 && ChartFileSniffer.IsImage(file.Kind))
                {
                    return new ResourcePick(file, file.Name);
                }
            }
            var chosen = FindNamed(files, namedFile, IsImageCandidate) ?? Choose(files, s_coverStems, IsImageCandidate);
            return chosen is null ? null : new ResourcePick(chosen, ChartFileSniffer.GetCoverFileName(chosen.Kind));
        }

        /// <summary>
        /// Chooses a chart's video.
        /// </summary>
        /// <param name="files">The files of the chart folder.</param>
        /// <param name="unsupportedFormat">The format of a video the game cannot play, or null.</param>
        /// <returns>The video and its name as <c>pv.*</c>, or null when the folder has no video the game plays.</returns>
        internal static ResourcePick? PickVideo(IReadOnlyList<ScannedFile> files, out string? unsupportedFormat)
        {
            unsupportedFormat = null;
            foreach (var file in files)
            {
                if (Array.IndexOf(SongStorage.SupportedVideoFileNames, file.Name.ToLowerInvariant()) >= 0)
                {
                    return new ResourcePick(file, file.Name);
                }
            }
            var chosen = Choose(files, s_videoStems, IsVideoCandidate);
            if (chosen is null)
            {
                return null;
            }
            var extension = ChartFileSniffer.GetPlayableVideoExtension(chosen.Kind) ?? GetSupportedExtension(chosen.Name, MajEnv.SupportedVideoFormats);
            if (extension is null)
            {
                unsupportedFormat = ChartFileSniffer.GetFormatName(chosen.Kind);
                return null;
            }
            return new ResourcePick(chosen, "pv" + extension);
        }

        /// <summary>
        /// Copies a simai chart folder, storing the chart text and the chosen media under the names the game reads and every
        /// other file and folder as it is.
        /// </summary>
        /// <param name="chart">The chart folder.</param>
        /// <param name="chartFile">The chart text, stored as maidata.txt.</param>
        /// <param name="picks">The chosen media; null entries are skipped.</param>
        /// <param name="destination">The new, empty chart folder.</param>
        /// <returns>The encoding the chart text was converted from to UTF-8, or null when it was copied as it is.</returns>
        /// <exception cref="IOException">Copying failed.</exception>
        /// <exception cref="UnauthorizedAccessException">Reading or writing was denied.</exception>
        internal static string? CopySimai(FoundChart chart, ScannedFile chartFile, IReadOnlyList<ResourcePick?> picks, StorageDirectory destination)
        {
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { SongStorage.ChartFileName };
            var convertedFrom = WriteChartText(chartFile, destination);
            var handled = new HashSet<string>(StringComparer.Ordinal) { chartFile.File.Location };
            foreach (var pick in picks)
            {
                if (pick is null || !written.Add(pick.TargetName))
                {
                    continue;
                }
                pick.File.File.CopyTo(destination, pick.TargetName, overwrite: true);
                handled.Add(pick.File.File.Location);
            }
            foreach (var file in chart.Files)
            {
                // A renamed file is stored once, under its new name; a file whose name the chart now uses is left out.
                if (handled.Contains(file.File.Location) || !written.Add(file.Name))
                {
                    continue;
                }
                file.File.CopyTo(destination, file.Name, overwrite: true);
            }
            foreach (var subdirectory in chart.Subdirectories)
            {
                var source = new StorageDirectory(chart.Directory.FileSystem, subdirectory.Location);
                StorageDirectoryTransfer.Copy(source, destination.CreateDirectory(subdirectory.Name));
            }
            return convertedFrom;
        }

        /// <summary>
        /// Stores the chart text as maidata.txt: UTF-8 text as it is, UTF-16 and legacy code pages (GBK, Shift-JIS) converted
        /// to UTF-8, which the game reads.
        /// </summary>
        /// <param name="chartFile">The chart text.</param>
        /// <param name="destination">The chart folder.</param>
        /// <returns>The encoding the text was converted from, or null when it was copied as it is.</returns>
        /// <exception cref="IOException">Reading or writing failed.</exception>
        private static string? WriteChartText(ScannedFile chartFile, StorageDirectory destination)
        {
            var bytes = chartFile.File.ReadAllBytes();
            string? text = null;
            string? encodingName = null;
            if (bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF)))
            {
                var utf16 = bytes[0] == 0xFF ? Encoding.Unicode : Encoding.BigEndianUnicode;
                text = utf16.GetString(bytes, 2, bytes.Length - 2);
                encodingName = utf16.WebName;
            }
            else if (!IsUtf8(bytes))
            {
                text = ChartArchiveReader.DecodeLegacyText(bytes, out encodingName);
            }
            if (text is null)
            {
                // UTF-8, or text no known encoding reads: stored as it is.
                chartFile.File.CopyTo(destination, SongStorage.ChartFileName, overwrite: true);
                return null;
            }
            destination.CreateFile(SongStorage.ChartFileName).WriteAllText(text, new UTF8Encoding(false));
            return encodingName;
        }

        /// <summary>Determines whether bytes are valid UTF-8.</summary>
        /// <param name="bytes">The bytes.</param>
        /// <returns>True when strict UTF-8 decodes them.</returns>
        private static bool IsUtf8(byte[] bytes)
        {
            try
            {
                new UTF8Encoding(false, true).GetCharCount(bytes);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        /// <summary>Describes the renamed files of a chart, such as "song.ogg → track.ogg".</summary>
        /// <param name="chartFile">The chart text, or null for a converted chart.</param>
        /// <param name="picks">The chosen media; null entries are skipped.</param>
        /// <returns>The renames joined by commas, or null when nothing was renamed.</returns>
        internal static string? DescribeRenames(ScannedFile? chartFile, IReadOnlyList<ResourcePick?> picks)
        {
            var renames = new List<string>();
            if (chartFile is not null && !string.Equals(chartFile.Name, SongStorage.ChartFileName, StringComparison.OrdinalIgnoreCase))
            {
                renames.Add($"{chartFile.Name} → {SongStorage.ChartFileName}");
            }
            foreach (var pick in picks)
            {
                if (pick is not null && pick.IsRenamed)
                {
                    renames.Add($"{pick.File.Name} → {pick.TargetName}");
                }
            }
            return renames.Count == 0 ? null : string.Join(", ", renames);
        }

        /// <summary>Finds the file a chart or its info names, if it has the right kind.</summary>
        /// <param name="files">The files of the chart folder.</param>
        /// <param name="name">The named file, or null.</param>
        /// <param name="isCandidate">Whether a file has the right kind.</param>
        /// <returns>The named file, or null when it is missing or of another kind.</returns>
        private static ScannedFile? FindNamed(IReadOnlyList<ScannedFile> files, string? name, Func<ScannedFile, bool> isCandidate)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            var fileName = Path.GetFileName(name.Replace('\\', '/'));
            foreach (var file in files)
            {
                if (string.Equals(file.Name, fileName, StringComparison.OrdinalIgnoreCase) && isCandidate(file))
                {
                    return file;
                }
            }
            return null;
        }

        /// <summary>Chooses the file of a role: the first whose name starts with the most likely word, else the largest.</summary>
        /// <param name="files">The files of the chart folder.</param>
        /// <param name="stems">The usual names of the role, most likely first.</param>
        /// <param name="isCandidate">Whether a file has the right kind.</param>
        /// <returns>The chosen file, or null when no file has the right kind.</returns>
        private static ScannedFile? Choose(IReadOnlyList<ScannedFile> files, string[] stems, Func<ScannedFile, bool> isCandidate)
        {
            foreach (var stem in stems)
            {
                foreach (var file in files)
                {
                    if (isCandidate(file) && Path.GetFileNameWithoutExtension(file.Name).StartsWith(stem, StringComparison.OrdinalIgnoreCase))
                    {
                        return file;
                    }
                }
            }
            ScannedFile? largest = null;
            foreach (var file in files)
            {
                if (isCandidate(file) && (largest is null || file.Length > largest.Length))
                {
                    largest = file;
                }
            }
            return largest;
        }

        /// <summary>Determines whether a file can be a chart's music.</summary>
        /// <param name="file">The file.</param>
        /// <returns>True for audio content, and for an unrecognised file with an audio extension.</returns>
        private static bool IsAudioCandidate(ScannedFile file)
        {
            return ChartFileSniffer.IsAudio(file.Kind) ||
                   (file.Kind == ChartFileKind.Unknown && GetSupportedExtension(file.Name, MajEnv.SupportedAudioFormats) is not null);
        }

        /// <summary>Determines whether a file can be a chart's cover.</summary>
        /// <param name="file">The file.</param>
        /// <returns>True for image content.</returns>
        private static bool IsImageCandidate(ScannedFile file)
        {
            return ChartFileSniffer.IsImage(file.Kind);
        }

        /// <summary>Determines whether a file can be a chart's video.</summary>
        /// <param name="file">The file.</param>
        /// <returns>True for video content, and for an unrecognised file with a video extension.</returns>
        private static bool IsVideoCandidate(ScannedFile file)
        {
            return ChartFileSniffer.IsVideo(file.Kind) ||
                   (file.Kind == ChartFileKind.Unknown && GetSupportedExtension(file.Name, MajEnv.SupportedVideoFormats) is not null);
        }

        /// <summary>Determines whether the game plays a file named as a track.</summary>
        /// <param name="file">The file.</param>
        /// <returns>False only when the content is audio the game does not play (M4A, FLAC).</returns>
        private static bool IsPlayableAudio(ScannedFile file)
        {
            return file.Kind is not (ChartFileKind.M4a or ChartFileKind.Flac);
        }

        /// <summary>Gets the track extension of a music file from its content, or its own extension when the content is not recognised.</summary>
        /// <param name="file">The music file.</param>
        /// <returns>The extension, or null when the game does not play the file.</returns>
        private static string? GetPlayableAudioExtension(ScannedFile file)
        {
            if (file.Kind == ChartFileKind.Unknown)
            {
                return GetSupportedExtension(file.Name, MajEnv.SupportedAudioFormats);
            }
            return ChartFileSniffer.GetPlayableAudioExtension(file.Kind);
        }

        /// <summary>Gets a file's extension when it is one of the given ones.</summary>
        /// <param name="name">The file name.</param>
        /// <param name="extensions">The dot-prefixed extensions.</param>
        /// <returns>The lower-case extension, or null when it is not in the list.</returns>
        private static string? GetSupportedExtension(string name, string[] extensions)
        {
            var extension = Path.GetExtension(name).ToLowerInvariant();
            return Array.IndexOf(extensions, extension) >= 0 ? extension : null;
        }
    }
}
