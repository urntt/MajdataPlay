using System.Collections.Generic;
#nullable enable
namespace MajdataPlay.Settings
{
    public static partial class SettingDefaults
    {
        /// <summary>
        /// The defaults of <see cref="ScoreOptions"/>.
        /// </summary>
        public static class Score
        {
            /// <summary>The default score partition to share: the one of the current play style.</summary>
            public const ScoreStoreOption ScoreStore = ScoreStoreOption.PlayStyle;

            /// <summary>The default share scope: the best scores.</summary>
            public const ScoreShareScopeOption ShareScope = ScoreShareScopeOption.Best;

            /// <summary>The default number of best scores to share.</summary>
            public const int ShareBestCount = 50;

            /// <summary>The default share order.</summary>
            public const ScoreShareSortOption ShareSort = ScoreShareSortOption.LevelAchievementDx;

            /// <summary>The default order of a custom selection.</summary>
            public const ScoreShareCustomSortOption ShareCustomSort = ScoreShareCustomSortOption.SelectionOrder;

            /// <summary>The default share format: an image.</summary>
            public const ScoreShareFormatOption ShareFormat = ScoreShareFormatOption.Image;

            /// <summary>The default state of the export action: ready.</summary>
            public const ScoreActionState ExportBackup = ScoreActionState.Ready;

            /// <summary>The default backup file to import: none.</summary>
            public const string BackupFile = "";

            /// <summary>The default state of the import action: ready.</summary>
            public const ScoreActionState ImportBackup = ScoreActionState.Ready;

            /// <summary>Gets a new empty custom selection.</summary>
            public static List<string> ShareSelection => new();
        }
    }
}
