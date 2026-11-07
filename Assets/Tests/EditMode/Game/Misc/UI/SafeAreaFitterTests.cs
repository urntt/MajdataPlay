using NUnit.Framework;
using UnityEngine;

#nullable enable
namespace MajdataPlay.Tests.Game.Misc.UI
{
    /// <summary>Checks the safe-area conversions of <see cref="SafeAreaFitter"/> and the sidebar widths of <see cref="SidebarDrawer"/>.</summary>
    public class SafeAreaFitterTests
    {
        /// <summary>The tolerance of the comparisons.</summary>
        private const float TOLERANCE = 0.0001f;

        /// <summary>Checks the anchors of a landscape phone with insets on the left, right and bottom.</summary>
        [Test]
        public void GetAnchors_ConvertsInsetsToNormalizedAnchors()
        {
            var safeArea = new Rect(141f, 63f, 2532f - 282f, 1170f - 63f);

            SafeAreaFitter.GetAnchors(safeArea, 2532f, 1170f, out var anchorMin, out var anchorMax);

            Assert.That(anchorMin.x, Is.EqualTo(141f / 2532f).Within(TOLERANCE));
            Assert.That(anchorMin.y, Is.EqualTo(63f / 1170f).Within(TOLERANCE));
            Assert.That(anchorMax.x, Is.EqualTo((2532f - 141f) / 2532f).Within(TOLERANCE));
            Assert.That(anchorMax.y, Is.EqualTo(1f).Within(TOLERANCE));
        }

        /// <summary>Checks that a full-screen safe area and invalid sizes cover the whole screen.</summary>
        [Test]
        public void GetAnchors_FullOrInvalidAreasCoverTheScreen()
        {
            SafeAreaFitter.GetAnchors(new Rect(0f, 0f, 1920f, 1080f), 1920f, 1080f, out var fullMin, out var fullMax);
            SafeAreaFitter.GetAnchors(new Rect(0f, 0f, 0f, 0f), 1920f, 1080f, out var emptyMin, out var emptyMax);
            SafeAreaFitter.GetAnchors(new Rect(0f, 0f, 100f, 100f), 0f, 1080f, out var noScreenMin, out var noScreenMax);

            Assert.That(fullMin, Is.EqualTo(Vector2.zero));
            Assert.That(fullMax, Is.EqualTo(Vector2.one));
            Assert.That(emptyMin, Is.EqualTo(Vector2.zero));
            Assert.That(emptyMax, Is.EqualTo(Vector2.one));
            Assert.That(noScreenMin, Is.EqualTo(Vector2.zero));
            Assert.That(noScreenMax, Is.EqualTo(Vector2.one));
        }

        /// <summary>Checks that the scaled offsets make the rectangle 1 / scale as large, so after scaling it covers the safe area.</summary>
        [Test]
        public void GetScaledOffsets_ShrinksTheRectangleByTheScale()
        {
            var safeSize = new Vector2(1000f, 500f);

            SafeAreaFitter.GetScaledOffsets(safeSize, 2f, out var offsetMin, out var offsetMax);

            var size = safeSize - offsetMin + offsetMax;
            Assert.That(size.x * 2f, Is.EqualTo(safeSize.x).Within(TOLERANCE));
            Assert.That(size.y * 2f, Is.EqualTo(safeSize.y).Within(TOLERANCE));
            Assert.That(offsetMin + offsetMax, Is.EqualTo(Vector2.zero), "the rectangle stays centered");
        }

        /// <summary>Checks that a scale of one, or an invalid scale, leaves the rectangle over the safe area.</summary>
        [Test]
        public void GetScaledOffsets_ScaleOneOrInvalidKeepsTheSafeArea()
        {
            SafeAreaFitter.GetScaledOffsets(new Vector2(1000f, 500f), 1f, out var oneMin, out var oneMax);
            SafeAreaFitter.GetScaledOffsets(new Vector2(1000f, 500f), 0f, out var zeroMin, out var zeroMax);

            Assert.That(oneMin, Is.EqualTo(Vector2.zero));
            Assert.That(oneMax, Is.EqualTo(Vector2.zero));
            Assert.That(zeroMin, Is.EqualTo(Vector2.zero));
            Assert.That(zeroMax, Is.EqualTo(Vector2.zero));
        }

        /// <summary>Checks the docked sidebar width: a quarter of the page, clamped to 260–360 only on short pages.</summary>
        [Test]
        public void GetDockedWidth_ClampsOnlyOnShortPages()
        {
            Assert.That(SidebarDrawer.GetDockedWidth(1223f, 602f), Is.EqualTo(1223f * 0.25f).Within(TOLERANCE));
            Assert.That(SidebarDrawer.GetDockedWidth(2000f, 600f), Is.EqualTo(360f).Within(TOLERANCE));
            Assert.That(SidebarDrawer.GetDockedWidth(900f, 600f), Is.EqualTo(260f).Within(TOLERANCE));
            Assert.That(SidebarDrawer.GetDockedWidth(1920f, 1080f), Is.EqualTo(480f).Within(TOLERANCE), "tablets and computers are unchanged");
        }
    }
}
