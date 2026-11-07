using MajdataPlay.Scenes.Game.Lane;
using NUnit.Framework;

#nullable enable
namespace MajdataPlay.Tests.Game.GamePlay.Lane
{
    /// <summary>Checks where <see cref="LaneHud.GetBottomTextBox"/> puts the title and the difficulty below the judge line.</summary>
    public class LaneHudLayoutTests
    {
        /// <summary>The tolerance of the comparisons.</summary>
        private const float TOLERANCE = 0.001f;

        /// <summary>Checks that a tablet's large gap keeps the original 24 above the bottom and 60 high.</summary>
        [Test]
        public void GetBottomTextBox_LargeGapKeepsTheOriginalBox()
        {
            LaneHud.GetBottomTextBox(211f, out var bottom, out var height);

            Assert.That(bottom, Is.EqualTo(24f).Within(TOLERANCE));
            Assert.That(height, Is.EqualTo(60f).Within(TOLERANCE));
        }

        /// <summary>Checks that a phone's small gap moves the texts down and makes them lower, keeping them under the judge line.</summary>
        [Test]
        public void GetBottomTextBox_SmallGapStaysBelowTheJudgeLine()
        {
            const float GAP = 78f;

            LaneHud.GetBottomTextBox(GAP, out var bottom, out var height);

            Assert.That(bottom, Is.EqualTo(GAP * 0.25f).Within(TOLERANCE));
            Assert.That(bottom + height, Is.LessThanOrEqualTo(GAP - 8f + TOLERANCE));
            Assert.That(height, Is.LessThan(60f));
        }

        /// <summary>Checks that a tiny gap still leaves a readable box.</summary>
        [Test]
        public void GetBottomTextBox_TinyGapKeepsMinimumHeight()
        {
            LaneHud.GetBottomTextBox(10f, out var bottom, out var height);

            Assert.That(bottom, Is.GreaterThanOrEqualTo(0f));
            Assert.That(height, Is.EqualTo(24f).Within(TOLERANCE));
        }
    }
}
