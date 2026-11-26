using System.Text;
using MajdataPlay.Import;
using MajdataPlay.Tests.Game.Misc.Support;
using NUnit.Framework;

#nullable enable
namespace MajdataPlay.Tests.Game.Misc.Import
{
    /// <summary>
    /// Covers how <see cref="ChartFileSniffer"/> tells files apart by their first bytes, whatever they are named.
    /// </summary>
    public class ChartFileSnifferTests
    {
        /// <summary>Checks the binary signatures, each under a misleading name.</summary>
        [Test]
        public void Sniff_RecognisesBinaryFormatsByContent()
        {
            Assert.That(ChartFileSniffer.Sniff(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0, 0 }, "song.txt"), Is.EqualTo(ChartFileKind.Zip));
            Assert.That(ChartFileSniffer.Sniff(MediaFixture.Rar, "pack.zip"), Is.EqualTo(ChartFileKind.Rar));
            Assert.That(ChartFileSniffer.Sniff(new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C, 0, 4 }, "pack.zip"), Is.EqualTo(ChartFileKind.SevenZip));
            Assert.That(ChartFileSniffer.Sniff(MediaFixture.Png, "cover.jpg"), Is.EqualTo(ChartFileKind.Png));
            Assert.That(ChartFileSniffer.Sniff(MediaFixture.Jpeg, "cover.png"), Is.EqualTo(ChartFileKind.Jpeg));
            Assert.That(ChartFileSniffer.Sniff(MediaFixture.WebP, "cover.png"), Is.EqualTo(ChartFileKind.WebP));
            Assert.That(ChartFileSniffer.Sniff(MediaFixture.Mp3, "track.ogg"), Is.EqualTo(ChartFileKind.Mp3));
            Assert.That(ChartFileSniffer.Sniff(MediaFixture.Ogg, "track.mp3"), Is.EqualTo(ChartFileKind.Ogg));
            Assert.That(ChartFileSniffer.Sniff(MediaFixture.M4a, "track.mp3"), Is.EqualTo(ChartFileKind.M4a));
            Assert.That(ChartFileSniffer.Sniff(MediaFixture.Mp4, "pv.bin"), Is.EqualTo(ChartFileKind.Mp4));
            Assert.That(ChartFileSniffer.Sniff(MediaFixture.Mp4, "track.m4a"), Is.EqualTo(ChartFileKind.M4a));
        }

        /// <summary>Checks that charts are told apart by their text.</summary>
        [Test]
        public void Sniff_RecognisesChartText()
        {
            Assert.That(Sniff(ChartFixture.CreateMaidata("Song"), "notes.txt"), Is.EqualTo(ChartFileKind.Simai));
            Assert.That(Sniff("{\"BPMList\":[],\"META\":{},\"judgeLineList\":[]}", "chart.txt"), Is.EqualTo(ChartFileKind.RpeChart));
            Assert.That(Sniff(MediaFixture.PecChart, "chart.json"), Is.EqualTo(ChartFileKind.PecChart));
            Assert.That(Sniff("{\"formatVersion\":3,\"judgeLineList\":[]}", "chart.json"), Is.EqualTo(ChartFileKind.PhigrosChart));
            Assert.That(Sniff("name: Song\nchart: chart.json\n", "info.yml"), Is.EqualTo(ChartFileKind.Text));
        }

        /// <summary>Checks that UTF-16 chart text is read as text and not as MPEG audio, whose frame sync its byte order mark resembles.</summary>
        [Test]
        public void Sniff_Utf16Text_IsNotAudio()
        {
            var bytes = Encoding.Unicode.GetPreamble();
            var text = Encoding.Unicode.GetBytes(ChartFixture.CreateMaidata("Song"));
            var data = new byte[bytes.Length + text.Length];
            bytes.CopyTo(data, 0);
            text.CopyTo(data, bytes.Length);

            Assert.That(ChartFileSniffer.Sniff(data, "maidata.txt"), Is.EqualTo(ChartFileKind.Simai));
        }

        /// <summary>Checks the names covers are stored under and the extensions the game plays.</summary>
        [Test]
        public void Names_FollowTheGame()
        {
            Assert.That(ChartFileSniffer.GetCoverFileName(ChartFileKind.Jpeg), Is.EqualTo("bg.jpg"));
            Assert.That(ChartFileSniffer.GetCoverFileName(ChartFileKind.WebP), Is.EqualTo("bg.png"));
            Assert.That(ChartFileSniffer.GetPlayableAudioExtension(ChartFileKind.Ogg), Is.EqualTo(".ogg"));
            Assert.That(ChartFileSniffer.GetPlayableAudioExtension(ChartFileKind.M4a), Is.Null);
            Assert.That(ChartFileSniffer.GetPlayableAudioExtension(ChartFileKind.Flac), Is.Null);
            Assert.That(ChartFileSniffer.GetPlayableVideoExtension(ChartFileKind.Matroska), Is.EqualTo(".mkv"));
        }

        /// <summary>Sniffs UTF-8 text.</summary>
        /// <param name="text">The text.</param>
        /// <param name="name">The file name.</param>
        /// <returns>The kind.</returns>
        private static ChartFileKind Sniff(string text, string name)
        {
            return ChartFileSniffer.Sniff(Encoding.UTF8.GetBytes(text), name);
        }
    }
}
