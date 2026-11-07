#nullable enable
using System;

namespace MajdataPlay
{
    /// <summary>
    /// Computes how large the code-built touch interfaces are drawn, from the screen size and its physical size.
    /// </summary>
    /// <remarks>
    /// The interfaces are laid out in canvas units for a 1920×1080 reference with width and height weighted equally, which looks
    /// right on tablets and computers but makes everything less than half as large physically on a phone. This class keeps that
    /// scale, except that the short side of the screen never holds more than <see cref="UNITS_PER_INCH"/> units per inch (and at
    /// least <see cref="MIN_SHORT_SIDE_UNITS"/> units), so phones get larger text and buttons while tablets and computers stay as
    /// they are. It has no Unity dependency so it can be tested on its own.
    /// </remarks>
    internal static class UiScaling
    {
        /// <summary>
        /// The width of the reference resolution the interfaces are designed for.
        /// </summary>
        public const float REFERENCE_WIDTH = 1920f;

        /// <summary>
        /// The height of the reference resolution the interfaces are designed for.
        /// </summary>
        public const float REFERENCE_HEIGHT = 1080f;

        /// <summary>
        /// The most canvas units one inch of the screen's short side may hold; fewer units per inch means a larger interface.
        /// </summary>
        public const float UNITS_PER_INCH = 250f;

        /// <summary>
        /// The fewest canvas units the short side of the screen keeps, so layouts still have room on very small screens.
        /// </summary>
        public const float MIN_SHORT_SIDE_UNITS = 600f;

        /// <summary>
        /// Gets the scale factor (screen pixels per canvas unit) of the reference layout: a 1920×1080 reference with width and
        /// height weighted equally, as Unity's <c>CanvasScaler</c> computes it with a match of 0.5.
        /// </summary>
        /// <param name="widthPx">The screen width in pixels.</param>
        /// <param name="heightPx">The screen height in pixels.</param>
        /// <returns>The scale factor, or 1 when the size is not positive.</returns>
        public static float GetReferenceScaleFactor(float widthPx, float heightPx)
        {
            if (widthPx <= 0f || heightPx <= 0f)
            {
                return 1f;
            }
            // 2^((log2(w/1920) + log2(h/1080)) / 2) = sqrt((w/1920) * (h/1080))
            return MathF.Sqrt(widthPx / REFERENCE_WIDTH * (heightPx / REFERENCE_HEIGHT));
        }

        /// <summary>
        /// Gets the scale factor (screen pixels per canvas unit) that keeps the interface at a comfortable physical size.
        /// </summary>
        /// <param name="widthPx">The screen width in pixels.</param>
        /// <param name="heightPx">The screen height in pixels.</param>
        /// <param name="dpi">The screen density in dots per inch, or 0 when unknown.</param>
        /// <returns>
        /// The reference scale factor, or a larger one when the reference would put more than <see cref="UNITS_PER_INCH"/> units on
        /// one inch of the short side; the reference factor when the density is unknown.
        /// </returns>
        public static float GetScaleFactor(float widthPx, float heightPx, float dpi)
        {
            var referenceFactor = GetReferenceScaleFactor(widthPx, heightPx);
            if (dpi <= 0f || widthPx <= 0f || heightPx <= 0f)
            {
                return referenceFactor;
            }
            var shortPx = MathF.Min(widthPx, heightPx);
            var targetShortUnits = MathF.Max(MIN_SHORT_SIDE_UNITS, shortPx / dpi * UNITS_PER_INCH);
            var referenceShortUnits = shortPx / referenceFactor;
            return targetShortUnits >= referenceShortUnits ? referenceFactor : shortPx / targetShortUnits;
        }

        /// <summary>
        /// Gets how many times larger <see cref="GetScaleFactor"/> draws the interface than the reference layout, for layouts that
        /// keep their own canvas scale (such as the falling-mode HUD) and only need to grow by the same amount.
        /// </summary>
        /// <param name="widthPx">The screen width in pixels.</param>
        /// <param name="heightPx">The screen height in pixels.</param>
        /// <param name="dpi">The screen density in dots per inch, or 0 when unknown.</param>
        /// <returns>1 on tablets and computers, more than 1 on phones.</returns>
        public static float GetEnlargement(float widthPx, float heightPx, float dpi)
        {
            var referenceFactor = GetReferenceScaleFactor(widthPx, heightPx);
            return MathF.Max(1f, GetScaleFactor(widthPx, heightPx, dpi) / referenceFactor);
        }
    }
}
