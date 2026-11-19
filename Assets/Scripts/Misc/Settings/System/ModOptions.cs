using MajdataPlay.Settings.OptionEnumerators;
#nullable enable
namespace MajdataPlay.Settings
{
    public class ModOptions
    {
        [Step("0.05")]
        [Range("0", null)]
        public float PlaybackSpeed { get; set; } = SettingDefaults.Mod.PlaybackSpeed;

        public AutoplayModeOption AutoPlay { get; set; } = SettingDefaults.Mod.AutoPlay;

        public JudgeStyleOption JudgeStyle { get; set; } = SettingDefaults.Mod.JudgeStyle;

        public bool SubdivideSlideJudgeGrade { get; set; } = SettingDefaults.Mod.SubdivideSlideJudgeGrade;

        public bool AllBreak { get; set; } = SettingDefaults.Mod.AllBreak;

        public bool AllEx { get; set; } = SettingDefaults.Mod.AllEx;

        public bool AllTouch { get; set; } = SettingDefaults.Mod.AllTouch;

        public bool SlideNoHead { get; set; } = SettingDefaults.Mod.SlideNoHead;

        public bool SlideNoTrack { get; set; } = SettingDefaults.Mod.SlideNoTrack;

        [OptionEnumerator(typeof(NoteMaskEnumerator))]
        public string NoteMask { get; set; } = SettingDefaults.Mod.NoteMask;

        /// <summary>
        /// Gets or sets whether break taps become Flicks in falling mode.
        /// </summary>
        /// <remarks>Only changes how breaks are hit, so scores are still saved.</remarks>
        public bool FallingBreakToFlick { get; set; } = SettingDefaults.Mod.FallingBreakToFlick;

        /// <summary>
        /// Gets or sets where the whole slide becomes one red Flick in falling mode, or <see cref="FallingSlideFlickOption.Off"/>.
        /// </summary>
        /// <remarks>Has no effect with the standard slide style. Changes the chart, so scores are not saved while it is on.</remarks>
        public FallingSlideFlickOption FallingSlideTailToFlick { get; set; } = SettingDefaults.Mod.FallingSlideTailToFlick;

        /// <summary>
        /// Gets or sets whether Touch and TouchHold notes become Tap and Hold notes in their lanes in falling mode.
        /// </summary>
        /// <remarks>Changes the chart, so scores are not saved while it is on.</remarks>
        public bool FallingTouchToTap { get; set; } = SettingDefaults.Mod.FallingTouchToTap;

        public bool IsAnyModActive()
        {
            return !(PlaybackSpeed == 1f &&
                !AllBreak && !AllEx && !AllTouch && AutoPlay == AutoplayModeOption.Disable && JudgeStyle == JudgeStyleOption.DEFAULT);
        }
    }
}
