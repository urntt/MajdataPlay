using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using MajdataPlay.Import;
using MajdataPlay.Tests.Game.Misc.Support;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

#nullable enable
namespace MajdataPlay.Tests.Game.Misc.Import
{
    /// <summary>
    /// Covers the bundled Universal Rhythm Chart converters run by <see cref="UrcConverter"/>: a small simai chart is converted
    /// to the files of a Phira .pez package and back. The exact output is checked against the URC command line when the bundle
    /// is rebuilt (Tools/Urc/README.md); these tests check that the bundle loads in the engine and keeps the notes.
    /// </summary>
    public class UrcConverterTests
    {
        /// <summary>The files next to the fixture chart, from which the music and the cover are chosen.</summary>
        private static readonly string[] ChartFiles = { "maidata.txt", "track.mp3", "bg.png" };

        /// <summary>Checks that the Master chart of the fixture becomes a .pez chart with its four notes and its metadata.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator SimaiToPez_ConvertsChartAndMetadata()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var result = await UrcConverter.SimaiToPezAsync(ChartFixture.CreateMaidata("Song"), 5, ChartFiles);
                await UniTask.SwitchToMainThread();

                Assert.That(result.Inote, Is.EqualTo(5));
                Assert.That(result.Music, Is.EqualTo("track.mp3"));
                Assert.That(result.Illustration, Is.EqualTo("bg.png"));
                Assert.That(result.InfoYaml, Does.Contain("name: \"Song\""));
                Assert.That(result.InfoYaml, Does.Contain("chart: \"chart.json\""));
                Assert.That(result.InfoYaml, Does.Contain("music: \"track.mp3\""));
                var chart = JObject.Parse(result.ChartJson);
                var lines = (JArray)chart["judgeLineList"]!;
                var noteCount = 0;
                foreach (var line in lines)
                {
                    noteCount += (line["notes"] as JArray)?.Count ?? 0;
                }
                Assert.That(noteCount, Is.EqualTo(4));
                Assert.That(result.Report, Is.Not.Empty);
            });
        }

        /// <summary>Checks that a converted chart converts back to maidata.txt with the same notes, title and media.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator PezToSimai_RoundTripsNotes()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var pez = await UrcConverter.SimaiToPezAsync(ChartFixture.CreateMaidata("Song"), 5, ChartFiles);

                var simai = await UrcConverter.PezToSimaiAsync(pez.ChartJson);
                await UniTask.SwitchToMainThread();

                Assert.That(simai.Title, Is.EqualTo("Song"));
                Assert.That(simai.Music, Is.EqualTo("track.mp3"));
                Assert.That(simai.Illustration, Is.EqualTo("bg.png"));
                Assert.That(simai.Maidata, Does.Contain("&inote_5="));
                Assert.That(simai.Maidata, Does.Contain("(120){4}1,2,3,4,"));
            });
        }

        /// <summary>Checks that a chart URC cannot read fails with the reason URC gave.</summary>
        /// <returns>The test coroutine.</returns>
        [UnityTest]
        public IEnumerator PezToSimai_InvalidChart_Throws()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var failure = default(Exception);

                try
                {
                    await UrcConverter.PezToSimaiAsync("{ \"not\": \"a chart\" }");
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                await UniTask.SwitchToMainThread();

                Assert.That(failure, Is.InstanceOf<UrcConversionException>());
                Assert.That(failure!.Message, Is.Not.Empty);
            });
        }
    }
}
