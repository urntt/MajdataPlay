using MajdataPlay.Scenes.Game.Lane;
using NUnit.Framework;

#nullable enable
namespace MajdataPlay.Tests.Game.GamePlay.Lane
{
    /// <summary>Checks <see cref="LaneInputProvider.IsInStarZone"/>, which decides whether a new finger was pressed on a star.</summary>
    public class LaneInputProviderTests
    {
        /// <summary>Checks that a press within the half width of any zone counts, and one outside all zones does not.</summary>
        [Test]
        public void IsInStarZone_ChecksEveryZone()
        {
            var zones = new[] { -3f, 2f };

            Assert.That(LaneInputProvider.IsInStarZone(-2.2f, zones, 1f), Is.True, "near the first zone");
            Assert.That(LaneInputProvider.IsInStarZone(3f, zones, 1f), Is.True, "on the edge of the second zone");
            Assert.That(LaneInputProvider.IsInStarZone(0f, zones, 1f), Is.False, "between the zones");
        }

        /// <summary>Checks that no star in progress means no star finger.</summary>
        [Test]
        public void IsInStarZone_NoZonesIsFalse()
        {
            Assert.That(LaneInputProvider.IsInStarZone(0f, new float[0], 1f), Is.False);
        }
    }
}
