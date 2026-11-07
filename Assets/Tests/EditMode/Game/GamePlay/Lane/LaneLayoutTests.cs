using MajdataPlay.Scenes.Game.Lane;
using NUnit.Framework;

#nullable enable
namespace MajdataPlay.Tests.Game.GamePlay.Lane
{
    /// <summary>Checks the lane area width of <see cref="LaneLayout.GetLaneAreaWidth"/> for different screens.</summary>
    public class LaneLayoutTests
    {
        /// <summary>The tolerance of the width comparisons, in world units.</summary>
        private const float TOLERANCE = 0.01f;

        /// <summary>Checks that a 4:3 tablet keeps the base width, which is already 85% of what the camera shows.</summary>
        [Test]
        public void GetLaneAreaWidth_FourThreeKeepsTheBaseWidth()
        {
            var width = LaneLayout.GetLaneAreaWidth(2732f, 2048f, 2732f);

            Assert.That(width, Is.EqualTo(LaneLayout.BASE_LANE_AREA_WIDTH).Within(TOLERANCE));
        }

        /// <summary>Checks that a 16:9 screen without insets uses 85% of the 19.2 units the camera shows.</summary>
        [Test]
        public void GetLaneAreaWidth_SixteenNineUsesEightyFivePercent()
        {
            var width = LaneLayout.GetLaneAreaWidth(1920f, 1080f, 1920f);

            Assert.That(width, Is.EqualTo(19.2f * LaneLayout.SAFE_WIDTH_SHARE).Within(TOLERANCE));
        }

        /// <summary>Checks that a phone in landscape uses 85% of the visible width inside its safe area.</summary>
        [Test]
        public void GetLaneAreaWidth_PhoneUsesTheSafeArea()
        {
            const float SCREEN_WIDTH = 2532f;
            const float SCREEN_HEIGHT = 1170f;
            const float SAFE_WIDTH = 2532f - 141f * 2f;
            var visibleWidth = 10.8f * (SCREEN_WIDTH / SCREEN_HEIGHT);

            var width = LaneLayout.GetLaneAreaWidth(SCREEN_WIDTH, SCREEN_HEIGHT, SAFE_WIDTH);

            Assert.That(width, Is.EqualTo(visibleWidth * (SAFE_WIDTH / SCREEN_WIDTH) * LaneLayout.SAFE_WIDTH_SHARE).Within(TOLERANCE));
            Assert.That(width / LaneLayout.BASE_LANE_AREA_WIDTH, Is.GreaterThan(1.25f), "phone lanes are clearly wider than before");
        }

        /// <summary>Checks that screens narrower than 4:3 and invalid sizes fall back to the base width.</summary>
        [Test]
        public void GetLaneAreaWidth_NarrowOrInvalidScreensUseTheBaseWidth()
        {
            Assert.That(LaneLayout.GetLaneAreaWidth(1080f, 1920f, 1080f), Is.EqualTo(LaneLayout.BASE_LANE_AREA_WIDTH).Within(TOLERANCE));
            Assert.That(LaneLayout.GetLaneAreaWidth(0f, 1080f, 0f), Is.EqualTo(LaneLayout.BASE_LANE_AREA_WIDTH));
            Assert.That(LaneLayout.GetLaneAreaWidth(1920f, 0f, 1920f), Is.EqualTo(LaneLayout.BASE_LANE_AREA_WIDTH));
        }
    }
}
