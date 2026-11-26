using Cysharp.Threading.Tasks;
using MajdataPlay.Diagnostics;
using MajdataPlay.IO.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace MajdataPlay.Import
{
    /// <summary>
    /// Extracts ZIP archives (.zip, .adx, .pez or any name) for the chart importer, with readable entry names whatever tool
    /// made the archive.
    /// </summary>
    /// <remarks>
    /// Many Windows tools store entry names in the system code page (GBK in China, Shift-JIS in Japan) without the
    /// UTF-8 flag, which the ZIP library would decode as UTF-8 and garble. The reader takes the raw names from the
    /// central directory and decodes the unflagged ones (and flagged ones that are not UTF-8) with UTF-8 when it reads all of
    /// them, else with Shift-JIS when they read as Japanese, else with GBK,
    /// and otherwise keeps garbled names distinct with a short hash, so that no two entries land on the same path.
    /// macOS metadata (<c>__MACOSX</c>, <c>._*</c>, <c>.DS_Store</c>) is skipped.
    /// </remarks>
    internal static class ChartArchiveReader
    {
        /// <summary>The code pages tried, after UTF-8, for entry names without the UTF-8 flag: GBK, then Shift-JIS.</summary>
        private static readonly int[] s_legacyCodePages = { 936, 932 };

        /// <summary>Strict UTF-8, which fails on bytes that are not UTF-8.</summary>
        private static readonly Encoding s_strictUtf8 = new UTF8Encoding(false, true);

        /// <summary>Whether the code page encodings of System.Text.Encoding.CodePages have been registered.</summary>
        private static bool s_isCodePageProviderRegistered;

        /// <summary>The raw name of one central directory entry.</summary>
        internal readonly struct RawEntryName
        {
            /// <summary>Initializes a new instance of the <see cref="RawEntryName"/> struct.</summary>
            /// <param name="isUtf8">Whether the entry has the UTF-8 flag (general purpose bit 11).</param>
            /// <param name="bytes">The name bytes as stored.</param>
            public RawEntryName(bool isUtf8, byte[] bytes)
            {
                IsUtf8 = isUtf8;
                Bytes = bytes;
            }

            /// <summary>Gets a value indicating whether the entry has the UTF-8 flag.</summary>
            public bool IsUtf8 { get; }

            /// <summary>Gets the name bytes as stored.</summary>
            public byte[] Bytes { get; }
        }

        /// <summary>
        /// Extracts an archive into a directory, rejecting entries that would leave it.
        /// </summary>
        /// <param name="archiveFile">The archive.</param>
        /// <param name="destination">The empty directory receiving the entries.</param>
        /// <param name="onProgress">Receives the extracted share from 0 to 1 after each entry, on the calling thread.</param>
        /// <returns>The encoding the unflagged entry names were decoded with, or null when every name was UTF-8 or ASCII.</returns>
        /// <exception cref="InvalidDataException">The file is not a readable ZIP archive.</exception>
        /// <exception cref="ChartArchivePathException">An entry escapes the destination directory.</exception>
        /// <exception cref="IOException">Reading the archive or writing an entry failed.</exception>
        internal static async ValueTask<string?> ExtractAsync(StorageFile archiveFile, StorageDirectory destination, Action<double>? onProgress)
        {
            StorageFile? localCopy = null;
            var stream = archiveFile.OpenRead();
            try
            {
                if (!stream.CanSeek)
                {
                    // Content URIs give forward-only streams; a local copy lets the central directory be read directly.
                    stream.Dispose();
                    localCopy = MajEnv.TempDirectory.CreateFile(Guid.NewGuid().ToString("N") + ".zip");
                    using (var input = archiveFile.OpenRead())
                    using (var output = localCopy.OpenWrite())
                    {
                        input.CopyTo(output);
                    }
                    stream = localCopy.OpenRead();
                }
                var rawNames = TryReadCentralDirectoryNames(stream);
                stream.Position = 0;
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                var entries = archive.Entries;
                var names = DecodeNames(entries, rawNames, out var encodingName);
                var totalSize = 0L;
                foreach (var entry in entries)
                {
                    totalSize += entry.Length;
                }
                var extractedSize = 0L;
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var name = names[i];
                    if (name.EndsWith("/", StringComparison.Ordinal) || name.EndsWith("\\", StringComparison.Ordinal) || IsMetadata(name))
                    {
                        continue;
                    }
                    string[] segments;
                    try
                    {
                        // Each entry is walked one child name at a time, so a rooted or traversing entry can never leave the destination.
                        segments = FileSystem.Path.Split(name);
                    }
                    catch (ArgumentException exception)
                    {
                        throw new ChartArchivePathException(name, exception);
                    }
                    if (segments.Length == 0)
                    {
                        continue;
                    }
                    for (var s = 0; s < segments.Length; s++)
                    {
                        segments[s] = ToSafeName(segments[s]);
                    }
                    var parent = destination.CreateDirectoryAt(segments.AsSpan(0, segments.Length - 1));
                    var file = FileSystem.File.Create(parent.Location, segments[segments.Length - 1], overwrite: true);
                    using (var entryStream = entry.Open())
                    using (var outputStream = file.OpenWrite())
                    {
                        entryStream.CopyTo(outputStream);
                    }
                    file.SetLastWriteTime(entry.LastWriteTime.DateTime);
                    extractedSize += entry.Length;
                    onProgress?.Invoke(totalSize == 0 ? 1d : extractedSize / (double)totalSize);
                    await UniTask.Yield();
                }
                return encodingName;
            }
            finally
            {
                stream.Dispose();
                if (localCopy is not null)
                {
                    TryDelete(localCopy);
                }
            }
        }

        /// <summary>
        /// Decodes the entry names of an archive.
        /// </summary>
        /// <param name="entries">The entries as the ZIP library read them.</param>
        /// <param name="rawNames">The raw names from the central directory in the same order, or null when they could not be read.</param>
        /// <param name="encodingName">The encoding chosen for unflagged non-ASCII names, or null when none was needed.</param>
        /// <returns>The decoded full name of every entry, in order.</returns>
        internal static List<string> DecodeNames(IReadOnlyList<ZipArchiveEntry> entries, IReadOnlyList<RawEntryName>? rawNames, out string? encodingName)
        {
            var libraryNames = new List<string>(entries.Count);
            foreach (var entry in entries)
            {
                libraryNames.Add(entry.FullName);
            }
            return DecodeNames(libraryNames, rawNames, out encodingName);
        }

        /// <summary>
        /// Decodes entry names, replacing the library's decoding of names without the UTF-8 flag.
        /// </summary>
        /// <param name="libraryNames">The names as the ZIP library decoded them.</param>
        /// <param name="rawNames">The raw names from the central directory in the same order, or null when they could not be read.</param>
        /// <param name="encodingName">The encoding chosen for unflagged non-ASCII names, or null when none was needed.</param>
        /// <returns>The decoded full name of every entry, in order.</returns>
        internal static List<string> DecodeNames(IReadOnlyList<string> libraryNames, IReadOnlyList<RawEntryName>? rawNames, out string? encodingName)
        {
            encodingName = null;
            var names = new List<string>(libraryNames);
            if (rawNames is null || rawNames.Count != libraryNames.Count)
            {
                return names;
            }
            var legacy = new List<int>();
            for (var i = 0; i < rawNames.Count; i++)
            {
                // Some tools set the UTF-8 flag on names they wrote in the system code page, so the flag alone is not trusted.
                if (HasHighByte(rawNames[i].Bytes) && (!rawNames[i].IsUtf8 || !IsUtf8(rawNames[i].Bytes)))
                {
                    legacy.Add(i);
                }
            }
            if (legacy.Count == 0)
            {
                return names;
            }
            var encoding = PickEncoding(legacy, rawNames);
            encodingName = encoding?.WebName ?? "unknown";
            foreach (var i in legacy)
            {
                names[i] = encoding is null ? DecodeLossy(rawNames[i].Bytes) : encoding.GetString(rawNames[i].Bytes);
            }
            return names;
        }

        /// <summary>
        /// Decodes text in a legacy code page, such as a maidata.txt saved as GBK or Shift-JIS.
        /// </summary>
        /// <param name="bytes">The text, which is not UTF-8.</param>
        /// <param name="encodingName">The encoding used, or null when none reads the text.</param>
        /// <returns>The text, or null when neither GBK nor Shift-JIS reads it.</returns>
        internal static string? DecodeLegacyText(byte[] bytes, out string? encodingName)
        {
            var raw = new[] { new RawEntryName(false, bytes) };
            var encoding = PickEncoding(new List<int> { 0 }, raw);
            if (encoding is null || ReferenceEquals(encoding, s_strictUtf8))
            {
                encodingName = null;
                return null;
            }
            encodingName = encoding.WebName;
            return encoding.GetString(bytes);
        }

        /// <summary>
        /// Reads the raw entry names and their UTF-8 flags from the central directory.
        /// </summary>
        /// <param name="stream">A seekable stream over the whole archive.</param>
        /// <returns>The names in central directory order, or null when the directory cannot be read (the library's names are used then).</returns>
        internal static List<RawEntryName>? TryReadCentralDirectoryNames(Stream stream)
        {
            try
            {
                return ReadCentralDirectoryNames(stream);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or NotSupportedException or OverflowException)
            {
                MajDebug.LogWarning(nameof(ChartArchiveReader), $"Could not read the raw entry names: {e.Message}");
                return null;
            }
        }

        /// <summary>Reads the central directory of a ZIP archive, following ZIP64 records when present.</summary>
        /// <param name="stream">A seekable stream over the whole archive.</param>
        /// <returns>The names in central directory order, or null when no end of central directory record is found.</returns>
        /// <exception cref="InvalidDataException">The central directory is malformed.</exception>
        private static List<RawEntryName>? ReadCentralDirectoryNames(Stream stream)
        {
            const uint EndSignature = 0x06054B50;
            const uint Zip64LocatorSignature = 0x07064B50;
            const uint Zip64EndSignature = 0x06064B50;
            const uint EntrySignature = 0x02014B50;
            const int EndLength = 22;
            var length = stream.Length;
            var tailLength = (int)Math.Min(length, EndLength + ushort.MaxValue);
            var tail = ReadAt(stream, length - tailLength, tailLength);
            var endOffset = -1;
            for (var i = tail.Length - EndLength; i >= 0; i--)
            {
                if (BitConverter.ToUInt32(tail, i) == EndSignature)
                {
                    endOffset = i;
                    break;
                }
            }
            if (endOffset < 0)
            {
                return null;
            }
            long count = BitConverter.ToUInt16(tail, endOffset + 10);
            long directoryLength = BitConverter.ToUInt32(tail, endOffset + 12);
            long directoryOffset = BitConverter.ToUInt32(tail, endOffset + 16);
            if (count == ushort.MaxValue || directoryLength == uint.MaxValue || directoryOffset == uint.MaxValue)
            {
                var locatorOffset = endOffset - 20;
                if (locatorOffset < 0 || BitConverter.ToUInt32(tail, locatorOffset) != Zip64LocatorSignature)
                {
                    throw new InvalidDataException("The ZIP64 end of central directory locator is missing.");
                }
                var zip64End = ReadAt(stream, (long)BitConverter.ToUInt64(tail, locatorOffset + 8), 56);
                if (BitConverter.ToUInt32(zip64End, 0) != Zip64EndSignature)
                {
                    throw new InvalidDataException("The ZIP64 end of central directory record is missing.");
                }
                count = (long)BitConverter.ToUInt64(zip64End, 32);
                directoryLength = (long)BitConverter.ToUInt64(zip64End, 40);
                directoryOffset = (long)BitConverter.ToUInt64(zip64End, 48);
            }
            var directory = ReadAt(stream, directoryOffset, checked((int)directoryLength));
            var names = new List<RawEntryName>((int)Math.Min(count, 65536));
            var offset = 0;
            for (var i = 0L; i < count; i++)
            {
                if (offset + 46 > directory.Length || BitConverter.ToUInt32(directory, offset) != EntrySignature)
                {
                    throw new InvalidDataException("A central directory entry is malformed.");
                }
                var flags = BitConverter.ToUInt16(directory, offset + 8);
                var nameLength = BitConverter.ToUInt16(directory, offset + 28);
                var extraLength = BitConverter.ToUInt16(directory, offset + 30);
                var commentLength = BitConverter.ToUInt16(directory, offset + 32);
                if (offset + 46 + nameLength > directory.Length)
                {
                    throw new InvalidDataException("A central directory entry name is truncated.");
                }
                var name = new byte[nameLength];
                Buffer.BlockCopy(directory, offset + 46, name, 0, nameLength);
                names.Add(new RawEntryName((flags & 0x0800) != 0, name));
                offset += 46 + nameLength + extraLength + commentLength;
            }
            return names;
        }

        /// <summary>Picks the encoding that decodes every unflagged non-ASCII name.</summary>
        /// <remarks>
        /// UTF-8 wins when it reads every name. GBK and Shift-JIS read almost the same bytes, so between them the decoded text
        /// decides: Shift-JIS when it gives Japanese kana and no half-width katakana (which is what Chinese text turns into
        /// when read as Shift-JIS), GBK otherwise.
        /// </remarks>
        /// <param name="legacy">The indices of the names to decode.</param>
        /// <param name="rawNames">The raw names.</param>
        /// <returns>UTF-8, GBK or Shift-JIS, or null when none reads all of them.</returns>
        private static Encoding? PickEncoding(List<int> legacy, IReadOnlyList<RawEntryName> rawNames)
        {
            if (TryDecodeAll(s_strictUtf8, legacy, rawNames, out _))
            {
                return s_strictUtf8;
            }
            var gbk = TryGetStrictEncoding(s_legacyCodePages[0]);
            var shiftJis = TryGetStrictEncoding(s_legacyCodePages[1]);
            string? japanese = null;
            var isShiftJis = shiftJis is not null && TryDecodeAll(shiftJis, legacy, rawNames, out japanese);
            if (isShiftJis && LooksJapanese(japanese!))
            {
                return shiftJis;
            }
            if (gbk is not null && TryDecodeAll(gbk, legacy, rawNames, out _))
            {
                return gbk;
            }
            return isShiftJis ? shiftJis : null;
        }

        /// <summary>Decodes every given name with an encoding that throws on bad bytes.</summary>
        /// <param name="encoding">The encoding.</param>
        /// <param name="legacy">The indices of the names to decode.</param>
        /// <param name="rawNames">The raw names.</param>
        /// <param name="text">All decoded names joined, or null when one could not be decoded.</param>
        /// <returns>True when every name was decoded.</returns>
        private static bool TryDecodeAll(Encoding encoding, List<int> legacy, IReadOnlyList<RawEntryName> rawNames, out string? text)
        {
            var builder = new StringBuilder();
            try
            {
                foreach (var i in legacy)
                {
                    builder.Append(encoding.GetString(rawNames[i].Bytes)).Append('\n');
                }
            }
            catch (DecoderFallbackException)
            {
                text = null;
                return false;
            }
            text = builder.ToString();
            return true;
        }

        /// <summary>Determines whether names read as Shift-JIS look like Japanese.</summary>
        /// <param name="text">The names decoded with Shift-JIS.</param>
        /// <returns>True when they hold hiragana or full-width katakana and no half-width katakana.</returns>
        private static bool LooksJapanese(string text)
        {
            var hasKana = false;
            foreach (var c in text)
            {
                if (c >= '\uFF61' && c <= '\uFF9F')
                {
                    return false;
                }
                if (c >= '\u3041' && c <= '\u30FF')
                {
                    hasKana = true;
                }
            }
            return hasKana;
        }

        /// <summary>Gets a code page encoding that throws on bytes it cannot decode.</summary>
        /// <param name="codePage">The code page.</param>
        /// <returns>The encoding, or null when this runtime has no such code page.</returns>
        private static Encoding? TryGetStrictEncoding(int codePage)
        {
            try
            {
                if (!s_isCodePageProviderRegistered)
                {
                    s_isCodePageProviderRegistered = true;
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                }
                return Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or TypeLoadException or IOException)
            {
                MajDebug.LogWarning(nameof(ChartArchiveReader), $"Code page {codePage} is not available: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Decodes a name no encoding reads: each path segment that is not UTF-8 gets its bad bytes replaced and a short hash of its bytes,
        /// so that two different names never decode to the same path.
        /// </summary>
        /// <param name="bytes">The raw name.</param>
        /// <returns>The decoded name.</returns>
        private static string DecodeLossy(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length);
            var start = 0;
            for (var i = 0; i <= bytes.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != (byte)'/')
                {
                    continue;
                }
                var segment = new byte[i - start];
                Buffer.BlockCopy(bytes, start, segment, 0, segment.Length);
                try
                {
                    builder.Append(s_strictUtf8.GetString(segment));
                }
                catch (DecoderFallbackException)
                {
                    var lossy = Encoding.UTF8.GetString(segment).Replace('�', '_');
                    builder.Append(lossy).Append('~').Append(Fnv1a(segment).ToString("x8"));
                }
                if (i < bytes.Length)
                {
                    builder.Append('/');
                }
                start = i + 1;
            }
            return builder.ToString();
        }

        /// <summary>Makes a name valid on every platform by replacing characters Windows rejects and trailing dots and spaces.</summary>
        /// <param name="name">A single path segment.</param>
        /// <returns>The safe name.</returns>
        internal static string ToSafeName(string name)
        {
            var builder = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                builder.Append(c < 32 || "\\/:*?\"<>|".IndexOf(c) >= 0 ? '_' : c);
            }
            var safe = builder.ToString().TrimEnd('.', ' ');
            return safe.Length == 0 ? "_" : safe;
        }

        /// <summary>Determines whether an entry is macOS metadata that is never imported.</summary>
        /// <param name="name">The full entry name.</param>
        /// <returns>True for anything under <c>__MACOSX</c>, AppleDouble <c>._*</c> files and <c>.DS_Store</c>.</returns>
        internal static bool IsMetadata(string name)
        {
            foreach (var segment in name.Split('/', '\\'))
            {
                if (string.Equals(segment, "__MACOSX", StringComparison.OrdinalIgnoreCase) ||
                    segment.StartsWith("._", StringComparison.Ordinal) ||
                    string.Equals(segment, ".DS_Store", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Determines whether bytes are valid UTF-8.</summary>
        /// <param name="bytes">The bytes.</param>
        /// <returns>True when strict UTF-8 decodes them.</returns>
        private static bool IsUtf8(byte[] bytes)
        {
            try
            {
                s_strictUtf8.GetCharCount(bytes);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        /// <summary>Determines whether a name has bytes outside ASCII.</summary>
        /// <param name="bytes">The raw name.</param>
        /// <returns>True when a byte is 0x80 or above.</returns>
        private static bool HasHighByte(byte[] bytes)
        {
            foreach (var b in bytes)
            {
                if (b >= 0x80)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Computes the 32-bit FNV-1a hash of bytes.</summary>
        /// <param name="bytes">The bytes.</param>
        /// <returns>The hash.</returns>
        private static uint Fnv1a(byte[] bytes)
        {
            var hash = 2166136261u;
            foreach (var b in bytes)
            {
                hash = unchecked((hash ^ b) * 16777619u);
            }
            return hash;
        }

        /// <summary>Reads a block at an offset of a seekable stream.</summary>
        /// <param name="stream">The stream.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="count">The number of bytes.</param>
        /// <returns>The bytes.</returns>
        /// <exception cref="InvalidDataException">The stream ends before the block does.</exception>
        private static byte[] ReadAt(Stream stream, long offset, int count)
        {
            if (offset < 0 || offset + count > stream.Length)
            {
                throw new InvalidDataException("A ZIP record lies outside the file.");
            }
            stream.Position = offset;
            var buffer = new byte[count];
            var total = 0;
            while (total < count)
            {
                var read = stream.Read(buffer, total, count - total);
                if (read == 0)
                {
                    throw new InvalidDataException("The ZIP file ended early.");
                }
                total += read;
            }
            return buffer;
        }

        /// <summary>Deletes a temporary copy without masking the outcome of the extraction.</summary>
        /// <param name="file">The copy.</param>
        private static void TryDelete(StorageFile file)
        {
            try
            {
                file.Delete();
            }
            catch (Exception e)
            {
                MajDebug.LogWarning(nameof(ChartArchiveReader), $"Failed to delete {file.Location}: {e.Message}");
            }
        }
    }

    /// <summary>
    /// The exception thrown when an archive entry would be written outside the extraction directory.
    /// </summary>
    internal sealed class ChartArchivePathException : IOException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChartArchivePathException"/> class.
        /// </summary>
        /// <param name="entryName">The full name of the entry.</param>
        /// <param name="innerException">The exception that rejected the path.</param>
        public ChartArchivePathException(string entryName, Exception innerException)
            : base("Zip entry escapes destination: " + entryName, innerException)
        {
            EntryName = entryName;
        }

        /// <summary>Gets the full name of the entry.</summary>
        public string EntryName { get; }
    }
}
