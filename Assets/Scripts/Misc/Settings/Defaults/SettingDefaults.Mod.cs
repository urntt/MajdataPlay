#nullable enable
namespace MajdataPlay.Settings
{
    public static partial class SettingDefaults
    {
        public static class Mod
        {
            public const float PlaybackSpeed = 1f;

            public const AutoplayModeOption AutoPlay = AutoplayModeOption.Disable;

            public const JudgeStyleOption JudgeStyle = JudgeStyleOption.DEFAULT;

            public const bool SubdivideSlideJudgeGrade = false;

            public const bool AllBreak = false;

            public const bool AllEx = false;

            public const bool AllTouch = false;

            public const bool SlideNoHead = false;

            public const bool SlideNoTrack = false;

            public const string NoteMask = "Disable";

            /// <summary>The default of turning break taps into Flicks in falling mode.</summary>
            public const bool FallingBreakToFlick = false;

            /// <summary>The default of turning whole slides into Flicks in falling mode: off.</summary>
            public const FallingSlideFlickOption FallingSlideTailToFlick = FallingSlideFlickOption.Off;

            /// <summary>The default of turning Touch notes into lane notes in falling mode.</summary>
            public const bool FallingTouchToTap = false;
        }
    }
}
