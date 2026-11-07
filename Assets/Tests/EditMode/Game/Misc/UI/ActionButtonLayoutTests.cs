using NUnit.Framework;

#nullable enable
namespace MajdataPlay.Tests.Game.Misc.UI
{
    /// <summary>Checks the size, gap and bottom distance of the floating icon buttons in <see cref="LightUi"/>.</summary>
    public class ActionButtonLayoutTests
    {
        /// <summary>The tolerance of the comparisons.</summary>
        private const float TOLERANCE = 0.001f;

        /// <summary>Checks that the buttons are 120 on tall pages and at most 12% of short pages, but at least 48.</summary>
        [Test]
        public void GetActionButtonSize_CapsOnShortPages()
        {
            Assert.That(LightUi.GetActionButtonSize(1247f), Is.EqualTo(120f).Within(TOLERANCE));
            Assert.That(LightUi.GetActionButtonSize(602f), Is.EqualTo(602f * 0.12f).Within(TOLERANCE));
            Assert.That(LightUi.GetActionButtonSize(300f), Is.EqualTo(48f).Within(TOLERANCE));
        }

        /// <summary>Checks that the gap is a quarter of the button size.</summary>
        [Test]
        public void GetActionButtonGap_IsAQuarterOfTheSize()
        {
            Assert.That(LightUi.GetActionButtonGap(120f), Is.EqualTo(30f).Within(TOLERANCE));
            Assert.That(LightUi.GetActionButtonGap(72f), Is.EqualTo(18f).Within(TOLERANCE));
        }

        /// <summary>Checks that the distance from the screen bottom equals the right margin, unless the home indicator is taller.</summary>
        [Test]
        public void GetActionButtonBottom_MatchesTheRightMargin()
        {
            Assert.That(LightUi.GetActionButtonBottom(48f, 0f), Is.EqualTo(48f).Within(TOLERANCE), "no inset");
            Assert.That(LightUi.GetActionButtonBottom(48f, 24f) + 24f, Is.EqualTo(48f).Within(TOLERANCE), "iPad home indicator");
            Assert.That(LightUi.GetActionButtonBottom(40f, 55f), Is.EqualTo(LightUi.ACTION_BUTTON_MIN_BOTTOM).Within(TOLERANCE), "phone home indicator");
            Assert.That(LightUi.GetActionButtonBottom(40f, -5f), Is.EqualTo(40f).Within(TOLERANCE), "negative inset ignored");
        }
    }
}
