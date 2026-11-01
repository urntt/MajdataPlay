using Cysharp.Threading.Tasks;
using MajdataPlay.Collections;
using MajdataPlay.Diagnostics;
using MajdataPlay.Json;
using MajdataPlay.Scenes.Game;
using MajdataPlay.Settings;
using MajdataPlay.Settings.OptionEnumerators;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
#nullable enable
namespace MajdataPlay
{
    /// <summary>
    /// 设置“成绩”分类里的动作：分享成绩（图片、文字、CSV、JSON）、导出备份、导入备份
    /// </summary>
    internal static class ScoreExchange
    {
        const string BACKUP_FORMAT = "MajdataPlay.Scores";
        const int BACKUP_VERSION = 1;

        public static ScoreActionState ShareState { get; private set; } = ScoreActionState.Ready;
        public static ScoreActionState ExportState { get; private set; } = ScoreActionState.Ready;
        public static ScoreActionState ImportState { get; private set; } = ScoreActionState.Ready;
        /// <summary>
        /// 备份文件夹：iOS 可在“文件”App 中看到，Android 在 /sdcard/Documents/MajdataPlay 下
        /// </summary>
        public static string BackupDirectory => Path.Combine(MajEnv.RootPath, "ScoreBackups");
        static string ShareDirectory => Path.Combine(MajEnv.RootPath, "ScoreShares");

        readonly static JsonSerializerSettings _jsonSettings = new()
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Converters = new List<JsonConverter>
            {
                new JudgeDetailConverter(),
                new JudgeInfoConverter(),
            }
        };
        /// <summary>
        /// 分享的一条成绩：一首歌的一个难度
        /// </summary>
        internal sealed class ScoreEntry
        {
            public required MaiScore Score { get; init; }
            public required string SongHash { get; init; }
            public ISongDetail? Song { get; init; }
            public string LevelText { get; init; } = string.Empty;
            /// <summary>
            /// 等级的数值，用于排序：“13+”按 13.7 计，无法识别时为 -1
            /// </summary>
            public float LevelValue { get; init; } = -1;
            public ChartLevel Level => Score.ChartLevel;
            public bool IsPlayed => Score.PlayCount > 0;
            public string Title => Song?.Title ?? "MAJTEXT_SCORE_UNKNOWN_SONG".i18n();
            public string Artist => Song?.Artist ?? string.Empty;
            public string DifficultyName => GetDifficultyName(Level);
            public string AchievementText => IsPlayed ? Score.Acc.DX.ToString("0.0000", CultureInfo.InvariantCulture) + "%" : "—";
            public string GradeText => IsPlayed ? GetGrade(Score.Acc.DX) : "MAJTEXT_SCORE_UNPLAYED".i18n();
            public string ComboText => GetComboText(Score.ComboState);
        }

        public static void StartShare(ScoreOptions options)
        {
            if (ShareState == ScoreActionState.Running)
            {
                return;
            }
            ShareState = ScoreActionState.Running;
            ShareAsync(options).Forget();
        }
        public static void StartExport(ScoreOptions options)
        {
            if (ExportState == ScoreActionState.Running)
            {
                return;
            }
            ExportState = ScoreActionState.Running;
            ExportAsync(options).Forget();
        }
        public static void StartImport(string fileName)
        {
            if (ImportState == ScoreActionState.Running)
            {
                return;
            }
            ImportState = ScoreActionState.Running;
            ImportAsync(fileName).Forget();
        }
        /// <summary>
        /// 备份文件夹里的备份文件名，最新的在前
        /// </summary>
        public static string[] ListBackupFiles()
        {
            try
            {
                var directory = new DirectoryInfo(BackupDirectory);
                if (!directory.Exists)
                {
                    return Array.Empty<string>();
                }
                return directory.GetFiles("*.json")
                                .OrderByDescending(x => x.LastWriteTimeUtc)
                                .Select(x => x.Name)
                                .ToArray();
            }
            catch (Exception e)
            {
                MajDebug.LogError(e);
                return Array.Empty<string>();
            }
        }

        static async UniTaskVoid ShareAsync(ScoreOptions options)
        {
            try
            {
                var isFalling = IsFallingStore(options);
                var entries = CollectEntries(options, isFalling);
                if (entries.Count == 0)
                {
                    MajDebug.LogWarning("[ScoreExchange] Nothing to share");
                    ShareState = ScoreActionState.Failed;
                    return;
                }
                var title = BuildTitle(options, isFalling, entries.Count);
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                var directory = PrepareShareDirectory();
                var baseName = $"MajdataPlay-Scores-{stamp}";
                switch (options.ShareFormat)
                {
                    case ScoreShareFormatOption.Text:
                        {
                            var text = BuildText(title, entries);
                            NativeShare.ShareText(text, title, Path.Combine(directory, baseName + ".txt"));
                        }
                        break;
                    case ScoreShareFormatOption.Csv:
                        {
                            var path = Path.Combine(directory, baseName + ".csv");
                            // 带 BOM，Excel 打开时中日文不乱码
                            File.WriteAllText(path, BuildCsv(entries), new UTF8Encoding(true));
                            NativeShare.ShareFiles(new[] { path }, "text/csv", title);
                        }
                        break;
                    case ScoreShareFormatOption.Json:
                        {
                            var path = Path.Combine(directory, baseName + ".json");
                            File.WriteAllText(path, BuildJson(title, entries, isFalling), new UTF8Encoding(false));
                            NativeShare.ShareFiles(new[] { path }, "application/json", title);
                        }
                        break;
                    default:
                        {
                            var paths = await ScoreImageRenderer.RenderAsync(entries, title, directory, baseName);
                            NativeShare.ShareFiles(paths, "image/png", title);
                        }
                        break;
                }
                ShareState = ScoreActionState.Done;
            }
            catch (Exception e)
            {
                MajDebug.LogError(e);
                ShareState = ScoreActionState.Failed;
            }
        }
        static async UniTaskVoid ExportAsync(ScoreOptions options)
        {
            try
            {
                var scores = ScoreManager.GetAllStoredScores();
                var serializer = JsonSerializer.Create(_jsonSettings);
                var root = new JObject
                {
                    ["format"] = BACKUP_FORMAT,
                    ["version"] = BACKUP_VERSION,
                    ["exportedAt"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    ["scores"] = JArray.FromObject(scores, serializer),
                };
                var json = root.ToString(Formatting.Indented);
                var fileName = $"MajdataPlay-Scores-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.json";
                var directory = BackupDirectory;
                await UniTask.SwitchToThreadPool();
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, fileName), json, new UTF8Encoding(false));
                await UniTask.SwitchToMainThread();
                // 选中刚导出的文件，备份文件列表随之刷新
                options.BackupFile = fileName;
                MajDebug.LogInfo($"[ScoreExchange] Exported {scores.Count} scores to {fileName}");
                ExportState = ScoreActionState.Done;
            }
            catch (Exception e)
            {
                await UniTask.SwitchToMainThread();
                MajDebug.LogError(e);
                ExportState = ScoreActionState.Failed;
            }
        }
        static async UniTaskVoid ImportAsync(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName) || fileName == ScoreBackupFileEnumerator.NONE)
                {
                    ImportState = ScoreActionState.Failed;
                    return;
                }
                var path = Path.Combine(BackupDirectory, Path.GetFileName(fileName));
                await UniTask.SwitchToThreadPool();
                var json = File.ReadAllText(path);
                await UniTask.SwitchToMainThread();
                // 兼容旧版成绩文件的拼写错误
                json = json.Replace("\"JudgeDeatil\"", "\"JudgeDetail\"");
                var token = JToken.Parse(json);
                // 本功能导出的备份是带 scores 的对象，旧版成绩文件是纯列表
                var scoresToken = token is JObject obj ? obj["scores"] : token;
                var scores = scoresToken?.ToObject<List<MaiScore>>(JsonSerializer.Create(_jsonSettings));
                if (scores is null)
                {
                    ImportState = ScoreActionState.Failed;
                    return;
                }
                var changedCount = await ScoreManager.MergeScoresAsync(scores);
                MajDebug.LogInfo($"[ScoreExchange] Imported {fileName}: {scores.Count} records, {changedCount} changed");
                ImportState = ScoreActionState.Done;
            }
            catch (Exception e)
            {
                await UniTask.SwitchToMainThread();
                MajDebug.LogError(e);
                ImportState = ScoreActionState.Failed;
            }
        }
        static bool IsFallingStore(ScoreOptions options)
        {
            return options.ScoreStore switch
            {
                ScoreStoreOption.Standard => false,
                ScoreStoreOption.Falling => true,
                _ => MajEnv.Settings.Falling.PlayStyle == PlayStyleOption.Falling,
            };
        }
        /// <summary>
        /// 按设置的范围取出要分享的成绩。“最好的若干个”与“全部”先按难度（等级数值，相同时按难度类别），
        /// 再按达成率、DX 分排序，同一首歌的不同难度各算一条
        /// </summary>
        static List<ScoreEntry> CollectEntries(ScoreOptions options, bool isFalling)
        {
            var songs = BuildSongLookup();
            var entries = new List<ScoreEntry>();
            if (options.ShareScope == ScoreShareScopeOption.CurrentChart)
            {
                var listConfig = MajEnv.RuntimeConfig.List;
                var hash = listConfig.SelectedSongHash;
                if (!string.IsNullOrEmpty(hash))
                {
                    var level = listConfig.SelectedDiff;
                    var score = ScoreManager.FindScore(hash, level, isFalling) ?? new MaiScore()
                    {
                        Hash = hash,
                        ChartLevel = level,
                    };
                    entries.Add(CreateEntry(score, hash, songs));
                }
                return entries;
            }
            foreach (var score in ScoreManager.GetAllScores(isFalling))
            {
                entries.Add(CreateEntry(score, ScoreManager.GetSongHash(score), songs));
            }
            entries.Sort(CompareEntries);
            if (options.ShareScope == ScoreShareScopeOption.Best && entries.Count > options.ShareBestCount)
            {
                entries.RemoveRange(options.ShareBestCount, entries.Count - options.ShareBestCount);
            }
            return entries;
        }
        static Dictionary<string, ISongDetail> BuildSongLookup()
        {
            var songs = new Dictionary<string, ISongDetail>();
            foreach (var collection in SongStorage.Collections)
            {
                foreach (var song in collection)
                {
                    if (!string.IsNullOrEmpty(song.Hash))
                    {
                        songs.TryAdd(song.Hash, song);
                    }
                }
            }
            return songs;
        }
        static ScoreEntry CreateEntry(MaiScore score, string songHash, Dictionary<string, ISongDetail> songs)
        {
            songs.TryGetValue(songHash, out var song);
            var levelText = string.Empty;
            if (song is not null)
            {
                var levels = song.Levels;
                var index = (int)score.ChartLevel;
                if (index < levels.Length)
                {
                    levelText = levels[index] ?? string.Empty;
                }
            }
            return new ScoreEntry()
            {
                Score = score,
                SongHash = songHash,
                Song = song,
                LevelText = levelText,
                LevelValue = ParseLevel(levelText),
            };
        }
        static int CompareEntries(ScoreEntry x, ScoreEntry y)
        {
            var result = y.LevelValue.CompareTo(x.LevelValue);
            if (result != 0)
            {
                return result;
            }
            result = y.Level.CompareTo(x.Level);
            if (result != 0)
            {
                return result;
            }
            result = y.Score.Acc.DX.CompareTo(x.Score.Acc.DX);
            if (result != 0)
            {
                return result;
            }
            result = y.Score.DXScore.CompareTo(x.Score.DXScore);
            if (result != 0)
            {
                return result;
            }
            return string.Compare(x.Title, y.Title, StringComparison.Ordinal);
        }
        /// <summary>
        /// 等级文字转数值：小数按原值，“13+”按 13.7，无法识别（如“?”）为 -1
        /// </summary>
        static float ParseLevel(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return -1;
            }
            text = text.Trim();
            var isPlus = text.EndsWith("+", StringComparison.Ordinal);
            if (isPlus)
            {
                text = text.Substring(0, text.Length - 1);
            }
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return -1;
            }
            return isPlus ? MathF.Floor(value) + 0.7f : value;
        }
        static string GetGrade(double achievement)
        {
            return achievement switch
            {
                >= 100.5 => "SSS+",
                >= 100 => "SSS",
                >= 99.5 => "SS+",
                >= 99 => "SS",
                >= 98 => "S+",
                >= 97 => "S",
                >= 94 => "AAA",
                >= 90 => "AA",
                >= 80 => "A",
                >= 75 => "BBB",
                >= 70 => "BB",
                >= 60 => "B",
                >= 50 => "C",
                _ => "D",
            };
        }
        static string GetComboText(ComboState state)
        {
            return state switch
            {
                ComboState.APPlus => "AP+",
                ComboState.AP => "AP",
                ComboState.FCPlus => "FC+",
                ComboState.FC => "FC",
                _ => string.Empty,
            };
        }
        internal static string GetDifficultyName(ChartLevel level)
        {
            return level switch
            {
                ChartLevel.Easy => "EASY",
                ChartLevel.Basic => "BASIC",
                ChartLevel.Advance => "ADVANCED",
                ChartLevel.Expert => "EXPERT",
                ChartLevel.Master => "MASTER",
                ChartLevel.ReMaster => "Re:MASTER",
                _ => "UTAGE",
            };
        }
        static string BuildTitle(ScoreOptions options, bool isFalling, int count)
        {
            var store = (isFalling ? "MAJTEXT_SCORE_STORE_FALLING" : "MAJTEXT_SCORE_STORE_STANDARD").i18n();
            var scope = options.ShareScope switch
            {
                ScoreShareScopeOption.CurrentChart => "MAJTEXT_SCORE_SCOPE_CURRENT".i18n(),
                ScoreShareScopeOption.Best => "MAJTEXT_SCORE_SCOPE_BEST".i18n(count),
                _ => "MAJTEXT_SCORE_SCOPE_ALL".i18n(count),
            };
            var date = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return $"{"MAJTEXT_SCORE_TITLE".i18n()} · {store} · {scope} · {date}";
        }
        static string PrepareShareDirectory()
        {
            var directory = ShareDirectory;
            Directory.CreateDirectory(directory);
            // 只保留本次分享的文件
            foreach (var file in Directory.GetFiles(directory))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception e)
                {
                    MajDebug.LogWarning($"[ScoreExchange] Failed to delete old share file: {e.Message}");
                }
            }
            return directory;
        }
        static string BuildText(string title, List<ScoreEntry> entries)
        {
            var builder = new StringBuilder();
            builder.AppendLine(title);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                builder.Append(i + 1).Append(". ").Append(entry.Title)
                       .Append(" [").Append(entry.DifficultyName);
                if (!string.IsNullOrEmpty(entry.LevelText))
                {
                    builder.Append(' ').Append(entry.LevelText);
                }
                builder.Append("] ").Append(entry.AchievementText).Append(' ').Append(entry.GradeText);
                if (!string.IsNullOrEmpty(entry.ComboText))
                {
                    builder.Append(' ').Append(entry.ComboText);
                }
                builder.AppendLine();
            }
            return builder.ToString();
        }
        static string BuildCsv(List<ScoreEntry> entries)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Rank,Title,Artist,Difficulty,Level,Achievement,Grade,Combo,DXScore,MaxDXScore,PlayCount,LastPlayed");
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var score = entry.Score;
                var fields = new[]
                {
                    (i + 1).ToString(CultureInfo.InvariantCulture),
                    entry.Title,
                    entry.Artist,
                    entry.DifficultyName,
                    entry.LevelText,
                    entry.IsPlayed ? score.Acc.DX.ToString("0.0000", CultureInfo.InvariantCulture) : string.Empty,
                    entry.IsPlayed ? GetGrade(score.Acc.DX) : string.Empty,
                    entry.ComboText,
                    score.DXScore.ToString(CultureInfo.InvariantCulture),
                    score.TotalDXScore.ToString(CultureInfo.InvariantCulture),
                    score.PlayCount.ToString(CultureInfo.InvariantCulture),
                    entry.IsPlayed ? score.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : string.Empty,
                };
                builder.AppendLine(string.Join(",", fields.Select(EscapeCsv)));
            }
            return builder.ToString();
        }
        static string EscapeCsv(string value)
        {
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
            {
                return value;
            }
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        static string BuildJson(string title, List<ScoreEntry> entries, bool isFalling)
        {
            var array = new JArray();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var score = entry.Score;
                array.Add(new JObject
                {
                    ["rank"] = i + 1,
                    ["title"] = entry.Title,
                    ["artist"] = entry.Artist,
                    ["difficulty"] = entry.DifficultyName,
                    ["level"] = entry.LevelText,
                    ["achievement"] = entry.IsPlayed ? new JValue(score.Acc.DX) : JValue.CreateNull(),
                    ["grade"] = entry.IsPlayed ? GetGrade(score.Acc.DX) : null,
                    ["combo"] = entry.ComboText,
                    ["dxScore"] = score.DXScore,
                    ["maxDxScore"] = score.TotalDXScore,
                    ["playCount"] = score.PlayCount,
                    ["lastPlayed"] = entry.IsPlayed ? score.Timestamp.ToString("o", CultureInfo.InvariantCulture) : null,
                    ["hash"] = entry.SongHash,
                });
            }
            var root = new JObject
            {
                ["title"] = title,
                ["playStyle"] = isFalling ? "Falling" : "Standard",
                ["scores"] = array,
            };
            return root.ToString(Formatting.Indented);
        }
    }
}
