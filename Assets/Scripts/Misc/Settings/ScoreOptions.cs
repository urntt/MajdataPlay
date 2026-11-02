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
        public ScoreStoreOption ScoreStore { get; set; } = ScoreStoreOption.PlayStyle;

        public ScoreShareScopeOption ShareScope { get; set; } = ScoreShareScopeOption.Best;
        /// <summary>
        /// 分享“最好的若干个”时的数量
        /// </summary>
        [Step("5")]
        [Range("5", "200")]
        public int ShareBestCount { get; set; } = 50;
        /// <summary>
        /// “最好的若干个”与“全部”的排序（先排序，再取前若干个）
        /// </summary>
        public ScoreShareSortOption ShareSort { get; set; } = ScoreShareSortOption.LevelAchievementDx;
        /// <summary>
        /// 自选谱面的排序：按勾选顺序，或跟随“分享排序”
        /// </summary>
        public ScoreShareCustomSortOption ShareCustomSort { get; set; } = ScoreShareCustomSortOption.SelectionOrder;
        /// <summary>
        /// 自选的谱面，按勾选顺序，每项为“哈希|难度”；在列表式选歌里勾选
        /// </summary>
        [HideInSettingUI]
        public List<string> ShareSelection { get; set; } = new();

        public ScoreShareFormatOption ShareFormat { get; set; } = ScoreShareFormatOption.Image;

        [JsonIgnore]
        [OptionEnumerator(typeof(ScoreActionEnumerator))]
        public ScoreActionState Share
        {
            get => ScoreExchange.ShareState;
            set
            {
                if (value == ScoreActionState.Running)
                {
                    ScoreExchange.StartShare(this);
                }
            }
        }

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
        public string BackupFile { get; set; } = string.Empty;

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
