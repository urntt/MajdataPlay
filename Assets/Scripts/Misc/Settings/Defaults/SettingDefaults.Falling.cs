#nullable enable
namespace MajdataPlay.Settings
{
    public static partial class SettingDefaults
    {
        /// <summary>
        /// The defaults of <see cref="FallingOptions"/>.
        /// </summary>
        public static class Falling
        {
            /// <summary>The default play style: standard.</summary>
            public const PlayStyleOption PlayStyle = PlayStyleOption.Standard;

            /// <summary>The default falling speed, which reads like TapSpeed.</summary>
            public const float FallingSpeed = 7.5f;

            /// <summary>The default of judging lane notes at any height.</summary>
            public const bool FallingVerticalJudge = true;

            /// <summary>The default of judging floating Touch notes at any height.</summary>
            public const bool FallingTouchVerticalJudge = false;

            /// <summary>The default of sweep taps.</summary>
            public const bool FallingSweep = false;

            /// <summary>The default of protecting slide fingers from sweep taps.</summary>
            public const bool FallingSweepSlideGuard = true;

            /// <summary>The default slide style: falling.</summary>
            public const FallingSlideStyleOption FallingSlideStyle = FallingSlideStyleOption.Falling;

            /// <summary>The default of showing lane dividers.</summary>
            public const bool FallingLaneDividers = true;

            /// <summary>The default of lighting up the lanes under the fingers.</summary>
            public const bool FallingLaneHighlight = false;

            /// <summary>The default of following the display settings for the gameplay info.</summary>
            public const bool FallingInfoFollowDisplay = false;

            /// <summary>The default of coloring the lanes by difficulty.</summary>
            public const bool FallingDifficultyColor = true;

            /// <summary>The default of coloring the score by achievement.</summary>
            public const bool FallingScoreColor = false;

            /// <summary>The default judge line color.</summary>
            public const FallingJudgeLineColorOption FallingJudgeLineColor = FallingJudgeLineColorOption.White;

            /// <summary>The revision that new falling settings are written with.</summary>
            public const int FallingSettingsRevision = FallingOptions.CURRENT_REVISION;
        }
    }
}
