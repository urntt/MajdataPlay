#nullable enable
namespace MajdataPlay.Settings
{
    /// <summary>
    /// Where the slide tail becomes a Flick in falling mode.
    /// </summary>
    public enum FallingSlideFlickOption
    {
        /// <summary>
        /// The slide is judged along its arrows.
        /// </summary>
        Off,
        /// <summary>
        /// The whole slide becomes one Flick in the lane where it starts, reaching the judge line when the star starts moving.
        /// </summary>
        Start,
        /// <summary>
        /// The whole slide becomes one Flick in the lane where it ends, reaching the judge line when the star reaches the end.
        /// </summary>
        End,
    }
}
