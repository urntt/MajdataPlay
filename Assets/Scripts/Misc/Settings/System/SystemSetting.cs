using Newtonsoft.Json;
#nullable enable
namespace MajdataPlay.Settings
{
    public class SystemSetting
    {
        public CommonOptions Common { get; init; } = new();

        /// <summary>
        /// Gets the gameplay modifiers, which only last for the current session unless <see cref="DebugOptions.SaveModSettings"/> is on.
        /// </summary>
        /// <remarks>
        /// 调试设置“保存 Mod 设置”开启时，Mod 设置随系统设置保存；关闭时不写入，读取时也由 <see cref="FallingSettingMigration"/> 丢弃，每次启动都是默认值。
        /// </remarks>
        public ModOptions Mod { get; init; } = new();

        /// <summary>
        /// Gets the falling mode settings, including the play style. They are stored on this device only.
        /// </summary>
        public FallingOptions Falling { get; init; } = new();

        /// <summary>
        /// Gets the score share, export and import settings. They are stored on this device only.
        /// </summary>
        public ScoreOptions Score { get; init; } = new();

        public RenderingOptions Rendering { get; init; } = new();

        [HideInSettingUI]
        public SoundOptions Audio { get; init; } = new();

        [ShowInSettingUIWhen($"{nameof(SettingRoot.System)}.{nameof(Common)}.{nameof(CommonOptions.RecordMode)}", RecordModeOption.FFmpeg)]
        public FFmpegEncodingOptions Encoding { get; init; } = new();

        public FFmpegDecodingOptions Decoding { get; init; } = new();

        public DebugOptions Debug { get; init; } = new();

        [HideInSettingUI]
        public NetworkOptions Network { get; init; } = new();

        [HideInSettingUI]
        public IOOptions IO { get; init; } = new();

        /// <summary>
        /// Determines whether <see cref="Mod"/> is written to the file: only while <see cref="DebugOptions.SaveModSettings"/> is on.
        /// </summary>
        /// <returns>True when the mod settings are saved; otherwise false.</returns>
        public bool ShouldSerializeMod()
        {
            return Debug.SaveModSettings;
        }

        /// <summary>
        /// Determines whether a modifier is active. Falling mode counts as one: its scores are not uploaded and are saved
        /// to the local falling partition only.
        /// </summary>
        /// <returns>True when a modifier or falling mode is on; otherwise false.</returns>
        public bool IsAnyModActive()
        {
            return IsAnyModActiveExceptPlayStyle() || Falling.PlayStyle != PlayStyleOption.Standard;
        }

        /// <summary>
        /// Determines whether a modifier other than the play style is active, in which case scores are not saved.
        /// The options that change the chart in falling mode (see <see cref="IsFallingChartModActive"/>) count as well;
        /// the falling options that only change the feel do not.
        /// </summary>
        /// <returns>True when scores must not be saved; otherwise false.</returns>
        public bool IsAnyModActiveExceptPlayStyle()
        {
            return Mod.IsAnyModActive() ||
                   (Falling.PlayStyle == PlayStyleOption.Falling && IsFallingChartModActive());
        }

        /// <summary>
        /// Determines whether an option that changes the chart in falling mode is on: Touch to Tap, slide tail to Flick, or the
        /// standard slide style. Like other modifiers, scores are not saved while one is on in falling mode.
        /// </summary>
        /// <remarks>Options that only change the feel, such as vertical judge, sweep or Break to Flick, do not count.</remarks>
        /// <returns>True when a chart-changing falling option is on; otherwise false.</returns>
        public bool IsFallingChartModActive()
        {
            return Mod.FallingTouchToTap ||
                   Mod.FallingSlideTailToFlick != FallingSlideFlickOption.Off ||
                   Falling.FallingSlideStyle == FallingSlideStyleOption.Standard;
        }
    }
}
