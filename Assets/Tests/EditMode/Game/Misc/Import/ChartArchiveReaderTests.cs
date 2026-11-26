using System.Collections.Generic;
using System.Text;
using MajdataPlay.Import;
using MajdataPlay.Tests;
using NUnit.Framework;

#nullable enable
namespace MajdataPlay.Tests.Game.Misc.Import
{
    /// <summary>
    /// Covers how <see cref="ChartArchiveReader"/> decodes ZIP entry names that tools stored in legacy code pages, and which
    /// entries it never extracts.
    /// </summary>
    public class ChartArchiveReaderTests
    {
        /// <summary>Gets a code page encoding through the reader, which registers the code page provider.</summary>
        /// <param name="codePage">The code page.</param>
        /// <returns>The encoding.</returns>
        private static Encoding GetEncodingOrIgnore(int codePage)
        {
            var encoding = PrivateMember.InvokeStatic<Encoding?>(typeof(ChartArchiveReader), "TryGetStrictEncoding", codePage);
            if (encoding is null)
            {
                Assert.Ignore($"Code page {codePage} is not available in this runtime.");
            }
            return encoding!;
        }

        /// <summary>Decodes names as the reader does, with the library's own decoding as the starting point.</summary>
        /// <param name="raw">The raw names.</param>
        /// <param name="encodingName">The chosen encoding.</param>
        /// <returns>The decoded names.</returns>
        private static List<string> Decode(IReadOnlyList<ChartArchiveReader.RawEntryName> raw, out string? encodingName)
        {
            var libraryNames = new List<string>();
            foreach (var name in raw)
            {
                libraryNames.Add(Encoding.UTF8.GetString(name.Bytes));
            }
            return ChartArchiveReader.DecodeNames(libraryNames, raw, out encodingName);
        }

        /// <summary>Checks that UTF-8 and ASCII names are left as the library read them.</summary>
        [Test]
        public void DecodeNames_KeepsUtf8Names()
        {
            var raw = new[]
            {
                new ChartArchiveReader.RawEntryName(true, Encoding.UTF8.GetBytes("中文/maidata.txt")),
                new ChartArchiveReader.RawEntryName(false, Encoding.ASCII.GetBytes("Song/track.mp3")),
            };

            var names = Decode(raw, out var encodingName);

            Assert.That(names, Is.EqualTo(new[] { "中文/maidata.txt", "Song/track.mp3" }));
            Assert.That(encodingName, Is.Null);
        }

        /// <summary>Checks that GBK names are decoded, also when a tool set the UTF-8 flag on them.</summary>
        [Test]
        public void DecodeNames_ReadsGbk()
        {
            var gbk = GetEncodingOrIgnore(936);
            var raw = new[]
            {
                new ChartArchiveReader.RawEntryName(false, gbk.GetBytes("春日影/maidata.txt")),
                new ChartArchiveReader.RawEntryName(true, gbk.GetBytes("夏日影/maidata.txt")),
            };

            var names = Decode(raw, out _);

            Assert.That(names, Is.EqualTo(new[] { "春日影/maidata.txt", "夏日影/maidata.txt" }));
        }

        /// <summary>Checks that Shift-JIS names with kana are read as Shift-JIS rather than as GBK, which accepts the same bytes.</summary>
        [Test]
        public void DecodeNames_ReadsShiftJis()
        {
            var shiftJis = GetEncodingOrIgnore(932);
            var raw = new[] { new ChartArchiveReader.RawEntryName(false, shiftJis.GetBytes("曲フォルダ/ジャケット.png")) };

            var names = Decode(raw, out _);

            Assert.That(names, Is.EqualTo(new[] { "曲フォルダ/ジャケット.png" }));
        }

        /// <summary>Checks that names no encoding reads stay distinct instead of collapsing into one garbled name.</summary>
        [Test]
        public void DecodeNames_KeepsUnreadableNamesDistinct()
        {
            var raw = new[]
            {
                new ChartArchiveReader.RawEntryName(false, new byte[] { 0x80, 0x2F, 0x61 }),
                new ChartArchiveReader.RawEntryName(false, new byte[] { 0xFF, 0x2F, 0x61 }),
            };

            var names = Decode(raw, out _);

            Assert.That(names[0], Is.Not.EqualTo(names[1]));
            Assert.That(names[0], Does.EndWith("/a"));
        }

        /// <summary>Checks which entries are macOS metadata.</summary>
        [Test]
        public void IsMetadata_SkipsMacMetadata()
        {
            Assert.That(ChartArchiveReader.IsMetadata("__MACOSX/Song/._maidata.txt"), Is.True);
            Assert.That(ChartArchiveReader.IsMetadata("Song/._bg.png"), Is.True);
            Assert.That(ChartArchiveReader.IsMetadata("Song/.DS_Store"), Is.True);
            Assert.That(ChartArchiveReader.IsMetadata("Song/maidata.txt"), Is.False);
        }

        /// <summary>Checks that names are made valid on every platform.</summary>
        [Test]
        public void ToSafeName_ReplacesInvalidCharacters()
        {
            Assert.That(ChartArchiveReader.ToSafeName("a:b*c?"), Is.EqualTo("a_b_c_"));
            Assert.That(ChartArchiveReader.ToSafeName("Song. "), Is.EqualTo("Song"));
            Assert.That(ChartArchiveReader.ToSafeName("..."), Is.EqualTo("_"));
        }
    }
}
