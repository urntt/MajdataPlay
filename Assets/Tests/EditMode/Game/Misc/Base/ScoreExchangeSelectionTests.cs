using System;
using System.Collections.Generic;
using MajdataPlay.Settings;
using MajdataPlay.Tests;
using MajdataPlay.Tests.Game.Misc.Support;
using NUnit.Framework;

#nullable enable
namespace MajdataPlay.Tests.Game.Misc.Base
{
    /// <summary>
    /// Covers picking the played difficulties of a song list for sharing (<see cref="ScoreExchange.SelectPlayed"/>,
    /// <see cref="ScoreExchange.DeselectPlayed"/> and <see cref="ScoreExchange.GetPlayedSelectionState"/>), with scores put
    /// straight into the in-memory score store.
    /// </summary>
    public class ScoreExchangeSelectionTests
    {
        /// <summary>The temporary storage for the test.</summary>
        private TemporaryDirectory _temporary = null!;

        /// <summary>The scope restoring the application directories and settings.</summary>
        private MajEnvScope _environment = null!;

        /// <summary>The score buckets present before the test.</summary>
        private KeyValuePair<string, MaiScore>[] _savedBuckets = Array.Empty<KeyValuePair<string, MaiScore>>();

        /// <summary>Gets the private local score buckets.</summary>
        private static Dictionary<string, MaiScore> Buckets => PrivateMember.GetStaticField<Dictionary<string, MaiScore>>(typeof(ScoreManager), "s_buckets");

        /// <summary>Uses fresh settings with an empty selection and an empty score store that shares standard scores.</summary>
        [SetUp]
        public void SetUp()
        {
            _temporary = new TemporaryDirectory();
            _environment = new MajEnvScope();
            _environment.UseTemporaryLayout(_temporary);
            MajEnv.SystemSettings.Score.ScoreStore = ScoreStoreOption.Standard;
            _savedBuckets = new List<KeyValuePair<string, MaiScore>>(Buckets).ToArray();
            Buckets.Clear();
        }

        /// <summary>Restores the score store, the settings and the directories.</summary>
        [TearDown]
        public void TearDown()
        {
            Buckets.Clear();
            foreach (var pair in _savedBuckets)
            {
                Buckets[pair.Key] = pair.Value;
            }
            _environment.Dispose();
            _temporary.Dispose();
        }

        /// <summary>Records a play of one difficulty in one partition.</summary>
        /// <param name="song">The song.</param>
        /// <param name="level">The difficulty.</param>
        /// <param name="isFalling">True for the falling mode partition; false for the standard one.</param>
        private static void Play(FakeSongDetail song, ChartLevel level, bool isFalling = false)
        {
            var levelHash = song.LevelHashValues[(int)level]!;
            Buckets[ScoreManager.GetStoreHash(levelHash, isFalling)] = new MaiScore { PlayCount = 1 };
        }

        /// <summary>Checks that only played difficulties are picked, in song order and from the lowest difficulty up.</summary>
        [Test]
        public void SelectPlayed_PicksPlayedDifficultiesInOrder()
        {
            var first = new FakeSongDetail("first");
            var second = new FakeSongDetail("second");
            var unplayed = new FakeSongDetail("unplayed");
            Play(first, ChartLevel.Master);
            Play(first, ChartLevel.Expert);
            Play(second, ChartLevel.Basic);
            Play(unplayed, ChartLevel.Easy, isFalling: true);

            var added = ScoreExchange.SelectPlayed(new ISongDetail[] { first, second, unplayed });

            Assert.That(added, Is.EqualTo(3));
            Assert.That(MajEnv.SystemSettings.Score.ShareSelection, Is.EqualTo(new[] { "first|Expert", "first|Master", "second|Basic" }));
            Assert.That(ScoreExchange.IsSelected("unplayed", ChartLevel.Easy), Is.False);
        }

        /// <summary>Checks that picks already made are kept in place and not added twice.</summary>
        [Test]
        public void SelectPlayed_KeepsExistingPicks()
        {
            var song = new FakeSongDetail("song");
            var other = new FakeSongDetail("other");
            Play(song, ChartLevel.Advance);
            Play(song, ChartLevel.Master);
            ScoreExchange.ToggleSelection("other", ChartLevel.Easy);
            ScoreExchange.ToggleSelection("song", ChartLevel.Master);

            var added = ScoreExchange.SelectPlayed(new ISongDetail[] { song, other });

            Assert.That(added, Is.EqualTo(1));
            Assert.That(MajEnv.SystemSettings.Score.ShareSelection, Is.EqualTo(new[] { "other|Easy", "song|Master", "song|Advance" }));
            Assert.That(ScoreExchange.SelectPlayed(new ISongDetail[] { song, other }), Is.Zero);
        }

        /// <summary>Checks that the falling partition is read when the share options choose it.</summary>
        [Test]
        public void SelectPlayed_FollowsScoreStore()
        {
            var song = new FakeSongDetail("song");
            Play(song, ChartLevel.Easy);
            Play(song, ChartLevel.ReMaster, isFalling: true);
            MajEnv.SystemSettings.Score.ScoreStore = ScoreStoreOption.Falling;

            ScoreExchange.SelectPlayed(new ISongDetail[] { song });

            Assert.That(MajEnv.SystemSettings.Score.ShareSelection, Is.EqualTo(new[] { "song|ReMaster" }));
        }

        /// <summary>Checks that unpicking removes the played difficulties of the listed songs only.</summary>
        [Test]
        public void DeselectPlayed_RemovesOnlyListedSongs()
        {
            var listed = new FakeSongDetail("listed");
            var elsewhere = new FakeSongDetail("elsewhere");
            Play(listed, ChartLevel.Expert);
            Play(elsewhere, ChartLevel.Expert);
            ScoreExchange.SelectPlayed(new ISongDetail[] { listed, elsewhere });

            var removed = ScoreExchange.DeselectPlayed(new ISongDetail[] { listed });

            Assert.That(removed, Is.EqualTo(1));
            Assert.That(MajEnv.SystemSettings.Score.ShareSelection, Is.EqualTo(new[] { "elsewhere|Expert" }));
        }

        /// <summary>Checks the counts that decide between Select All and Deselect All.</summary>
        [Test]
        public void GetPlayedSelectionState_CountsPlayedAndPicked()
        {
            var song = new FakeSongDetail("song");
            var unplayed = new FakeSongDetail("unplayed");
            var songs = new ISongDetail[] { song, unplayed };

            Assert.That(ScoreExchange.GetPlayedSelectionState(songs), Is.EqualTo((0, 0)));

            Play(song, ChartLevel.Basic);
            Play(song, ChartLevel.Master);
            ScoreExchange.ToggleSelection("song", ChartLevel.Master);
            ScoreExchange.ToggleSelection("unplayed", ChartLevel.Master);

            Assert.That(ScoreExchange.GetPlayedSelectionState(songs), Is.EqualTo((2, 1)));

            ScoreExchange.SelectPlayed(songs);

            Assert.That(ScoreExchange.GetPlayedSelectionState(songs), Is.EqualTo((2, 2)));
        }
    }
}
