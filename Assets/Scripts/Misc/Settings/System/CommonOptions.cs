#nullable enable
namespace MajdataPlay.Settings
{
    public class CommonOptions
    {
        public RecordModeOption RecordMode { get; set; } = SettingDefaults.Common.RecordMode;

        [HideInSettingUI]
        public int MenuOptionIterationSpeed { get; set; } = SettingDefaults.Common.MenuOptionIterationSpeed;

        [HideInSettingUI]
        [DisableValues(Platforms = SettingPlatform.Mobile)]
        public bool HideCursorInGame { get; set; } = SettingDefaults.Common.HideCursorInGame;

        [HideInSettingUI]
        [JsonIgnoreOnPlatform(SettingPlatform.IOS)]
        public bool NoteFolding { get; set; } = SettingDefaults.Common.NoteFolding;

        public DJAutoPolicyOption DJAutoPolicy { get; set; } = SettingDefaults.Common.DJAutoPolicy;

        /// <summary>
        /// Gets or sets the interface style: the maimai style with the cover carousel, or the Phigros style made for
        /// touch, which replaces the song list, the settings and the result screen.
        /// </summary>
        public ListStyleOption ListStyle { get; set; } = SettingDefaults.Common.ListStyle;

        /// <summary>
        /// Gets or sets how many songs a page of the Phigros style song list shows.
        /// </summary>
        [Step("5")]
        [Range("5", "100")]
        public int ListPageSize { get; set; } = SettingDefaults.Common.ListPageSize;

        /// <summary>
        /// Gets or sets whether the song list starts the preview of a newly selected song at once, without going back to
        /// the song list music in between; when off, the preview waits for one second.
        /// </summary>
        public bool ListInstantPreview { get; set; } = SettingDefaults.Common.ListInstantPreview;
    }
}
