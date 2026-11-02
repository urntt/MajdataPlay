using Cysharp.Threading.Tasks;
using MajdataPlay.Collections;
using MajdataPlay.Diagnostics;
using MajdataPlay.Json;
using MajdataPlay.Net;
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
using UnityEngine;
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

        /// <summary>
        /// 完成或失败后过这么久回到 Ready，设置里重新显示“按 + …”
        /// </summary>
        const float STATE_RESET_DELAY_SEC = 3f;

        public static ScoreActionState ShareState
        {
            get => _shareState;
            private set => SetState(ref _shareState, ref _shareVersion, value, () => _shareVersion, () => _shareState = ScoreActionState.Ready);
        }
        public static ScoreActionState ExportState
        {
            get => _exportState;
            private set => SetState(ref _exportState, ref _exportVersion, value, () => _exportVersion, () => _exportState = ScoreActionState.Ready);
        }
        public static ScoreActionState ImportState
        {
            get => _importState;
            private set => SetState(ref _importState, ref _importVersion, value, () => _importVersion, () => _importState = ScoreActionState.Ready);
        }
        static ScoreActionState _shareState = ScoreActionState.Ready;
        static ScoreActionState _exportState = ScoreActionState.Ready;
        static ScoreActionState _importState = ScoreActionState.Ready;
        static int _shareVersion = 0;
        static int _exportVersion = 0;
        static int _importVersion = 0;
        /// <summary>
        /// 备份文件夹：iOS 可在“文件”App 中看到，Android 在 /sdcard/Documents/MajdataPlay 下
        /// </summary>
        public static string BackupDirectory => MajEnv.RootDirectory.CreateDirectory("ScoreBackups").Location;
        /// <summary>
        /// 分享用的临时文件：放在本地缓存目录（始终是本地路径，系统分享需要文件路径）
        /// </summary>
        static string ShareDirectory => Path.Combine(MajEnv.TempDirectory.Location, "ScoreShares");

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
            /// <summary>
            /// 这个难度的谱师
            /// </summary>
            public string Designer
            {
                get
                {
                    if (Song is null)
                    {
                        return string.Empty;
                    }
                    var designers = Song.Designers;
                    var index = (int)Level;
                    return index < designers.Length ? designers[index] ?? string.Empty : string.Empty;
                }
            }
            public string DifficultyName => GetDifficultyName(Level);
            public string AchievementText => IsPlayed ? Score.Acc.DX.ToString("0.0000", CultureInfo.InvariantCulture) + "%" : "—";
            public string GradeText => IsPlayed ? GetGrade(Score.Acc.DX) : "MAJTEXT_SCORE_UNPLAYED".i18n();
            public string ComboText => GetComboText(Score.ComboState);
            public string DxScoreText => $"DX {Score.DXScore.ToString(CultureInfo.InvariantCulture)}";
            public string PlayCountText => $"{Score.PlayCount.ToString(CultureInfo.InvariantCulture)} PC";
        }
        /// <summary>
        /// 分享内容的头部信息：玩家、范围标签（“Best 50”“All 120”，当前谱面为空）与日期，图片和三种文本格式共用
        /// </summary>
        internal sealed class ShareHeader
        {
            public required string PlayerName { get; init; }
            /// <summary>
            /// 已登录账号的头像；未登录或没有头像时为空，图片上画一个带首字母的圆
            /// </summary>
            public Sprite? Avatar { get; init; }
            public required string Label { get; init; }
            public required string Date { get; init; }
            /// <summary>
            /// 分享文件名（不含扩展名），如“昵称-Best50-2026-11-01”
            /// </summary>
            public required string FileName { get; init; }
            /// <summary>
            /// 文字分享的第一行与系统分享的标题：“昵称 · Best 50 · 日期”
            /// </summary>
            public string Caption => string.Join(" · ", new[] { PlayerName, Label, Date }.Where(x => !string.IsNullOrEmpty(x)));
        }

        /// <summary>
        /// 记下新状态；完成或失败时在 <see cref="STATE_RESET_DELAY_SEC"/> 秒后回到 Ready（期间又有新操作则不回）
        /// </summary>
        static void SetState(ref ScoreActionState state, ref int version, ScoreActionState value, Func<int> getVersion, Action reset)
        {
            state = value;
            var current = ++version;
            if (value is ScoreActionState.Done or ScoreActionState.Failed)
            {
                ResetStateLaterAsync(current, getVersion, reset).Forget();
            }
        }
        static async UniTaskVoid ResetStateLaterAsync(int version, Func<int> getVersion, Action reset)
        {
            await UniTask.SwitchToMainThread();
            await UniTask.Delay(TimeSpan.FromSeconds(STATE_RESET_DELAY_SEC), ignoreTimeScale: true);
            if (getVersion() == version)
            {
                reset();
            }
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
        /// <summary>
        /// 列表式选歌的谱面详情：分享或导出这一张谱的成绩（按当前游玩模式的分区）
        /// </summary>
        public static void StartShareChart(ISongDetail song, ChartLevel level, ScoreShareFormatOption format)
        {
            if (ShareState == ScoreActionState.Running)
            {
                return;
            }
            ShareState = ScoreActionState.Running;
            ShareChartAsync(song, level, format).Forget();
        }
        /// <summary>
        /// 新结算界面：分享本局成绩（达成率、评级、FC/AP、DX 为本局，游玩次数由调用方填好）
        /// </summary>
        public static void StartShareResult(ISongDetail song, MaiScore score, ScoreShareFormatOption format)
        {
            if (ShareState == ScoreActionState.Running)
            {
                return;
            }
            ShareState = ScoreActionState.Running;
            ShareResultAsync(song, score, format).Forget();
        }
        static async UniTaskVoid ShareResultAsync(ISongDetail song, MaiScore score, ScoreShareFormatOption format)
        {
            try
            {
                var isFalling = MajEnv.Settings.Falling.PlayStyle == PlayStyleOption.Falling;
                var entries = new List<ScoreEntry>
                {
                    CreateEntry(score, song.Hash, new Dictionary<string, ISongDetail> { [song.Hash] = song })
                };
                var header = BuildHeader(string.Empty, song.Title);
                await ShareEntriesAsync(entries, header, format, isFalling);
                ShareState = ScoreActionState.Done;
            }
            catch (Exception e)
            {
                MajDebug.LogError(e);
                ShareState = ScoreActionState.Failed;
            }
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
                var label = options.ShareScope switch
                {
                    ScoreShareScopeOption.CurrentChart => string.Empty,
                    // 不随语言变化；X 为设置里“最好的若干个”的数量
                    ScoreShareScopeOption.Best => $"Best {options.ShareBestCount}",
                    ScoreShareScopeOption.Custom => $"Selected {entries.Count}",
                    _ => $"All {entries.Count}",
                };
                var header = BuildHeader(label, options.ShareScope == ScoreShareScopeOption.CurrentChart ? entries[0].Title : null);
                await ShareEntriesAsync(entries, header, options.ShareFormat, isFalling);
                ShareState = ScoreActionState.Done;
            }
            catch (Exception e)
            {
                MajDebug.LogError(e);
                ShareState = ScoreActionState.Failed;
            }
        }
        static async UniTaskVoid ShareChartAsync(ISongDetail song, ChartLevel level, ScoreShareFormatOption format)
        {
            try
            {
                var isFalling = MajEnv.Settings.Falling.PlayStyle == PlayStyleOption.Falling;
                var score = ScoreManager.FindScore(song.Hash, level, isFalling) ?? new MaiScore()
                {
                    Hash = song.Hash,
                    ChartLevel = level,
                };
                var entries = new List<ScoreEntry>
                {
                    CreateEntry(score, song.Hash, new Dictionary<string, ISongDetail> { [song.Hash] = song })
                };
                var header = BuildHeader(string.Empty, song.Title);
                await ShareEntriesAsync(entries, header, format, isFalling);
                ShareState = ScoreActionState.Done;
            }
            catch (Exception e)
            {
                MajDebug.LogError(e);
                ShareState = ScoreActionState.Failed;
            }
        }
        static async UniTask ShareEntriesAsync(List<ScoreEntry> entries, ShareHeader header, ScoreShareFormatOption format, bool isFalling)
        {
            var directory = PrepareShareDirectory();
            var baseName = header.FileName;
            var caption = header.Caption;
            switch (format)
            {
                case ScoreShareFormatOption.Text:
                    {
                        var text = BuildText(header, entries);
                        NativeShare.ShareText(text, caption, Path.Combine(directory, baseName + ".txt"));
                    }
                    break;
                case ScoreShareFormatOption.Csv:
                    {
                        var path = Path.Combine(directory, baseName + ".csv");
                        // 带 BOM，Excel 打开时中日文不乱码
                        File.WriteAllText(path, BuildCsv(entries), new UTF8Encoding(true));
                        NativeShare.ShareFiles(new[] { path }, "text/csv", caption);
                    }
                    break;
                case ScoreShareFormatOption.Json:
                    {
                        var path = Path.Combine(directory, baseName + ".json");
                        File.WriteAllText(path, BuildJson(header, entries, isFalling), new UTF8Encoding(false));
                        NativeShare.ShareFiles(new[] { path }, "application/json", caption);
                    }
                    break;
                default:
                    {
                        var paths = await ScoreImageRenderer.RenderAsync(entries, header, directory, baseName);
                        NativeShare.ShareFiles(paths, "image/png", caption);
                    }
                    break;
            }
        }
        /// <param name="label">“Best 50”“All 120”，当前谱面为空</param>
        /// <param name="songTitle">分享单张谱面时用于文件名的曲名</param>
        static ShareHeader BuildHeader(string label, string? songTitle)
        {
            var profile = PlayerProfile.GetCurrent();
            var playerName = profile.Name;
            var avatar = profile.Avatar;
            var date = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var middle = string.IsNullOrEmpty(label) ? (songTitle ?? "Scores") : label.Replace(" ", string.Empty);
            return new ShareHeader()
            {
                PlayerName = playerName,
                Avatar = avatar,
                Label = label,
                Date = date,
                FileName = SanitizeFileName($"{playerName}-{middle}-{date}"),
            };
        }
        static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                // 另外去掉各平台分享时可能出问题的字符
                builder.Append(Array.IndexOf(invalid, c) >= 0 || c is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|' || char.IsControl(c) ? '_' : c);
            }
            var result = builder.ToString().Trim().Trim('.');
            if (result.Length > 80)
            {
                result = result.Substring(0, 80);
            }
            return string.IsNullOrEmpty(result) ? "MajdataPlay-Scores" : result;
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
                var hash = SongStorage.FindSelectedSong()?.Hash;
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
            if (options.ShareScope == ScoreShareScopeOption.Custom)
            {
                return CollectSelectionEntries(options, isFalling, songs);
            }
            foreach (var score in ScoreManager.GetAllScores(isFalling))
            {
                entries.Add(CreateEntry(score, ScoreManager.GetSongHash(score), songs));
            }
            entries.Sort(GetComparison(options.ShareSort));
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
        /// <summary>
        /// 按“分享排序”依次比较难度（等级数值，相同时按难度类别）、达成率、DX 分，都从大到小；都相同时按曲名
        /// </summary>
        static Comparison<ScoreEntry> GetComparison(ScoreShareSortOption sort)
        {
            Comparison<ScoreEntry>[] keys = sort switch
            {
                ScoreShareSortOption.LevelDxAchievement => new Comparison<ScoreEntry>[] { CompareLevel, CompareDxScore, CompareAchievement },
                ScoreShareSortOption.AchievementLevelDx => new Comparison<ScoreEntry>[] { CompareAchievement, CompareLevel, CompareDxScore },
                ScoreShareSortOption.AchievementDxLevel => new Comparison<ScoreEntry>[] { CompareAchievement, CompareDxScore, CompareLevel },
                ScoreShareSortOption.DxLevelAchievement => new Comparison<ScoreEntry>[] { CompareDxScore, CompareLevel, CompareAchievement },
                ScoreShareSortOption.DxAchievementLevel => new Comparison<ScoreEntry>[] { CompareDxScore, CompareAchievement, CompareLevel },
                _ => new Comparison<ScoreEntry>[] { CompareLevel, CompareAchievement, CompareDxScore },
            };
            return (x, y) =>
            {
                foreach (var key in keys)
                {
                    var result = key(x, y);
                    if (result != 0)
                    {
                        return result;
                    }
                }
                return string.Compare(x.Title, y.Title, StringComparison.Ordinal);
            };
        }
        static int CompareLevel(ScoreEntry x, ScoreEntry y)
        {
            var result = y.LevelValue.CompareTo(x.LevelValue);
            return result != 0 ? result : y.Level.CompareTo(x.Level);
        }
        static int CompareAchievement(ScoreEntry x, ScoreEntry y)
        {
            return y.Score.Acc.DX.CompareTo(x.Score.Acc.DX);
        }
        static int CompareDxScore(ScoreEntry x, ScoreEntry y)
        {
            return y.Score.DXScore.CompareTo(x.Score.DXScore);
        }

        #region Selection
        /// <summary>
        /// 自选谱面：列表式选歌里勾选，保存在 ScoreOptions.ShareSelection（每项为“哈希|难度”，按勾选顺序）
        /// </summary>
        static List<string> Selection => MajEnv.Settings.Score.ShareSelection ??= new List<string>();
        static string GetSelectionKey(string hash, ChartLevel level) => $"{hash}|{level}";
        public static int SelectionCount => Selection.Count;
        public static bool IsSelected(string hash, ChartLevel level)
        {
            return !string.IsNullOrEmpty(hash) && Selection.Contains(GetSelectionKey(hash, level));
        }
        /// <summary>
        /// 勾选或取消一张谱面的一个难度；返回勾选后的状态
        /// </summary>
        public static bool ToggleSelection(string hash, ChartLevel level)
        {
            if (string.IsNullOrEmpty(hash))
            {
                return false;
            }
            var key = GetSelectionKey(hash, level);
            if (Selection.Remove(key))
            {
                return false;
            }
            Selection.Add(key);
            return true;
        }
        public static void ClearSelection()
        {
            Selection.Clear();
        }
        /// <summary>
        /// 按已选列表取成绩（没玩过的也列出），再按“自选排序”排列
        /// </summary>
        static List<ScoreEntry> CollectSelectionEntries(ScoreOptions options, bool isFalling, Dictionary<string, ISongDetail> songs)
        {
            var entries = new List<ScoreEntry>();
            foreach (var item in Selection)
            {
                var separator = item.LastIndexOf('|');
                if (separator <= 0 || !Enum.TryParse<ChartLevel>(item.Substring(separator + 1), out var level))
                {
                    continue;
                }
                var hash = item.Substring(0, separator);
                var score = ScoreManager.FindScore(hash, level, isFalling) ?? new MaiScore()
                {
                    Hash = hash,
                    ChartLevel = level,
                };
                entries.Add(CreateEntry(score, hash, songs));
            }
            if (options.ShareCustomSort == ScoreShareCustomSortOption.FollowSort)
            {
                // List.Sort 不稳定：先记下勾选顺序作为最后的比较
                var order = new Dictionary<ScoreEntry, int>();
                for (var i = 0; i < entries.Count; i++)
                {
                    order[entries[i]] = i;
                }
                var comparison = GetComparison(options.ShareSort);
                entries.Sort((x, y) =>
                {
                    var result = comparison(x, y);
                    return result != 0 ? result : order[x].CompareTo(order[y]);
                });
            }
            return entries;
        }
        /// <summary>
        /// 列表式选歌的“分享”：按设置里的“自选排序”分享已选谱面
        /// </summary>
        public static void StartShareSelection(ScoreShareFormatOption format)
        {
            if (ShareState == ScoreActionState.Running)
            {
                return;
            }
            ShareState = ScoreActionState.Running;
            ShareSelectionAsync(format).Forget();
        }
        static async UniTaskVoid ShareSelectionAsync(ScoreShareFormatOption format)
        {
            try
            {
                var options = MajEnv.Settings.Score;
                var isFalling = IsFallingStore(options);
                var entries = CollectSelectionEntries(options, isFalling, BuildSongLookup());
                if (entries.Count == 0)
                {
                    ShareState = ScoreActionState.Failed;
                    return;
                }
                var header = BuildHeader($"Selected {entries.Count}", null);
                await ShareEntriesAsync(entries, header, format, isFalling);
                ShareState = ScoreActionState.Done;
            }
            catch (Exception e)
            {
                MajDebug.LogError(e);
                ShareState = ScoreActionState.Failed;
            }
        }
        #endregion
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
        static string BuildText(ShareHeader header, List<ScoreEntry> entries)
        {
            var builder = new StringBuilder();
            builder.AppendLine(header.Caption);
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
                builder.Append(' ').Append(entry.DxScoreText).Append(' ').Append(entry.PlayCountText);
                builder.AppendLine();
            }
            return builder.ToString();
        }
        static string BuildCsv(List<ScoreEntry> entries)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Rank,Title,Artist,Designer,Difficulty,Level,Achievement,Grade,FC/AP,DX,DX Max,PC,Last Played");
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var score = entry.Score;
                var fields = new[]
                {
                    (i + 1).ToString(CultureInfo.InvariantCulture),
                    entry.Title,
                    entry.Artist,
                    entry.Designer,
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
        static string BuildJson(ShareHeader header, List<ScoreEntry> entries, bool isFalling)
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
                    ["designer"] = entry.Designer,
                    ["difficulty"] = entry.DifficultyName,
                    ["level"] = entry.LevelText,
                    ["achievement"] = entry.IsPlayed ? new JValue(score.Acc.DX) : JValue.CreateNull(),
                    ["grade"] = entry.IsPlayed ? GetGrade(score.Acc.DX) : null,
                    ["combo"] = entry.ComboText,
                    ["dxScore"] = score.DXScore,
                    ["maxDxScore"] = score.TotalDXScore,
                    ["pc"] = score.PlayCount,
                    ["lastPlayed"] = entry.IsPlayed ? score.Timestamp.ToString("o", CultureInfo.InvariantCulture) : null,
                    ["hash"] = entry.SongHash,
                });
            }
            var root = new JObject
            {
                ["player"] = header.PlayerName,
                ["label"] = header.Label,
                ["date"] = header.Date,
                ["playStyle"] = isFalling ? "Falling" : "Standard",
                ["scores"] = array,
            };
            return root.ToString(Formatting.Indented);
        }
    }
}
