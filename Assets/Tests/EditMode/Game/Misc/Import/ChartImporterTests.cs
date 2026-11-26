using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MajdataPlay.Import;
using MajdataPlay.IO.Storage;
using MajdataPlay.Tests;
using MajdataPlay.Tests.Game.Misc.Support;
using NUnit.Framework;
using TMPro;
using UnityEngine.TestTools;

#nullable enable
namespace MajdataPlay.Tests.Game.Misc.Import
{
    /// <summary>
    /// Covers how <see cref="ChartImporter"/> finds charts by content in archives and folders, renames their media to what the
    /// game reads and explains what it cannot import, with temporary chart roots. The scene switching driver of the importer
    /// is not exercised; each source is imported through the private <c>ImportSourceAsync</c> step.
    /// </summary>
    public class ChartImporterTests
    {
        /// <summary>The temporary storage for the test.</summary>
        private TemporaryDirectory _temporary = null!;

        /// <summary>The scope restoring the application directories.</summary>
        private MajEnvScope _environment = null!;

        /// <summary>The objects created by the test.</summary>
        private TestObjects _objects = null!;

        /// <summary>The scene switcher instance before the test.</summary>
        private SceneSwitcher? _savedSwitcher;

        /// <summary>The import root before the test.</summary>
        private StorageDirectory? _savedImportRoot;

        /// <summary>The report before the test.</summary>
        private ChartImportReport? _savedReport;

        /// <summary>The loading text shown by the fake scene switcher.</summary>
        private TextMeshProUGUI _loadingText = null!;

        /// <summary>The chart root of the temporary layout.</summary>
        private StorageDirectory _chartRoot = null!;

        /// <summary>The import collection of the temporary layout.</summary>
        private StorageDirectory _importRoot = null!;

        /// <summary>Gets the report the importer is writing.</summary>
        private static ChartImportReport Report => PrivateMember.GetStaticField<ChartImportReport>(typeof(ChartImporter), "s_report");

        /// <summary>Creates a temporary layout, a fresh report and a scene switcher that only records loading text.</summary>
        [SetUp]
        public void SetUp()
        {
            _temporary = new TemporaryDirectory();
            _environment = new MajEnvScope();
            _environment.UseTemporaryLayout(_temporary);
            _chartRoot = MajEnv.ChartDirectory;
            _importRoot = _chartRoot.CreateDirectory("Import");
            _objects = new TestObjects();
            _savedSwitcher = Majdata<SceneSwitcher>.Instance;
            _savedImportRoot = PrivateMember.GetStaticField<StorageDirectory?>(typeof(ChartImporter), "s_importRoot");
            _savedReport = PrivateMember.GetStaticField<ChartImportReport?>(typeof(ChartImporter), "s_report");
            var switcher = _objects.Add<SceneSwitcher>();
            _loadingText = _objects.Add<TextMeshProUGUI>("Loading");
            PrivateMember.SetField(switcher, "_loadingText", _loadingText);
            Majdata<SceneSwitcher>.Free();
            Majdata<SceneSwitcher>.Instance = switcher;
            PrivateMember.SetStaticField(typeof(ChartImporter), "s_importRoot", _importRoot);
            PrivateMember.SetStaticField(typeof(ChartImporter), "s_report", new ChartImportReport());
            DrainQueue();
        }

        /// <summary>Restores the importer, scene switcher and directories.</summary>
        [TearDown]
        public void TearDown()
        {
            DrainQueue();
            PrivateMember.SetStaticField(typeof(ChartImporter), "s_importRoot", _savedImportRoot);
            PrivateMember.SetStaticField(typeof(ChartImporter), "s_report", _savedReport);
            Majdata<SceneSwitcher>.Free();
            if (_savedSwitcher is not null)
            {
                Majdata<SceneSwitcher>.Instance = _savedSwitcher;
            }
            _objects.Dispose();
            _environment.Dispose();
            _temporary.Dispose();
        }

        /// <summary>Gets the number of queued imports.</summary>
        /// <returns>The queue length.</returns>
        private static int QueueCount()
        {
            var queue = PrivateMember.GetStaticField<ICollection>(typeof(ChartImporter), "s_pendingImportTasks");
            return queue.Count;
        }

        /// <summary>Removes every queued import.</summary>
        private static void DrainQueue()
        {
            var queue = PrivateMember.GetStaticField<object>(typeof(ChartImporter), "s_pendingImportTasks");
            var tryDequeue = queue.GetType().GetMethod("TryDequeue")!;
            var arguments = new object?[] { null };
            while ((bool)tryDequeue.Invoke(queue, arguments)!)
            {
            }
        }

        /// <summary>Imports one source through the importer's private step and waits for it.</summary>
        /// <param name="location">The file or folder to import.</param>
        /// <param name="deleteSource">Whether the source is a consumed temporary copy.</param>
        /// <returns>The completed step.</returns>
        private static async UniTask ImportAsync(string location, bool deleteSource = false)
        {
            var entry = FileSystem.GetEntry(location) ?? new FileSystemEntry(location, Path.GetFileName(location), false);
            var step = (ValueTask)PrivateMember.InvokeStatic(typeof(ChartImporter), "ImportSourceAsync", entry, deleteSource)!;
            await step;
        }

        /// <summary>Creates a ZIP archive file from name/content pairs.</summary>
        /// <param name="name">The archive file name; its extension does not matter to the importer.</param>
        /// <param name="entries">The entry names and their contents (text or bytes); null content creates a directory entry.</param>
        /// <returns>The archive file.</returns>
        private StorageFile CreateArchive(string name, params (string Name, object? Content)[] entries)
        {
            return CreateArchive(name, null, entries);
        }

        /// <summary>Creates a ZIP archive file whose entry names are stored in a given encoding.</summary>
        /// <param name="name">The archive file name.</param>
        /// <param name="nameEncoding">The encoding of the entry names, or null for the library's default (UTF-8).</param>
        /// <param name="entries">The entry names and their contents (text or bytes); null content creates a directory entry.</param>
        /// <returns>The archive file.</returns>
        private StorageFile CreateArchive(string name, Encoding? nameEncoding, params (string Name, object? Content)[] entries)
        {
            var file = _temporary.Directory.CreateFile(name);
            file.WriteAllBytes(CreateArchiveBytes(nameEncoding, entries));
            return file;
        }

        /// <summary>Creates the bytes of a ZIP archive.</summary>
        /// <param name="nameEncoding">The encoding of the entry names, or null for the library's default (UTF-8).</param>
        /// <param name="entries">The entry names and their contents (text or bytes); null content creates a directory entry.</param>
        /// <returns>The archive bytes.</returns>
        private static byte[] CreateArchiveBytes(Encoding? nameEncoding, params (string Name, object? Content)[] entries)
        {
            using var memory = new MemoryStream();
            using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true, nameEncoding))
            {
                foreach (var (entryName, content) in entries)
                {
                    var entry = archive.CreateEntry(entryName);
                    if (content is null)
                    {
                        continue;
                    }
                    var bytes = content as byte[] ?? Encoding.UTF8.GetBytes((string)content);
                    using var stream = entry.Open();
                    stream.Write(bytes, 0, bytes.Length);
                }
            }
            return memory.ToArray();
        }

        /// <summary>Gets the GBK encoding through the importer, which registers the code page provider.</summary>
        /// <returns>GBK, or null when this runtime has no code page 936.</returns>
        private static Encoding? GetGbk()
        {
            return PrivateMember.InvokeStatic<Encoding?>(typeof(ChartArchiveReader), "TryGetStrictEncoding", 936);
        }

        /// <summary>Checks that an archive containing one chart at its root is imported into the Import collection.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator ImportArchive_RootChart()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var archive = CreateArchive("Song.zip", ("maidata.txt", ChartFixture.CreateMaidata("Song")), ("track.mp3", MediaFixture.Mp3), ("bg.png", MediaFixture.Png));

                await ImportAsync(archive.Location);

                var chart = _importRoot.FindDirectory("Song");
                Assert.That(chart, Is.Not.Null);
                Assert.That(chart!.FindFile("maidata.txt"), Is.Not.Null);
                Assert.That(chart.FindFile("track.mp3")!.ReadAllBytes(), Is.EqualTo(MediaFixture.Mp3));
                Assert.That(chart.FindFile("bg.png"), Is.Not.Null);
                Assert.That(MajEnv.TempDirectory.EnumerateEntries(), Is.Empty);
                Assert.That(_temporary.Directory.FindFile("Song.zip"), Is.Not.Null);
                Assert.That(Report.Charts.Count, Is.EqualTo(1));
                Assert.That(Report.Failures, Is.Empty);
            });
        }

        /// <summary>Checks that chart folders directly inside an archive go to the Import collection and grouped charts to a named collection.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator ImportArchive_ChartAndCollectionLayouts()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var archive = CreateArchive("Pack.adx",
                    ("Single/", null),
                    ("Single/maidata.txt", ChartFixture.CreateMaidata("Single")),
                    ("Single/track.ogg", MediaFixture.Ogg),
                    ("Pack/C1/maidata.txt", ChartFixture.CreateMaidata("C1")),
                    ("Pack/C1/track.mp3", MediaFixture.Mp3),
                    ("Pack/C2/maidata.txt", ChartFixture.CreateMaidata("C2")),
                    ("Pack/C2/TRACK.WAV", "c"),
                    ("Pack/NotAChart/readme.txt", "d"),
                    ("Empty/readme.txt", "e"),
                    (".hidden/maidata.txt", ChartFixture.CreateMaidata("Hidden")),
                    (".hidden/track.mp3", MediaFixture.Mp3));

                await ImportAsync(archive.Location);

                Assert.That(_importRoot.FindDirectory("Single"), Is.Not.Null);
                var pack = _chartRoot.FindDirectory("Pack");
                Assert.That(pack, Is.Not.Null);
                Assert.That(pack!.FindDirectory("C1"), Is.Not.Null);
                Assert.That(pack.FindDirectory("C2")!.FindFile("TRACK.WAV"), Is.Not.Null);
                Assert.That(pack.FindDirectory("NotAChart"), Is.Null);
                Assert.That(_chartRoot.FindDirectory("Empty"), Is.Null);
                Assert.That(_importRoot.FindDirectory(".hidden"), Is.Null);
            });
        }

        /// <summary>Checks that an archive is recognised by content whatever its extension.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator ImportArchive_WrongExtension_IsReadByContent()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var archive = CreateArchive("Song.dat", ("Song/maidata.txt", ChartFixture.CreateMaidata("Song")), ("Song/track.mp3", MediaFixture.Mp3));

                await ImportAsync(archive.Location);

                Assert.That(_importRoot.FindDirectory("Song"), Is.Not.Null);
            });
        }

        /// <summary>Checks that charts are found at any depth, a folder that only wraps another is ignored, and macOS metadata is skipped.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator ImportArchive_DeepLayoutAndMetadata()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var archive = CreateArchive("Deep.zip",
                    ("Wrapper/Pack/Alpha/maidata.txt", ChartFixture.CreateMaidata("Alpha")),
                    ("Wrapper/Pack/Alpha/track.mp3", MediaFixture.Mp3),
                    ("Wrapper/Pack/Beta/maidata.txt", ChartFixture.CreateMaidata("Beta")),
                    ("Wrapper/Pack/Beta/track.ogg", MediaFixture.Ogg),
                    ("Solo/Solo/maidata.txt", ChartFixture.CreateMaidata("Solo")),
                    ("Solo/Solo/track.mp3", MediaFixture.Mp3),
                    ("__MACOSX/Wrapper/Pack/._Alpha", "x"),
                    ("Wrapper/.DS_Store", "x"));

                await ImportAsync(archive.Location);

                var pack = _chartRoot.FindDirectory("Pack");
                Assert.That(pack!.FindDirectory("Alpha"), Is.Not.Null);
                Assert.That(pack.FindDirectory("Beta"), Is.Not.Null);
                Assert.That(_importRoot.FindDirectory("Solo"), Is.Not.Null);
                Assert.That(_chartRoot.FindDirectory("__MACOSX"), Is.Null);
                Assert.That(_chartRoot.FindDirectory("Wrapper"), Is.Null);
            });
        }

        /// <summary>Checks that the chart text and media are stored under the names the game reads, chosen by content.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator ImportArchive_RenamesMediaToGameNames()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var archive = CreateArchive("Names.zip",
                    ("Alpha/maidata.txt.txt", ChartFixture.CreateMaidata("Alpha")),
                    ("Alpha/BGM.MP3", MediaFixture.Mp3),
                    ("Alpha/Jacket.PNG", MediaFixture.Png),
                    ("Alpha/movie.mp4", MediaFixture.Mp4),
                    ("Beta/maidata.txt", ChartFixture.CreateMaidata("Beta")),
                    ("Beta/song", MediaFixture.Ogg),
                    ("Beta/cover.jpg", MediaFixture.Jpeg),
                    ("Gamma/maidata.txt", ChartFixture.CreateMaidata("Gamma")),
                    ("Gamma/track.mp3", MediaFixture.Mp3),
                    ("Gamma/art.webp", MediaFixture.WebP));

                await ImportAsync(archive.Location);

                var alpha = _importRoot.FindDirectory("Alpha")!;
                Assert.That(alpha.FindFile("maidata.txt"), Is.Not.Null);
                Assert.That(alpha.FindFile("track.mp3")!.ReadAllBytes(), Is.EqualTo(MediaFixture.Mp3));
                Assert.That(alpha.FindFile("bg.png"), Is.Not.Null);
                Assert.That(alpha.FindFile("pv.mp4"), Is.Not.Null);
                Assert.That(alpha.FindFile("Jacket.PNG"), Is.Null);
                var beta = _importRoot.FindDirectory("Beta")!;
                Assert.That(beta.FindFile("track.ogg"), Is.Not.Null);
                Assert.That(beta.FindFile("bg.jpg"), Is.Not.Null);
                Assert.That(_importRoot.FindDirectory("Gamma")!.FindFile("bg.png")!.ReadAllBytes(), Is.EqualTo(MediaFixture.WebP));
                Assert.That(Report.HasWarning(ChartImportWarning.Renamed), Is.True);
            });
        }

        /// <summary>Checks that archives inside an archive, such as a pack of .adx files, are imported too.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator ImportArchive_ArchivesInsideArchive()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var one = CreateArchiveBytes(null, ("One/maidata.txt", ChartFixture.CreateMaidata("One")), ("One/track.mp3", MediaFixture.Mp3));
                var two = CreateArchiveBytes(null, ("Two/maidata.txt", ChartFixture.CreateMaidata("Two")), ("Two/track.ogg", MediaFixture.Ogg));
                var pack = CreateArchive("Pack.zip", ("one.adx", one), ("two.ADX", two));

                await ImportAsync(pack.Location);

                Assert.That(_importRoot.FindDirectory("One"), Is.Not.Null);
                Assert.That(_importRoot.FindDirectory("Two"), Is.Not.Null);
                Assert.That(MajEnv.TempDirectory.EnumerateEntries(), Is.Empty);
            });
        }

        /// <summary>Checks that entry names stored as GBK without the UTF-8 flag are decoded and kept apart.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator ImportArchive_GbkNames()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var gbk = GetGbk();
                if (gbk is null)
                {
                    Assert.Ignore("Code page 936 is not available in this runtime.");
                }
                var archive = CreateArchive("Gbk.zip", gbk,
                    ("中文合集/春日影/maidata.txt", ChartFixture.CreateMaidata("Spring")), ("中文合集/春日影/track.mp3", MediaFixture.Mp3),
                    ("中文合集/夏日影/maidata.txt", ChartFixture.CreateMaidata("Summer")), ("中文合集/夏日影/track.ogg", MediaFixture.Ogg));

                await ImportAsync(archive.Location);

                var collection = _chartRoot.FindDirectory("中文合集");
                Assert.That(collection, Is.Not.Null);
                Assert.That(collection!.FindDirectory("春日影")!.FindFile("track.mp3"), Is.Not.Null);
                Assert.That(collection.FindDirectory("夏日影")!.FindFile("track.ogg"), Is.Not.Null);
                Assert.That(Report.HasWarning(ChartImportWarning.LegacyNames), Is.True);
            });
        }

        /// <summary>Checks that chart text saved as GBK is stored as UTF-8.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator ImportDirectory_GbkChartText_BecomesUtf8()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var gbk = GetGbk();
                if (gbk is null)
                {
                    Assert.Ignore("Code page 936 is not available in this runtime.");
                }
                var source = _temporary.Directory.CreateDirectory("Gbk");
                source.CreateFile("maidata.txt").WriteAllBytes(gbk!.GetBytes(ChartFixture.CreateMaidata("春日影")));
                source.CreateFile("track.mp3").WriteAllBytes(MediaFixture.Mp3);

                await ImportAsync(source.Location);

                var text = _importRoot.FindDirectory("Gbk")!.FindFile("maidata.txt")!.ReadAllText(new UTF8Encoding(false));
                Assert.That(text, Does.Contain("&title=春日影"));
                Assert.That(Report.HasWarning(ChartImportWarning.ConvertedText), Is.True);
            });
        }

        /// <summary>Checks that an archive entry escaping the extraction directory is refused with its reason and leaves no files behind.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator ImportArchive_RejectsTraversal()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var archive = CreateArchive("Evil.zip", ("maidata.txt", ChartFixture.CreateMaidata("Evil")), ("../escaped.txt", "x"));

                await ImportAsync(archive.Location);

                Assert.That(Report.HasFailure(ChartImportFailure.UnsafeArchive), Is.True);
                Assert.That(MajEnv.TempDirectory.EnumerateEntries(), Is.Empty);
                Assert.That(MajEnv.CacheDirectory.FindFile("escaped.txt"), Is.Null);
                Assert.That(_importRoot.EnumerateEntries(), Is.Empty);
            });
        }

        /// <summary>Checks that what cannot be imported is reported with its reason.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator Import_ReportsWhyNothingWasImported()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var rar = _temporary.Directory.CreateFile("pack.rar");
                rar.WriteAllBytes(MediaFixture.Rar);
                var lone = _temporary.WriteText("maidata.txt", ChartFixture.CreateMaidata("Lone"));
                var picture = _temporary.Directory.CreateFile("picture.jpg");
                picture.WriteAllBytes(MediaFixture.Jpeg);
                var readme = CreateArchive("readme.zip", ("readme.txt", "hello"));
                var m4a = CreateArchive("m4a.zip", ("M4a/maidata.txt", ChartFixture.CreateMaidata("M4a")), ("M4a/track.m4a", MediaFixture.M4a));
                var silent = CreateArchive("silent.zip", ("Silent/maidata.txt", ChartFixture.CreateMaidata("Silent")));
                var phigros = CreateArchive("phigros.pez", ("chart.json", "{\"formatVersion\":3,\"judgeLineList\":[]}"), ("info.yml", "chart: chart.json\n"));

                foreach (var source in new StorageFile[] { rar, lone, picture, readme, m4a, silent, phigros })
                {
                    await ImportAsync(source.Location);
                }
                await ImportAsync(Path.Combine(_temporary.Location, "missing.zip"));

                Assert.That(Report.HasFailure(ChartImportFailure.UnsupportedArchive), Is.True);
                Assert.That(Report.HasFailure(ChartImportFailure.LoneChartFile), Is.True);
                Assert.That(Report.HasFailure(ChartImportFailure.MediaFile), Is.True);
                Assert.That(Report.HasFailure(ChartImportFailure.NoChart), Is.True);
                Assert.That(Report.HasFailure(ChartImportFailure.UnsupportedMusic), Is.True);
                Assert.That(Report.HasFailure(ChartImportFailure.MissingMusic), Is.True);
                Assert.That(Report.HasFailure(ChartImportFailure.PhigrosChart), Is.True);
                Assert.That(Report.HasFailure(ChartImportFailure.NotFound), Is.True);
                Assert.That(Report.Charts, Is.Empty);
                Assert.That(_importRoot.EnumerateEntries(), Is.Empty);
            });
        }

        /// <summary>Checks that a .pez holding a PhiEdit (PEC) chart under a .json name is converted and imported.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator ImportArchive_PecChartInPez()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var archive = CreateArchive("Pec.pez",
                    ("chart.json", MediaFixture.PecChart),
                    ("info.yml", "name: \"PEC Song\"\nlevel: \"IN Lv.13\"\nchart: \"chart.json\"\nmusic: \"song.ogg\"\nillustration: \"art.jpg\"\n"),
                    ("song.ogg", MediaFixture.Ogg),
                    ("art.jpg", MediaFixture.Jpeg));

                await ImportAsync(archive.Location);

                var chart = _importRoot.FindDirectory("PEC Song");
                Assert.That(chart, Is.Not.Null, string.Join("\n", Report.Failures));
                Assert.That(chart!.FindFile("maidata.txt")!.ReadAllText(), Does.Contain("&inote_"));
                Assert.That(chart.FindFile("track.ogg"), Is.Not.Null);
                Assert.That(chart.FindFile("bg.jpg"), Is.Not.Null);
            });
        }

        /// <summary>
        /// Checks that a .pez exported by <see cref="ChartExporter"/> imports back as a simai chart of the Import collection, named
        /// after its title, with the track and the cover renamed.
        /// </summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator ImportArchive_PezRoundTrip()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var source = ChartFixture.CreateChart(_temporary.Directory, "Source", "Pez Song");
                source.FindFile("track.mp3")!.WriteAllBytes(MediaFixture.Mp3);
                source.CreateFile("bg.png").WriteAllBytes(MediaFixture.Png);
                string path;
                using (var song = await SongDetail.ParseAsync(source))
                {
                    path = await ChartExporter.ExportAsync(song, ChartExportFormat.Pez, ChartLevel.Master);
                }
                await UniTask.SwitchToMainThread();

                await ImportAsync(path);

                var chart = _importRoot.FindDirectory("Pez Song");
                Assert.That(chart, Is.Not.Null);
                Assert.That(chart!.FindFile("maidata.txt")!.ReadAllText(), Does.Contain("&inote_5=").And.Contain("1,2,3,4,"));
                Assert.That(chart.FindFile("track.mp3")!.ReadAllBytes(), Is.EqualTo(MediaFixture.Mp3));
                Assert.That(chart.FindFile("bg.png")!.ReadAllBytes(), Is.EqualTo(MediaFixture.Png));
                Assert.That(FileSystem.GetEntry(path), Is.Not.Null);
            });
        }

        /// <summary>Checks that importing the same chart twice keeps both copies under distinct names.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator ImportDirectory_ChartDirectoryIsCopiedUniquely()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var source = ChartFixture.CreateChart(_temporary.Directory, "Source", "Source");

                await ImportAsync(source.Location);
                await ImportAsync(source.Location);
                await ImportAsync(source.Location);

                Assert.That(_importRoot.FindDirectory("Source"), Is.Not.Null);
                Assert.That(_importRoot.FindDirectory("Source (1)"), Is.Not.Null);
                Assert.That(_importRoot.FindDirectory("Source (2)")!.FindFile("track.mp3"), Is.Not.Null);
                Assert.That(source.Exists, Is.True);
            });
        }

        /// <summary>Checks that a queued folder holding charts and collections follows the archive layout rules.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator ImportDirectory_ContainerLayout()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var container = _temporary.Directory.CreateDirectory("Container");
                ChartFixture.CreateChart(container, "Loose", "Loose");
                var collection = container.CreateDirectory("Collection");
                ChartFixture.CreateChart(collection, "Inner", "Inner");
                ChartFixture.CreateChart(collection, "NoTrack", "NoTrack", trackName: null);
                ChartFixture.CreateChart(container, ".dot", "Dot");
                _chartRoot.CreateDirectory("Collection").CreateDirectory("Existing");

                await ImportAsync(container.Location);

                Assert.That(_importRoot.FindDirectory("Loose"), Is.Not.Null);
                var target = _chartRoot.FindDirectory("Collection")!;
                Assert.That(target.FindDirectory("Inner"), Is.Not.Null);
                Assert.That(target.FindDirectory("Existing"), Is.Not.Null);
                Assert.That(target.FindDirectory("NoTrack"), Is.Null);
                Assert.That(_importRoot.FindDirectory(".dot"), Is.Null);
                Assert.That(Report.HasFailure(ChartImportFailure.MissingMusic), Is.True);
            });
        }

        /// <summary>Checks that a consumed temporary copy is deleted after the import and a picked source is kept.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator ImportSource_DeletesOnlyConsumedCopies()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var kept = CreateArchive("Kept.zip", ("maidata.txt", ChartFixture.CreateMaidata("Kept")), ("track.mp3", MediaFixture.Mp3));
                var consumed = ChartFixture.CreateChart(_temporary.Directory, "Consumed", "Consumed");

                await ImportAsync(kept.Location);
                await ImportAsync(consumed.Location, deleteSource: true);

                Assert.That(kept.Exists, Is.True);
                Assert.That(consumed.Exists, Is.False);
                Assert.That(_importRoot.FindDirectory("Consumed"), Is.Not.Null);
            });
        }

        /// <summary>Checks which folders are chart folders.</summary>
        [Test]
        public void GetChartFormat_FindsSimaiAndPhiraFolders()
        {
            var simai = ChartFixture.CreateChart(_temporary.Directory, "Simai", "Simai", trackName: null);
            var renamed = _temporary.Directory.CreateDirectory("Renamed");
            renamed.CreateFile("chart (1).txt").WriteAllText(ChartFixture.CreateMaidata("Renamed"));
            var phira = _temporary.Directory.CreateDirectory("Phira");
            phira.CreateFile("info.yml").WriteAllText("chart: chart.json\n");
            var none = ChartFixture.CreateChart(_temporary.Directory, "None", "None", withMaidata: false);
            none.CreateDirectory("maidata.txt");

            Assert.That(FormatOf(simai), Is.EqualTo(FoundChartFormat.Simai));
            Assert.That(FormatOf(renamed), Is.EqualTo(FoundChartFormat.Simai));
            Assert.That(FormatOf(phira), Is.EqualTo(FoundChartFormat.Phira));
            Assert.That(FormatOf(none), Is.Null);
        }

        /// <summary>Gets the chart format of a folder.</summary>
        /// <param name="directory">The folder.</param>
        /// <returns>The chart format, or null when the folder is not a chart.</returns>
        private static FoundChartFormat? FormatOf(StorageDirectory directory)
        {
            ChartPackageScanner.ReadFolder(directory, out var files, out _);
            return ChartPackageScanner.GetChartFormat(files);
        }

        /// <summary>Checks that hidden, dot-prefixed and macOS metadata entries are excluded.</summary>
        [Test]
        public void IsExcluded_SkipsHiddenEntries()
        {
            Assert.That(ChartPackageScanner.IsExcluded(new FileSystemEntry("C:/x/Visible", "Visible", true)), Is.False);
            Assert.That(ChartPackageScanner.IsExcluded(new FileSystemEntry("C:/x/.git", ".git", true)), Is.True);
            Assert.That(ChartPackageScanner.IsExcluded(new FileSystemEntry("C:/x/__MACOSX", "__MACOSX", true)), Is.True);
            Assert.That(ChartPackageScanner.IsExcluded(new FileSystemEntry("C:/x/Hidden", "Hidden", true, isHidden: true)), Is.True);
            Assert.That(ChartPackageScanner.IsExcluded(new FileSystemEntry("C:/x/System", "System", true, isSystem: true)), Is.True);
        }

        /// <summary>Checks that existing paths are queued and missing paths are ignored.</summary>
        [Test]
        public void AddImportTask_QueuesExistingEntries()
        {
            var file = _temporary.WriteText("queued.zip", "x");

            ChartImporter.AddImportTask(file.Location);
            ChartImporter.AddImportTask(Path.Combine(_temporary.Location, "missing.zip"));

            Assert.That(QueueCount(), Is.EqualTo(1));
        }

        /// <summary>Checks that only copies in the iOS inbox are treated as consumed.</summary>
        [Test]
        public void IsInboxCopy_MatchesOnlyTheInbox()
        {
            Assert.That(PrivateMember.InvokeStatic<bool>(typeof(ChartImporter), "IsInboxCopy", "/var/tmp/maicharts_inbox/Song.zip"), Is.True);
            Assert.That(PrivateMember.InvokeStatic<bool>(typeof(ChartImporter), "IsInboxCopy", "/Documents/Charts/Song.zip"), Is.False);
        }

        /// <summary>Checks that the update hook does not start an import while the empty scene is active or nothing is queued.</summary>
        [Test]
        public void OnUpdate_WaitsForQueueAndScene()
        {
            var savedScene = SceneSwitcher.CurrentScene;
            try
            {
                ChartImporter.OnUpdate();
                Assert.That(PrivateMember.GetStaticField<ValueTask>(typeof(ChartImporter), "s_importTask").IsCompleted, Is.True);

                PrivateMember.SetStaticProperty(typeof(SceneSwitcher), nameof(SceneSwitcher.CurrentScene), MajScenes.Empty);
                ChartImporter.AddImportTask(_temporary.WriteText("queued.zip", "x").Location);
                ChartImporter.OnUpdate();

                Assert.That(QueueCount(), Is.EqualTo(1));
                Assert.That(PrivateMember.GetStaticField<ValueTask>(typeof(ChartImporter), "s_importTask").IsCompleted, Is.True);
            }
            finally
            {
                PrivateMember.SetStaticProperty(typeof(SceneSwitcher), nameof(SceneSwitcher.CurrentScene), savedScene);
            }
        }

        /// <summary>Checks that startup completes immediately when nothing is queued.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator OnStartAsync_WithoutQueue_Completes()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var task = ChartImporter.OnStartAsync();

                Assert.That(task.IsCompleted, Is.True);
                await task;
            });
        }

        /// <summary>Checks that cleanup helpers ignore missing targets.</summary>
        [Test]
        public void CleanupHelpers_IgnoreMissingTargets()
        {
            var missingDirectory = new StorageDirectory(_temporary.Directory.FileSystem, Path.Combine(_temporary.Location, "missing"));
            var existing = _temporary.Directory.CreateDirectory("existing");
            existing.CreateFile("file.txt");
            var file = _temporary.WriteText("delete.txt", "x");
            var missingFile = new FileSystemEntry(Path.Combine(_temporary.Location, "missing.txt"), "missing.txt", false);

            Assert.DoesNotThrow(() => PrivateMember.InvokeStatic(typeof(ChartImporter), "TryDeleteDirectory", missingDirectory));
            PrivateMember.InvokeStatic(typeof(ChartImporter), "TryDeleteDirectory", existing);
            Assert.DoesNotThrow(() => PrivateMember.InvokeStatic(typeof(ChartImporter), "TryDeleteSource", missingFile));
            PrivateMember.InvokeStatic(typeof(ChartImporter), "TryDeleteSource", FileSystem.GetEntry(file.Location)!);

            Assert.That(existing.Exists, Is.False);
            Assert.That(file.Exists, Is.False);
        }
    }
}
