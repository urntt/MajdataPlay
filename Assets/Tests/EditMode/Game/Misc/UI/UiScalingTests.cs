using NUnit.Framework;

#nullable enable
namespace MajdataPlay.Tests.Game.Misc.UI
{
    /// <summary>Checks the physical-size scaling of the touch interfaces in <see cref="UiScaling"/>.</summary>
    public class UiScalingTests
    {
        /// <summary>The tolerance of the scale comparisons.</summary>
        private const float TOLERANCE = 0.001f;

        /// <summary>Checks the reference factor: the geometric mean of the width and height ratios to 1920×1080.</summary>
        [Test]
        public void GetReferenceScaleFactor_MatchesTheCanvasScaler()
        {
            Assert.That(UiScaling.GetReferenceScaleFactor(1920f, 1080f), Is.EqualTo(1f).Within(TOLERANCE));
            Assert.That(UiScaling.GetReferenceScaleFactor(3840f, 2160f), Is.EqualTo(2f).Within(TOLERANCE));
            Assert.That(UiScaling.GetReferenceScaleFactor(0f, 1080f), Is.EqualTo(1f));
        }

        /// <summary>Checks that tablets and computers keep the reference layout.</summary>
        /// <param name="width">The screen width in pixels.</param>
        /// <param name="height">The screen height in pixels.</param>
        /// <param name="dpi">The screen density.</param>
        [TestCase(2732f, 2048f, 264f, TestName = "IPadPro_KeepsReference")]
        [TestCase(2048f, 2732f, 264f, TestName = "IPadProPortrait_KeepsReference")]
        [TestCase(1920f, 1080f, 96f, TestName = "Desktop_KeepsReference")]
        [TestCase(2532f, 1170f, 0f, TestName = "UnknownDensity_KeepsReference")]
        public void GetScaleFactor_LargeScreensKeepTheReference(float width, float height, float dpi)
        {
            var reference = UiScaling.GetReferenceScaleFactor(width, height);

            Assert.That(UiScaling.GetScaleFactor(width, height, dpi), Is.EqualTo(reference).Within(TOLERANCE));
            Assert.That(UiScaling.GetEnlargement(width, height, dpi), Is.EqualTo(1f).Within(TOLERANCE));
        }

        /// <summary>Checks that a phone's short side holds 250 units per inch, making the interface about 1.5 times larger.</summary>
        [Test]
        public void GetScaleFactor_PhoneUsesUnitsPerInch()
        {
            const float SHORT_PX = 1170f;
            const float DPI = 460f;
            var expectedShortUnits = SHORT_PX / DPI * UiScaling.UNITS_PER_INCH;

            var factor = UiScaling.GetScaleFactor(2532f, SHORT_PX, DPI);

            Assert.That(SHORT_PX / factor, Is.EqualTo(expectedShortUnits).Within(0.1f));
            Assert.That(UiScaling.GetEnlargement(2532f, SHORT_PX, DPI), Is.EqualTo(1.539f).Within(0.01f));
            Assert.That(UiScaling.GetScaleFactor(SHORT_PX, 2532f, DPI), Is.EqualTo(factor).Within(TOLERANCE), "portrait matches landscape");
        }

        /// <summary>Checks that very small screens keep at least 600 units on the short side.</summary>
        [Test]
        public void GetScaleFactor_SmallPhoneKeepsMinimumUnits()
        {
            var factor = UiScaling.GetScaleFactor(1334f, 750f, 326f);

            Assert.That(750f / factor, Is.EqualTo(UiScaling.MIN_SHORT_SIDE_UNITS).Within(0.1f));
        }
    }
}
