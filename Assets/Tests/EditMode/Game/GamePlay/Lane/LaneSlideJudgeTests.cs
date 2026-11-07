using MajdataPlay.Scenes.Game.Lane;
using NUnit.Framework;

#nullable enable
namespace MajdataPlay.Tests.Game.GamePlay.Lane
{
    /// <summary>Checks <see cref="LaneSlideJudge.FirstHitTiming"/>, which delays the slide sound until a hit check point arrives.</summary>
    public class LaneSlideJudgeTests
    {
        /// <summary>The tolerance of the timing comparisons, in seconds.</summary>
        private const float TOLERANCE = 0.0001f;

        /// <summary>Creates a straight slide in the middle of the lanes from 1.0 s to 1.5 s.</summary>
        /// <returns>The judge.</returns>
        private static LaneSlideJudge CreateStraightSlide()
        {
            var path = new[] { new LanePathVertex(1f, 0f), new LanePathVertex(1.5f, 0f) };
            return new LaneSlideJudge(new[] { path }, 1f, 0.5f);
        }

        /// <summary>Checks that a finger resting where the slide starts hits it early, while the first hit timing stays at its arrival.</summary>
        [Test]
        public void FirstHitTiming_RestingFingerKeepsArrivalTime()
        {
            var judge = CreateStraightSlide();
            var fingers = new[] { new LaneFinger(1, 0f) };

            judge.Update(0.6f, fingers, null);
            Assert.That(judge.FirstHitTiming, Is.Null, "0.4 s early is outside the window");

            var isNewlyHit = judge.Update(0.71f, fingers, null);
            Assert.That(isNewlyHit, Is.True, "0.29 s early hits the first check point");
            Assert.That(judge.FirstHitTiming, Is.EqualTo(1f).Within(TOLERANCE));
            Assert.That(judge.IsResolved, Is.False, "hit check points wait for their arrival");

            judge.Update(0.95f, fingers, null);
            Assert.That(judge.FirstHitTiming, Is.EqualTo(1f).Within(TOLERANCE), "later hits keep the earliest arrival");
        }

        /// <summary>Checks that a finger joining late reports the arrival of the first check point it hit.</summary>
        [Test]
        public void FirstHitTiming_LateFingerUsesItsCheckPoint()
        {
            var judge = CreateStraightSlide();
            var noFingers = new LaneFinger[0];
            var fingers = new[] { new LaneFinger(2, 0f) };

            judge.Update(1.35f, noFingers, null);
            Assert.That(judge.FirstHitTiming, Is.Null);

            judge.Update(1.36f, fingers, null);
            Assert.That(judge.FirstHitTiming, Is.Not.Null);
            Assert.That(judge.FirstHitTiming!.Value, Is.GreaterThan(1.05f), "the first check points have passed");
            Assert.That(judge.FirstHitTiming!.Value, Is.LessThanOrEqualTo(1.36f + 0.3f));
        }

        /// <summary>Checks that a finger far from the slide never hits it.</summary>
        [Test]
        public void FirstHitTiming_FarFingerNeverHits()
        {
            var judge = CreateStraightSlide();
            var fingers = new[] { new LaneFinger(3, LaneLayout.LaneWidth * 4f) };

            judge.Update(1f, fingers, null);
            judge.Update(1.5f, fingers, null);

            Assert.That(judge.FirstHitTiming, Is.Null);
        }
    }
}
