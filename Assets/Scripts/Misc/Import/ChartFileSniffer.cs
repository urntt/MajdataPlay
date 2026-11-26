using MajdataPlay.IO.Storage;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

#nullable enable
namespace MajdataPlay.Import
{
    /// <summary>
    /// What a file holds, judged by its content rather than by its name.
    /// </summary>
    internal enum ChartFileKind
    {
        /// <summary>Nothing recognised, or a binary file of another kind.</summary>
        Unknown,

        /// <summary>A ZIP archive; .adx and .pez packages are ZIP archives too.</summary>
        Zip,

        /// <summary>A RAR archive, which the game cannot unpack.</summary>
        Rar,

        /// <summary>A 7-Zip archive, which the game cannot unpack.</summary>
        SevenZip,

        /// <summary>A gzip, bzip2 or xz stream, which the game cannot unpack.</summary>
        CompressedStream,

        /// <summary>A PNG image.</summary>
        Png,

        /// <summary>A JPEG image.</summary>
        Jpeg,

        /// <summary>A GIF image.</summary>
        Gif,

        /// <summary>A WebP image.</summary>
        WebP,

        /// <summary>A BMP image.</summary>
        Bmp,

        /// <summary>MPEG audio (MP3), with or without an ID3 tag.</summary>
        Mp3,

        /// <summary>Ogg Vorbis audio.</summary>
        Ogg,

        /// <summary>Ogg Opus audio.</summary>
        Opus,

        /// <summary>WAVE audio.</summary>
        Wav,

        /// <summary>FLAC audio, which the game does not play.</summary>
        Flac,

        /// <summary>Raw AAC audio in ADTS frames.</summary>
        Aac,

        /// <summary>AAC or ALAC audio in an MPEG-4 container (.m4a), which the game does not play.</summary>
        M4a,

        /// <summary>An MPEG-4 video.</summary>
        Mp4,

        /// <summary>A QuickTime video.</summary>
        QuickTime,

        /// <summary>A Matroska or WebM video.</summary>
        Matroska,

        /// <summary>An AVI video, which the game does not play.</summary>
        Avi,

        /// <summary>A simai chart (maidata.txt).</summary>
        Simai,

        /// <summary>A Re:PhiEdit (RPE) chart, the JSON format of Phira.</summary>
        RpeChart,

        /// <summary>A PhiEdit (PEC) chart, the text format Phira also reads.</summary>
        PecChart,

        /// <summary>An official Phigros chart (JSON without RPE's META), which URC does not read yet.</summary>
        PhigrosChart,

        /// <summary>Other text, such as a readme or a Phira info file.</summary>
        Text,
    }

    /// <summary>
    /// Tells what a file is from its first bytes, so that charts are found whatever their files are named.
    /// </summary>
    /// <remarks>
    /// Binary formats are recognised by their signatures. Text is probed with Latin-1, which keeps every ASCII marker of
    /// simai, RPE and PEC whatever encoding the text really uses, and only the start of a long file is read.
    /// </remarks>
    internal static class ChartFileSniffer
    {
        /// <summary>The number of bytes read to recognise a binary format.</summary>
        internal const int HeadLength = 64;

        /// <summary>The number of bytes read to recognise a chart in a text file.</summary>
        internal const int TextProbeLength = 256 * 1024;

        /// <summary>The Latin-1 encoding, which maps every byte to one character.</summary>
        private static readonly Encoding s_latin1 = Encoding.GetEncoding(28591);

        /// <summary>Matches the start of a PEC chart: an offset alone on the first line, then <c>bp</c> lines (URC's looksLikePec).</summary>
        private static readonly Regex s_pecPattern = new(@"^\s*[+-]?\d+(\.\d+)?[ \t]*\r?\n[\s\S]*^\s*bp\s",
                                                         RegexOptions.Multiline | RegexOptions.CultureInvariant);

        /// <summary>
        /// Reads the start of a file and tells what it holds.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="fileName">The file name, used only to tell M4A from MP4 and to keep an unrecognised media extension.</param>
        /// <returns>The kind of the file; <see cref="ChartFileKind.Unknown"/> when it cannot be read.</returns>
        internal static ChartFileKind Sniff(StorageFile file, string fileName)
        {
            try
            {
                using var stream = file.OpenRead();
                var head = new byte[HeadLength];
                var headLength = ReadFully(stream, head, 0);
                var kind = SniffBinary(head.AsSpan(0, headLength), fileName);
                if (kind != ChartFileKind.Unknown)
                {
                    return kind;
                }
                var isUtf16 = headLength >= 2 && ((head[0] == 0xFF && head[1] == 0xFE) || (head[0] == 0xFE && head[1] == 0xFF));
                if (headLength < HeadLength || (!isUtf16 && head.AsSpan(0, headLength).IndexOf((byte)0) >= 0))
                {
                    return Sniff(head.AsSpan(0, headLength), fileName);
                }
                // Only a file that starts like text is read further, so large media files cost one small read.
                var buffer = new byte[TextProbeLength];
                head.CopyTo(buffer, 0);
                var length = headLength + ReadFully(stream, buffer, headLength);
                return Sniff(buffer.AsSpan(0, length), fileName);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return ChartFileKind.Unknown;
            }
        }

        /// <summary>
        /// Tells what a file holds from its first bytes.
        /// </summary>
        /// <param name="data">The start of the file, up to <see cref="TextProbeLength"/> bytes.</param>
        /// <param name="fileName">The file name, used only to tell M4A from MP4 and to keep an unrecognised media extension.</param>
        /// <returns>The kind of the file.</returns>
        internal static ChartFileKind Sniff(ReadOnlySpan<byte> data, string fileName)
        {
            var kind = SniffBinary(data, fileName);
            if (kind != ChartFileKind.Unknown)
            {
                return kind;
            }
            if (TryDecodeText(data, out var text))
            {
                return SniffText(text);
            }
            return FromExtension(fileName);
        }

        /// <summary>
        /// Tells what a text file holds.
        /// </summary>
        /// <param name="text">The start of the text.</param>
        /// <returns><see cref="ChartFileKind.Simai"/>, <see cref="ChartFileKind.RpeChart"/>, <see cref="ChartFileKind.PecChart"/>,
        /// <see cref="ChartFileKind.PhigrosChart"/> or <see cref="ChartFileKind.Text"/>.</returns>
        internal static ChartFileKind SniffText(string text)
        {
            var body = text.TrimStart('﻿', ' ', '\t', '\r', '\n');
            if (body.StartsWith("{", StringComparison.Ordinal))
            {
                if (body.IndexOf("\"META\"", StringComparison.Ordinal) >= 0)
                {
                    return ChartFileKind.RpeChart;
                }
                return body.IndexOf("judgeLineList", StringComparison.Ordinal) >= 0 ? ChartFileKind.PhigrosChart : ChartFileKind.Text;
            }
            if (body.IndexOf("&inote_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                body.StartsWith("&title=", StringComparison.OrdinalIgnoreCase))
            {
                return ChartFileKind.Simai;
            }
            return s_pecPattern.IsMatch(body) ? ChartFileKind.PecChart : ChartFileKind.Text;
        }

        /// <summary>Determines whether a kind is an archive.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns>True for ZIP, RAR, 7-Zip and compressed streams.</returns>
        internal static bool IsArchive(ChartFileKind kind)
        {
            return kind is ChartFileKind.Zip or ChartFileKind.Rar or ChartFileKind.SevenZip or ChartFileKind.CompressedStream;
        }

        /// <summary>Determines whether a kind is an image.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns>True for PNG, JPEG, GIF, WebP and BMP; the game decodes all of them as covers.</returns>
        internal static bool IsImage(ChartFileKind kind)
        {
            return kind is ChartFileKind.Png or ChartFileKind.Jpeg or ChartFileKind.Gif or ChartFileKind.WebP or ChartFileKind.Bmp;
        }

        /// <summary>Determines whether a kind is audio.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns>True for every audio kind, including the ones the game does not play.</returns>
        internal static bool IsAudio(ChartFileKind kind)
        {
            return kind is ChartFileKind.Mp3 or ChartFileKind.Ogg or ChartFileKind.Opus or ChartFileKind.Wav or
                           ChartFileKind.Flac or ChartFileKind.Aac or ChartFileKind.M4a;
        }

        /// <summary>Determines whether a kind is video.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns>True for every video kind, including the ones the game does not play.</returns>
        internal static bool IsVideo(ChartFileKind kind)
        {
            return kind is ChartFileKind.Mp4 or ChartFileKind.QuickTime or ChartFileKind.Matroska or ChartFileKind.Avi;
        }

        /// <summary>Determines whether a kind is a chart.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns>True for simai, RPE, PEC and official Phigros charts.</returns>
        internal static bool IsChart(ChartFileKind kind)
        {
            return kind is ChartFileKind.Simai or ChartFileKind.RpeChart or ChartFileKind.PecChart or ChartFileKind.PhigrosChart;
        }

        /// <summary>Gets the extension a track file of this kind needs so that the game plays it.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns>".mp3", ".ogg", ".opus", ".wav" or ".aac", or null for audio the game does not play.</returns>
        internal static string? GetPlayableAudioExtension(ChartFileKind kind)
        {
            return kind switch
            {
                ChartFileKind.Mp3 => ".mp3",
                ChartFileKind.Ogg => ".ogg",
                ChartFileKind.Opus => ".opus",
                ChartFileKind.Wav => ".wav",
                ChartFileKind.Aac => ".aac",
                _ => null,
            };
        }

        /// <summary>Gets the extension a video file of this kind needs so that the game plays it.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns>".mp4", ".mov" or ".mkv", or null for video the game does not play.</returns>
        internal static string? GetPlayableVideoExtension(ChartFileKind kind)
        {
            return kind switch
            {
                ChartFileKind.Mp4 => ".mp4",
                ChartFileKind.QuickTime => ".mov",
                ChartFileKind.Matroska => ".mkv",
                _ => null,
            };
        }

        /// <summary>Gets the name a cover image of this kind is stored under.</summary>
        /// <param name="kind">An image kind.</param>
        /// <returns><c>bg.jpg</c> for JPEG and <c>bg.png</c> for the other images, which the game decodes by content.</returns>
        internal static string GetCoverFileName(ChartFileKind kind)
        {
            return kind == ChartFileKind.Jpeg ? "bg.jpg" : "bg.png";
        }

        /// <summary>Gets a short name of a kind for messages, such as "RAR" or "M4A".</summary>
        /// <param name="kind">The kind.</param>
        /// <returns>The name of the format.</returns>
        internal static string GetFormatName(ChartFileKind kind)
        {
            return kind switch
            {
                ChartFileKind.SevenZip => "7z",
                ChartFileKind.CompressedStream => "gz/bz2/xz",
                ChartFileKind.QuickTime => "MOV",
                ChartFileKind.Matroska => "MKV",
                _ => kind.ToString().ToUpperInvariant(),
            };
        }

        /// <summary>Recognises binary formats by their signatures.</summary>
        /// <param name="head">The start of the file.</param>
        /// <param name="fileName">The file name, used to tell M4A from MP4.</param>
        /// <returns>The kind, or <see cref="ChartFileKind.Unknown"/> when no signature matches.</returns>
        private static ChartFileKind SniffBinary(ReadOnlySpan<byte> head, string fileName)
        {
            if (StartsWith(head, 0, 0x50, 0x4B) && head.Length >= 4 &&
                ((head[2] == 0x03 && head[3] == 0x04) || (head[2] == 0x05 && head[3] == 0x06) || (head[2] == 0x07 && head[3] == 0x08)))
            {
                return ChartFileKind.Zip;
            }
            if (StartsWith(head, 0, 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07))
            {
                return ChartFileKind.Rar;
            }
            if (StartsWith(head, 0, 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C))
            {
                return ChartFileKind.SevenZip;
            }
            if (StartsWith(head, 0, 0x1F, 0x8B) || StartsWith(head, 0, 0x42, 0x5A, 0x68) ||
                StartsWith(head, 0, 0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00))
            {
                return ChartFileKind.CompressedStream;
            }
            if (StartsWith(head, 0, 0x89, 0x50, 0x4E, 0x47))
            {
                return ChartFileKind.Png;
            }
            if (StartsWith(head, 0, 0xFF, 0xD8, 0xFF))
            {
                return ChartFileKind.Jpeg;
            }
            if (StartsWith(head, 0, 0x47, 0x49, 0x46, 0x38))
            {
                return ChartFileKind.Gif;
            }
            if (StartsWith(head, 0, 0x42, 0x4D) && head.Length >= 10 && head[6] == 0 && head[7] == 0 && head[8] == 0 && head[9] == 0)
            {
                return ChartFileKind.Bmp;
            }
            if (StartsWith(head, 0, 0x52, 0x49, 0x46, 0x46))
            {
                if (StartsWith(head, 8, 0x57, 0x41, 0x56, 0x45))
                {
                    return ChartFileKind.Wav;
                }
                if (StartsWith(head, 8, 0x57, 0x45, 0x42, 0x50))
                {
                    return ChartFileKind.WebP;
                }
                if (StartsWith(head, 8, 0x41, 0x56, 0x49, 0x20))
                {
                    return ChartFileKind.Avi;
                }
            }
            if (StartsWith(head, 0, 0x4F, 0x67, 0x67, 0x53))
            {
                return IndexOf(head, "OpusHead") >= 0 ? ChartFileKind.Opus : ChartFileKind.Ogg;
            }
            if (StartsWith(head, 0, 0x66, 0x4C, 0x61, 0x43))
            {
                return ChartFileKind.Flac;
            }
            if (StartsWith(head, 0, 0x49, 0x44, 0x33))
            {
                return ChartFileKind.Mp3;
            }
            if (head.Length >= 2 && ((head[0] == 0xFF && head[1] == 0xFE) || (head[0] == 0xFE && head[1] == 0xFF)))
            {
                // A UTF-16 byte order mark, which would otherwise pass for an MPEG frame sync; the text check reads it.
                return ChartFileKind.Unknown;
            }
            if (head.Length >= 3 && head[0] == 0xFF && (head[1] & 0xE0) == 0xE0)
            {
                // The layer bits are 00 in AAC's ADTS header and 01-11 in MPEG audio layers III-I.
                if ((head[1] & 0x06) == 0)
                {
                    return (head[1] & 0xF0) == 0xF0 ? ChartFileKind.Aac : ChartFileKind.Unknown;
                }
                // An MPEG audio header never has bitrate index 15 or the reserved sample rate 3.
                var isValidHeader = (head[2] & 0xF0) != 0xF0 && (head[2] & 0x0C) != 0x0C;
                return isValidHeader ? ChartFileKind.Mp3 : ChartFileKind.Unknown;
            }
            if (StartsWith(head, 4, 0x66, 0x74, 0x79, 0x70) && head.Length >= 12)
            {
                return SniffIsoMedia(head.Slice(8, 4), fileName);
            }
            if (StartsWith(head, 0, 0x1A, 0x45, 0xDF, 0xA3))
            {
                return ChartFileKind.Matroska;
            }
            return ChartFileKind.Unknown;
        }

        /// <summary>Tells an ISO media file (MP4, M4A, MOV) by its major brand.</summary>
        /// <param name="brand">The four bytes of the major brand.</param>
        /// <param name="fileName">The file name; .m4a and similar names mark audio when the brand is generic.</param>
        /// <returns>The kind of the file.</returns>
        private static ChartFileKind SniffIsoMedia(ReadOnlySpan<byte> brand, string fileName)
        {
            var name = s_latin1.GetString(brand.ToArray());
            if (name is "M4A " or "M4B " or "M4P ")
            {
                return ChartFileKind.M4a;
            }
            if (name == "qt  ")
            {
                return ChartFileKind.QuickTime;
            }
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension is ".m4a" or ".m4b" or ".aac" or ".alac" ? ChartFileKind.M4a : ChartFileKind.Mp4;
        }

        /// <summary>Guesses a media kind from the extension when the content has no known signature.</summary>
        /// <param name="fileName">The file name.</param>
        /// <returns>An audio or video kind for a matching extension, else <see cref="ChartFileKind.Unknown"/>.</returns>
        private static ChartFileKind FromExtension(string fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".mp3" => ChartFileKind.Mp3,
                ".aac" => ChartFileKind.Aac,
                _ => ChartFileKind.Unknown,
            };
        }

        /// <summary>Decodes the start of a file as text when it looks like text.</summary>
        /// <param name="data">The start of the file.</param>
        /// <param name="text">The text, decoded with UTF-16 when it has a UTF-16 byte order mark and with Latin-1 otherwise.</param>
        /// <returns>False when the data holds NUL bytes outside UTF-16, which marks a binary file.</returns>
        private static bool TryDecodeText(ReadOnlySpan<byte> data, out string text)
        {
            if (data.Length >= 2 && ((data[0] == 0xFF && data[1] == 0xFE) || (data[0] == 0xFE && data[1] == 0xFF)))
            {
                var encoding = data[0] == 0xFF ? Encoding.Unicode : Encoding.BigEndianUnicode;
                text = encoding.GetString(data.Slice(2, (data.Length - 2) & ~1).ToArray());
                return true;
            }
            if (data.IndexOf((byte)0) >= 0)
            {
                text = string.Empty;
                return false;
            }
            text = s_latin1.GetString(data.ToArray());
            return true;
        }

        /// <summary>Determines whether data has the given bytes at an offset.</summary>
        /// <param name="data">The data.</param>
        /// <param name="offset">The offset of the first byte to compare.</param>
        /// <param name="expected">The expected bytes.</param>
        /// <returns>True when every expected byte is there.</returns>
        private static bool StartsWith(ReadOnlySpan<byte> data, int offset, params byte[] expected)
        {
            if (data.Length < offset + expected.Length)
            {
                return false;
            }
            return data.Slice(offset, expected.Length).SequenceEqual(expected);
        }

        /// <summary>Finds an ASCII marker in data.</summary>
        /// <param name="data">The data.</param>
        /// <param name="marker">The ASCII marker.</param>
        /// <returns>The offset of the marker, or -1 when it is missing.</returns>
        private static int IndexOf(ReadOnlySpan<byte> data, string marker)
        {
            return data.IndexOf(Encoding.ASCII.GetBytes(marker));
        }

        /// <summary>Reads until a buffer is full or the stream ends.</summary>
        /// <param name="stream">The stream.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">Where in the buffer to start writing.</param>
        /// <returns>The number of bytes read.</returns>
        private static int ReadFully(Stream stream, byte[] buffer, int offset)
        {
            var total = 0;
            while (offset + total < buffer.Length)
            {
                var read = stream.Read(buffer, offset + total, buffer.Length - offset - total);
                if (read == 0)
                {
                    break;
                }
                total += read;
            }
            return total;
        }
    }
}
