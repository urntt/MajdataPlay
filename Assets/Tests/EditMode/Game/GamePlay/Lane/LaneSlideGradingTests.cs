using MajdataPlay.Scenes.Game.Lane;
using MajdataPlay.Scenes.Game.Notes;
using NUnit.Framework;

#nullable enable
namespace MajdataPlay.Tests.Game.GamePlay.Lane
{
    /// <summary>Checks the falling-mode slide grading of <see cref="LaneSlideGrading"/>: the better of the position grade and the ratio grade.</summary>
    public class LaneSlideGradingTests
    {
        /// <summary>Checks the final grade for combinations where either the position grade or the ratio grade is the better one.</summary>
        /// <param name="hitCount">The number of check points hit.</param>
        /// <param name="totalCount">The number of check points.</param>
        /// <param name="isFirstHit">Whether the first check point was hit.</param>
        /// <param name="isLastHit">Whether the last check point was hit.</param>
        /// <param name="expected">The expected grade.</param>
        [TestCase(5, 5, true, true, JudgeGrade.Perfect, TestName = "AllHit_IsCriticalPerfect")]
        [TestCase(2, 5, true, true, JudgeGrade.LatePerfect2nd, TestName = "FirstAndLastOnly_IsPerfect")]
        [TestCase(4, 5, false, true, JudgeGrade.LatePerfect2nd, TestName = "MostHitWithoutFirst_RatioGivesPerfect")]
        [TestCase(3, 8, true, false, JudgeGrade.LateGreat, TestName = "FirstPlusOthers_PositionGivesGreat")]
        [TestCase(6, 8, false, false, JudgeGrade.LatePerfect2nd, TestName = "ThreeQuarters_RatioGivesPerfect")]
        [TestCase(1, 8, false, false, JudgeGrade.LateGood, TestName = "SingleHit_PositionGivesGood")]
        [TestCase(0, 8, false, false, JudgeGrade.Miss, TestName = "NothingHit_IsMiss")]
        [TestCase(1, 2, true, false, JudgeGrade.LatePerfect2nd, TestName = "HalfOfTwo_RatioGivesPerfect")]
        [TestCase(0, 0, false, false, JudgeGrade.Perfect, TestName = "NoCheckPoints_IsCriticalPerfect")]
        public void GetGrade_TakesTheBetterGrade(int hitCount, int totalCount, bool isFirstHit, bool isLastHit, JudgeGrade expected)
        {
            Assert.That(LaneSlideGrading.GetGrade(hitCount, totalCount, isFirstHit, isLastHit), Is.EqualTo(expected));
        }

        /// <summary>Checks the position grade (g1), which is unchanged from the earlier rule.</summary>
        [Test]
        public void GetPositionGrade_FollowsWhichPointsWereHit()
        {
            Assert.That(LaneSlideGrading.GetPositionGrade(6, 6, true, true), Is.EqualTo(JudgeGrade.Perfect));
            Assert.That(LaneSlideGrading.GetPositionGrade(2, 6, true, true), Is.EqualTo(JudgeGrade.LatePerfect2nd));
            Assert.That(LaneSlideGrading.GetPositionGrade(2, 6, true, false), Is.EqualTo(JudgeGrade.LateGreat));
            Assert.That(LaneSlideGrading.GetPositionGrade(1, 6, true, false), Is.EqualTo(JudgeGrade.LateGood));
            Assert.That(LaneSlideGrading.GetPositionGrade(5, 6, false, true), Is.EqualTo(JudgeGrade.LateGood));
            Assert.That(LaneSlideGrading.GetPositionGrade(0, 6, false, false), Is.EqualTo(JudgeGrade.Miss));
        }

        /// <summary>Checks the ratio thresholds (g2) for eight check points: 8, 6, 4 and 2 hits.</summary>
        /// <param name="hitCount">The number of check points hit.</param>
        /// <param name="expected">The expected grade.</param>
        [TestCase(8, JudgeGrade.Perfect)]
        [TestCase(7, JudgeGrade.LatePerfect2nd)]
        [TestCase(6, JudgeGrade.LatePerfect2nd)]
        [TestCase(5, JudgeGrade.LateGreat)]
        [TestCase(4, JudgeGrade.LateGreat)]
        [TestCase(3, JudgeGrade.LateGood)]
        [TestCase(2, JudgeGrade.LateGood)]
        [TestCase(1, JudgeGrade.Miss)]
        [TestCase(0, JudgeGrade.Miss)]
        public void GetRatioGrade_EightPoints(int hitCount, JudgeGrade expected)
        {
            Assert.That(LaneSlideGrading.GetRatioGrade(hitCount, 8), Is.EqualTo(expected));
        }

        /// <summary>Checks that the thresholds are floored and never below one hit, so short slides are not too strict.</summary>
        [Test]
        public void GetRatioGrade_SmallCountsUseAtLeastOneHit()
        {
            // x = 3: ⌊3⌋ = 3, ⌊2.25⌋ = 2, ⌊1.5⌋ = 1, max(1, ⌊0.75⌋) = 1
            Assert.That(LaneSlideGrading.GetRatioGrade(3, 3), Is.EqualTo(JudgeGrade.Perfect));
            Assert.That(LaneSlideGrading.GetRatioGrade(2, 3), Is.EqualTo(JudgeGrade.LatePerfect2nd));
            Assert.That(LaneSlideGrading.GetRatioGrade(1, 3), Is.EqualTo(JudgeGrade.LateGreat));
            // x = 1: every threshold is one hit
            Assert.That(LaneSlideGrading.GetRatioGrade(1, 1), Is.EqualTo(JudgeGrade.Perfect));
            Assert.That(LaneSlideGrading.GetRatioGrade(0, 1), Is.EqualTo(JudgeGrade.Miss));
        }
    }
}
