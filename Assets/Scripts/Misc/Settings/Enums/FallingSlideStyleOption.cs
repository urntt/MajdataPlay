#nullable enable
namespace MajdataPlay.Settings
{
    /// <summary>
    /// How slides look and are judged in falling mode.
    /// </summary>
    public enum FallingSlideStyleOption
    {
        /// <summary>
        /// As in standard play: star heads on the outer ring, slides in the middle of the screen, judged by the circle sensors.
        /// </summary>
        Standard,
        /// <summary>
        /// The arrows fall to the judge line and are judged by check points along them.
        /// </summary>
        Falling,
        /// <summary>
        /// The arrows fall to a second, lighter judge line above the judge line; only fingers above that line hit them.
        /// </summary>
        Floating,
    }
}
