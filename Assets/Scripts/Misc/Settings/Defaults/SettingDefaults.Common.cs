#nullable enable
namespace MajdataPlay.Settings
{
    public static partial class SettingDefaults
    {
        public static class Common
        {
            public const RecordModeOption RecordMode = RecordModeOption.Disable;

            public const int MenuOptionIterationSpeed = 45;

            public const bool HideCursorInGame = true;

            public const bool NoteFolding = true;

            public const DJAutoPolicyOption DJAutoPolicy = DJAutoPolicyOption.Strict;

            /// <summary>The default interface style: the maimai style.</summary>
            public const ListStyleOption ListStyle = ListStyleOption.Cover;

            /// <summary>The default number of songs on a page of the Phigros style song list.</summary>
            public const int ListPageSize = 20;

            /// <summary>The default of starting song previews at once.</summary>
            public const bool ListInstantPreview = false;
        }
    }
}
