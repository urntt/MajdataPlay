using System.Collections.Generic;
using MajdataPlay.Settings.OptionEnumerators;
using MajdataPlay.Settings.Runtime;
using Newtonsoft.Json;
#nullable enable
namespace MajdataPlay.Settings
{
    /// <summary>
    /// 成绩的分享、导出与导入。选项类随设置保存，动作类不保存，在该行按左右键即执行（见 <see cref="ScoreExchange"/>）
    /// </summary>
    public class ScoreOptions
    {
        /// <summary>
        /// Gets or sets which score partition is shared: the current play style, standard or falling.
        /// </summary>
        /// <remarks>The share options are edited in the share dialog of the Phigros style song list, not in the settings.</remarks>
        [HideInSettingUI]
        public ScoreStoreOption ScoreStore { get; set; } = SettingDefaults.Score.ScoreStore;

        /// <summary>
        /// Gets or sets which charts are shared: the best ones, all played charts or the picked charts.
        /// </summary>
        [HideInSettingUI]
        public ScoreShareScopeOption ShareScope { get; set; } = SettingDefaults.Score.ShareScope;
        /// <summary>
        /// 分享“最好的若干个”时的数量
        /// </summary>
        [HideInSettingUI]
        [Step("5")]
        [Range("5", "200")]
        public int ShareBestCount { get; set; } = SettingDefaults.Score.ShareBestCount;
        /// <summary>
        /// “最好的若干个”与“全部”的排序（先排序，再取前若干个）
        /// </summary>
        [HideInSettingUI]
        public ScoreShareSortOption ShareSort { get; set; } = SettingDefaults.Score.ShareSort;
        /// <summary>
        /// 自选谱面的排序：按勾选顺序，或跟随“分享排序”
        /// </summary>
        [HideInSettingUI]
        public ScoreShareCustomSortOption ShareCustomSort { get; set; } = SettingDefaults.Score.ShareCustomSort;
        /// <summary>
        /// 自选的谱面，按勾选顺序，每项为“哈希|难度”；在列表式选歌里勾选
        /// </summary>
        [HideInSettingUI]
        public List<string> ShareSelection { get; set; } = SettingDefaults.Score.ShareSelection;

        /// <summary>
        /// Gets or sets the file format used to share scores.
        /// </summary>
        [HideInSettingUI]
        public ScoreShareFormatOption ShareFormat { get; set; } = SettingDefaults.Score.ShareFormat;

        [JsonIgnore]
        [OptionEnumerator(typeof(ScoreActionEnumerator))]
        public ScoreActionState ExportBackup
        {
            get => ScoreExchange.ExportState;
            set
            {
                if (value == ScoreActionState.Running)
                {
                    ScoreExchange.StartExport(this);
                }
            }
        }
        /// <summary>
        /// 要导入的备份文件名（备份文件夹里的文件）
        /// </summary>
        [JsonIgnore]
        [NoTranslation(TranslationTarget.OptionValue)]
        [OptionEnumerator(typeof(ScoreBackupFileEnumerator))]
        public string BackupFile { get; set; } = SettingDefaults.Score.BackupFile;

        [JsonIgnore]
        [OptionEnumerator(typeof(ScoreActionEnumerator))]
        public ScoreActionState ImportBackup
        {
            get => ScoreExchange.ImportState;
            set
            {
                if (value == ScoreActionState.Running)
                {
                    ScoreExchange.StartImport(BackupFile);
                }
            }
        }
    }
}
