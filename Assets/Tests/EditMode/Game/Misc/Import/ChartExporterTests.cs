using System.Collections;
using System.Collections.Generic;
using System.IO.Compression;
using Cysharp.Threading.Tasks;
using MajdataPlay.Import;
using MajdataPlay.IO.Storage;
using MajdataPlay.Tests;
using MajdataPlay.Tests.Game.Misc.Support;
using NUnit.Framework;
using UnityEngine.TestTools;

#nullable enable
namespace MajdataPlay.Tests.Game.Misc.Import
{
    /// <summary>
    /// Covers the archive layouts written by <see cref="ChartExporter"/> for a local chart in temporary storage. Sharing the
    /// archive is not exercised.
    /// </summary>
    public class ChartExporterTests
    {
        /// <summary>The temporary storage for the test.</summary>
        private TemporaryDirectory _temporary = null!;

        /// <summary>The scope restoring the application directories.</summary>
        private MajEnvScope _environment = null!;

        /// <summary>Points the application directories at a temporary layout.</summary>
        [SetUp]
        public void SetUp()
        {
            _temporary = new TemporaryDirectory();
            _environment = new MajEnvScope();
            _environment.UseTemporaryLayout(_temporary);
        }

        /// <summary>Restores the directories.</summary>
        [TearDown]
        public void TearDown()
        {
            _environment.Dispose();
            _temporary.Dispose();
        }

        /// <summary>
        /// Creates a chart with a cover, a video, an extra file in a subfolder and an identity marker folder.
        /// </summary>
        /// <returns>The chart folder.</returns>
        private StorageDirectory CreateChart()
        {
            var directory = ChartFixture.CreateChart(_temporary.Directory, "Chart", "Song Title", coverName: "bg.png", videoName: "pv.mp4");
            directory.CreateFile("notes.txt").WriteAllText("notes");
            directory.CreateDirectory("extra").CreateFile("readme.txt").WriteAllText("readme");
            directory.CreateDirectory(".MajdataPlay").CreateFile("id").WriteAllText("identity");
            return directory;
        }

        /// <summary>Reads the entry names of an archive.</summary>
        /// <param name="path">The local path of the archive.</param>
        /// <returns>The entry names in archive order.</returns>
        private static List<string> ReadEntryNames(string path)
        {
            var names = new List<string>();
            using var stream = FileSystem.File.OpenRead(path);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                names.Add(entry.FullName);
            }
            return names;
        }

        /// <summary>Checks that a ZIP export holds the whole chart folder under its title, without the identity marker.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator ExportAsync_Zip_HoldsWholeFolder()
        {
            return UniTask.ToCoroutine(async () =>
            {
                using var song = await SongDetail.ParseAsync(CreateChart());

                var path = await ChartExporter.ExportAsync(song, ChartExportFormat.Zip, ChartLevel.Master);
                await UniTask.SwitchToMainThread();

                Assert.That(path, Does.EndWith("Song Title.zip"));
                Assert.That(ReadEntryNames(path), Is.EquivalentTo(new[]
                {
                    "Song Title/maidata.txt",
                    "Song Title/track.mp3",
                    "Song Title/bg.png",
                    "Song Title/pv.mp4",
                    "Song Title/notes.txt",
                    "Song Title/extra/readme.txt",
                }));
            });
        }

        /// <summary>Checks that an ADX export holds only maidata.txt, the track, the cover and the video.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator ExportAsync_Adx_HoldsChartAndMedia()
        {
            return UniTask.ToCoroutine(async () =>
            {
                using var song = await SongDetail.ParseAsync(CreateChart());

                var path = await ChartExporter.ExportAsync(song, ChartExportFormat.Adx, ChartLevel.Master);
                await UniTask.SwitchToMainThread();

                Assert.That(path, Does.EndWith("Song Title.adx"));
                Assert.That(ReadEntryNames(path), Is.EquivalentTo(new[]
                {
                    "Song Title/maidata.txt",
                    "Song Title/track.mp3",
                    "Song Title/bg.png",
                    "Song Title/pv.mp4",
                }));
            });
        }

        /// <summary>Checks that a PEZ export holds the converted chart, its info and the media it names.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator ExportAsync_Pez_HoldsConvertedChart()
        {
            return UniTask.ToCoroutine(async () =>
            {
                using var song = await SongDetail.ParseAsync(CreateChart());

                var path = await ChartExporter.ExportAsync(song, ChartExportFormat.Pez, ChartLevel.Master);
                await UniTask.SwitchToMainThread();

                Assert.That(path, Does.EndWith("Song Title.pez"));
                Assert.That(ReadEntryNames(path), Is.EquivalentTo(new[] { "chart.json", "info.yml", "track.mp3", "bg.png" }));
            });
        }

        /// <summary>Checks that each export empties the export folder first, so old archives do not pile up.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator ExportAsync_RemovesEarlierExports()
        {
            return UniTask.ToCoroutine(async () =>
            {
                using var song = await SongDetail.ParseAsync(CreateChart());

                await ChartExporter.ExportAsync(song, ChartExportFormat.Zip, ChartLevel.Master);
                var path = await ChartExporter.ExportAsync(song, ChartExportFormat.Adx, ChartLevel.Master);
                await UniTask.SwitchToMainThread();

                var folder = MajEnv.TempDirectory.FindDirectory("ChartExports")!;
                var names = new List<string>();
                foreach (var entry in folder.EnumerateEntries())
                {
                    names.Add(entry.Name);
                }
                Assert.That(names, Is.EqualTo(new[] { "Song Title.adx" }));
                Assert.That(FileSystem.GetEntry(path), Is.Not.Null);
            });
        }
    }
}
