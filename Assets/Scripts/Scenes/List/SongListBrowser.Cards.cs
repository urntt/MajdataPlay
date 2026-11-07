using Cysharp.Threading.Tasks;
using MajdataPlay.Diagnostics;
using MajdataPlay.Net;
using MajdataPlay.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#nullable enable
namespace MajdataPlay.Scenes.List
{
    internal sealed partial class SongListBrowser
    {
        /// <summary>
        /// 列表里的一张曲目卡片：切换难度时只更新曲与谱、信息行、难度标签和成绩，曲绘与曲名不动
        /// </summary>
        sealed class CardView
        {
            public required ISongDetail Song { get; init; }
            public required Image Heart { get; init; }
            public required TextMeshProUGUI Credits { get; init; }
            public required TextMeshProUGUI Info { get; init; }
            public required RectTransform Levels { get; init; }
            public required RectTransform ScoreArea { get; init; }
            /// <summary>
            /// Gets a value indicating whether the score is a single line in the text column (compact cards).
            /// </summary>
            public required bool IsScoreInline { get; init; }
            public MaidataAnalyzeResult? ChartInfo { get; set; }
        }

        CardView CreateCard(ISongDetail song, ChartLevel level, bool isSelected, CancellationToken token)
        {
            // 尺寸按当前排版取：普通、矮屏（横屏手机）、紧凑（竖屏手机，两行）
            var metrics = CurrentCardMetrics;
            // 白底卡片加一圈等宽描边（选中时为主题色、稍粗）
            var card = CreatePanel(_listContent, PAPER_COLOR, 16f);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = metrics.Height;
            LightUi.AddRing(card.transform, isSelected ? ACCENT_COLOR : LINE_COLOR, 16f, isSelected ? 2.5f : 1.5f);
            // 点在卡片空白处时由它接收，再交给卡片上的 Button
            card.raycastTarget = true;
            var cardButton = card.gameObject.AddComponent<Button>();
            cardButton.targetGraphic = card;
            SetButtonColors(cardButton);
            cardButton.onClick.AddListener(() => OpenDetail(song, null, true));
            var root = card.rectTransform;

            var cover = LightUi.CreateRoundedCover(root, 14f, PAPER_COLOR, out var coverFrame);
            PlaceTopLeft(coverFrame, metrics.CoverLeft, metrics.CoverTop, metrics.CoverSize, metrics.CoverSize);
            LoadCoverAsync(song, cover, true, token).Forget();

            var title = CreateText(root, song.Title, metrics.TitleFontSize, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            PlaceRow(title.rectTransform, metrics.TextLeft, metrics.TitleTop, metrics.TextRight, metrics.TitleHeight);
            AddMarquee(title);
            var credits = CreateText(root, string.Empty, metrics.CreditsFontSize, MUTED_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            PlaceRow(credits.rectTransform, metrics.TextLeft, metrics.CreditsTop, metrics.TextRight, metrics.CreditsHeight);
            var info = CreateText(root, string.Empty, metrics.InfoFontSize, MUTED_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            PlaceRow(info.rectTransform, metrics.TextLeft, metrics.InfoTop, metrics.TextRight, metrics.InfoHeight);
            // 难度：每个难度一列，上面是标签，下面是这个难度的达成率；紧凑排版时横跨整张卡片
            var levels = CreateRect("Levels", root);
            PlaceRow(levels, metrics.LevelsLeft, metrics.LevelsTop, metrics.LevelsRight, LEVEL_TAG_HEIGHT + LEVEL_SCORE_HEIGHT + 4f);
            var levelLayout = levels.gameObject.AddComponent<HorizontalLayoutGroup>();
            levelLayout.spacing = 8f;
            levelLayout.childAlignment = TextAnchor.UpperLeft;
            levelLayout.childControlWidth = true;
            levelLayout.childControlHeight = true;
            levelLayout.childForceExpandWidth = false;
            levelLayout.childForceExpandHeight = true;

            Image heart;
            RectTransform scoreArea;
            if (metrics.IsScoreInline)
            {
                // 紧凑：爱心在右上角，成绩是信息行下面的一行
                heart = CreateHeartButton(root, song, metrics.HeartSize, PAPER_COLOR, out var heartRect);
                PlaceTopRight(heartRect, 10f, metrics.HeartTop, metrics.HeartSize, metrics.HeartSize);
                scoreArea = CreateRect("Score", root);
                PlaceRow(scoreArea, metrics.TextLeft, metrics.ScoreTop, 16f, INLINE_SCORE_HEIGHT);
            }
            else
            {
                // 右侧：爱心收藏、成绩
                var right = CreateRect("Right", root);
                right.anchorMin = new Vector2(1f, 0f);
                right.anchorMax = new Vector2(1f, 1f);
                right.pivot = new Vector2(1f, 0.5f);
                right.offsetMin = new Vector2(-CARD_RIGHT_WIDTH - 18f, 0f);
                right.offsetMax = new Vector2(-18f, 0f);
                heart = CreateHeartButton(right, song, metrics.HeartSize, PAPER_COLOR, out var heartRect);
                PlaceTopRight(heartRect, -6f, metrics.HeartTop, metrics.HeartSize, metrics.HeartSize);
                scoreArea = CreateRect("Score", right);
                PlaceRow(scoreArea, 0f, metrics.ScoreTop, 0f, 126f);
            }

            var view = new CardView()
            {
                Song = song,
                Heart = heart,
                Credits = credits,
                Info = info,
                Levels = levels,
                ScoreArea = scoreArea,
                IsScoreInline = metrics.IsScoreInline,
            };
            UpdateCardLevel(view, level);
            return view;
        }
        void UpdateCardLevel(CardView card, ChartLevel level)
        {
            var song = card.Song;
            card.Credits.text = BuildCredits(song, level);

            ClearChildren(card.Levels);
            var songLevels = song.Levels;
            for (var i = 0; i < songLevels.Length && i < LEVEL_SHORT_NAMES.Length; i++)
            {
                if (string.IsNullOrEmpty(songLevels[i]))
                {
                    continue;
                }
                var target = (ChartLevel)i;
                CreateLevelColumn(card.Levels, song, target, songLevels[i], target == level);
            }

            ClearChildren(card.ScoreArea);
            var score = HasLevel(song, level) ? ScoreManager.GetScore(song, level) : null;
            if (card.IsScoreInline)
            {
                BuildInlineCardScore(card.ScoreArea, score);
            }
            else
            {
                BuildCardScore(card.ScoreArea, score);
            }

            var infoLevel = HasLevel(song, level) ? level : GetHighestLevel(song);
            card.ChartInfo = _chartInfoCache.TryGetValue((song.Hash, infoLevel), out var info) ? info : null;
            RefreshCardInfo(card);
        }
        /// <summary>
        /// 卡片上的一个难度：全称加等级的标签，下方小字是这个难度的达成率（按评级着色，没玩过为灰色“—”）
        /// </summary>
        void CreateLevelColumn(RectTransform parent, ISongDetail song, ChartLevel level, string levelText, bool isCurrent)
        {
            var metrics = CurrentCardMetrics;
            var column = CreateRect("Level", parent);
            var columnElement = column.gameObject.AddComponent<LayoutElement>();
            columnElement.minWidth = metrics.LevelMinWidth;
            columnElement.preferredWidth = LEVEL_TAG_PREFERRED_WIDTH;
            var columnLayout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            columnLayout.spacing = 4f;
            columnLayout.childAlignment = TextAnchor.UpperCenter;
            columnLayout.childControlWidth = true;
            columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = true;
            columnLayout.childForceExpandHeight = false;

            // 只在勾选模式下标出自选：已选的难度色实底、白色粗体字并带“−”，未选的带“+”；平时标签保持原样
            var isPicked = _isSelecting && ScoreExchange.IsSelected(song.Hash, level);
            var tag = CreateLevelTag(column, level, $"{ScoreExchange.GetDifficultyName(level)} {levelText}", isCurrent, 15f,
                                     () => OnLevelTagClicked(song, level), isPicked, _isSelecting);
            tag.gameObject.AddComponent<LayoutElement>().preferredHeight = LEVEL_TAG_HEIGHT;
            var label = tag.GetComponentInChildren<TextMeshProUGUI>();
            label.enableAutoSizing = true;
            label.fontSizeMin = metrics.LevelFontSizeMin;
            label.fontSizeMax = 15f;

            AddLevelAchievement(column, song, level, 14f);
        }
        /// <summary>
        /// Adds the achievement of one difficulty under its tag: colored by grade, or a grey "—" when it was never played.
        /// </summary>
        /// <param name="column">The vertical layout column holding the tag.</param>
        /// <param name="song">The song.</param>
        /// <param name="level">The difficulty.</param>
        /// <param name="fontSize">The font size of the achievement.</param>
        void AddLevelAchievement(RectTransform column, ISongDetail song, ChartLevel level, float fontSize)
        {
            var score = ScoreManager.GetScore(song, level);
            var isPlayed = score is not null && score.PlayCount > 0;
            var achievementText = isPlayed ? FormatAchievement(score!.Acc.DX) : "—";
            var achievementColor = isPlayed ? GetGradeColor(GetGrade(score!.Acc.DX)) : MUTED_COLOR;
            var achievement = CreateText(column, achievementText, fontSize, achievementColor, TextAlignmentOptions.Center, isPlayed ? FontStyles.Bold : FontStyles.Normal);
            achievement.gameObject.AddComponent<LayoutElement>().preferredHeight = LEVEL_SCORE_HEIGHT;
        }
        string BuildCredits(ISongDetail song, ChartLevel level)
        {
            var artist = string.IsNullOrWhiteSpace(song.Artist) ? "—" : song.Artist;
            return $"{"MAJTEXT_LIST_LABEL_ARTIST".i18n()} {artist}    {"MAJTEXT_LIST_LABEL_DESIGNER".i18n()} {GetDesigner(song, level)}";
        }
        /// <summary>
        /// 卡片右侧的成绩：评级徽章与达成率；DX 分与 FC/AP；“X PC · 最近日期”。没玩过时显示“未游玩”
        /// </summary>
        void BuildCardScore(RectTransform parent, MaiScore? score)
        {
            if (score is null || score.PlayCount <= 0)
            {
                var unplayed = CreateText(parent, "MAJTEXT_SCORE_UNPLAYED".i18n(), 20f, MUTED_COLOR, TextAlignmentOptions.MidlineRight, FontStyles.Normal);
                PlaceRow(unplayed.rectTransform, 0f, 0f, 0f, 46f);
                return;
            }
            var achievement = score.Acc.DX;
            var grade = GetGrade(achievement);
            var top = CreateRightAlignedRow(parent, 0f, 46f);
            AddBadge(top, grade, GetGradeColor(grade), 18f);
            AddFittedText(top, FormatAchievement(achievement), 28f, TEXT_COLOR, FontStyles.Bold);

            var middle = CreateRightAlignedRow(parent, 50f, 34f);
            AddFittedText(middle, $"DX {score.DXScore.ToString(CultureInfo.InvariantCulture)}", 18f, MUTED_COLOR, FontStyles.Normal);
            var combo = GetComboText(score.ComboState);
            if (!string.IsNullOrEmpty(combo))
            {
                AddBadge(middle, combo, GetComboColor(score.ComboState), 16f);
            }

            var footer = CreateText(parent, FormatPlayStats(score), 16f, MUTED_COLOR, TextAlignmentOptions.MidlineRight, FontStyles.Normal);
            PlaceRow(footer.rectTransform, 0f, 90f, 0f, 28f);
        }
        /// <summary>
        /// The height of the one-line score on compact cards.
        /// </summary>
        const float INLINE_SCORE_HEIGHT = 36f;
        /// <summary>
        /// Builds the one-line score of a compact card: grade, achievement and FC/AP, or "Unplayed".
        /// </summary>
        /// <param name="parent">The score area in the text column.</param>
        /// <param name="score">The best score of the difficulty, or <see langword="null"/> when there is none.</param>
        void BuildInlineCardScore(RectTransform parent, MaiScore? score)
        {
            if (score is null || score.PlayCount <= 0)
            {
                var unplayed = CreateText(parent, "MAJTEXT_SCORE_UNPLAYED".i18n(), 16f, MUTED_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
                Stretch(unplayed.rectTransform);
                return;
            }
            var achievement = score.Acc.DX;
            var grade = GetGrade(achievement);
            var row = CreateAlignedRow(parent, 0f, INLINE_SCORE_HEIGHT, TextAnchor.MiddleLeft);
            AddBadge(row, grade, GetGradeColor(grade), 15f);
            AddFittedText(row, FormatAchievement(achievement), 22f, TEXT_COLOR, FontStyles.Bold);
            var combo = GetComboText(score.ComboState);
            if (!string.IsNullOrEmpty(combo))
            {
                AddBadge(row, combo, GetComboColor(score.ComboState), 14f);
            }
        }
        RectTransform CreateRightAlignedRow(RectTransform parent, float top, float height)
        {
            return CreateAlignedRow(parent, top, height, TextAnchor.MiddleRight);
        }
        /// <summary>
        /// Creates a full-width row whose children are laid out side by side at their preferred widths.
        /// </summary>
        /// <param name="parent">The parent of the row.</param>
        /// <param name="top">The distance of the row from the top of the parent.</param>
        /// <param name="height">The height of the row.</param>
        /// <param name="alignment">Where the children gather in the row.</param>
        /// <returns>The row.</returns>
        RectTransform CreateAlignedRow(RectTransform parent, float top, float height, TextAnchor alignment)
        {
            var row = CreateRect("Row", parent);
            PlaceRow(row, 0f, top, 0f, height);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }
        /// <summary>
        /// 信息行：时长、BPM；网络谱面另加游玩、赞、评论（ID 只在详情页显示）
        /// </summary>
        void RefreshCardInfo(CardView card)
        {
            var parts = new List<string>();
            if (card.ChartInfo is MaidataAnalyzeResult info && info.Length > TimeSpan.Zero)
            {
                parts.Add($"{"MAJTEXT_LIST_LABEL_LENGTH".i18n()} {FormatLength(info.Length)}");
                parts.Add($"BPM {FormatBpm(info.MinBPM, info.MaxBPM)}");
            }
            if (card.Song is OnlineSongDetail && _interactCache.TryGetValue(card.Song.Hash, out var interact))
            {
                AddInteractParts(parts, interact);
            }
            card.Info.text = string.Join("  ·  ", parts);
        }
        static void AddInteractParts(List<string> parts, MajNetSongInteract interact)
        {
            parts.Add($"{"MAJTEXT_LIST_LABEL_PLAYS".i18n()} {interact.Plays.ToString(CultureInfo.InvariantCulture)}");
            parts.Add($"{"MAJTEXT_LIST_LABEL_LIKES".i18n()} {GetLikeCount(interact).ToString(CultureInfo.InvariantCulture)}");
            parts.Add($"{"MAJTEXT_LIST_LABEL_COMMENTS".i18n()} {(interact.Comments?.Length ?? 0).ToString(CultureInfo.InvariantCulture)}");
        }
        static int GetLikeCount(MajNetSongInteract interact)
        {
            return (interact.Likes?.Length ?? 0) - interact.DisLikeCount;
        }
        /// <summary>
        /// 依次读取这一页卡片的时长与 BPM（随难度变化），再取网络谱面的游玩、赞、评论数；翻页、切换分类或难度时取消
        /// </summary>
        async UniTaskVoid LoadCardInfoAsync(CardView[] cards, ChartLevel level, CancellationToken token)
        {
            try
            {
                foreach (var card in cards)
                {
                    token.ThrowIfCancellationRequested();
                    var song = card.Song;
                    // 这首歌没有全局难度时用它的最高难度，与详情页一致
                    var cardLevel = HasLevel(song, level) ? level : GetHighestLevel(song);
                    if (!HasLevel(song, cardLevel))
                    {
                        continue;
                    }
                    var key = (song.Hash, cardLevel);
                    if (!_chartInfoCache.TryGetValue(key, out var info))
                    {
                        // 在线谱面每下载一次服务器就记一次游玩：只分析已经下载过（玩过或打开过详情页）的谱面
                        if (song is OnlineSongDetail onlineSong && !onlineSong.IsMaidataStored())
                        {
                            continue;
                        }
                        try
                        {
                            var simaiFile = await song.GetMaidataAsync(false, token: token);
                            await UniTask.SwitchToMainThread();
                            token.ThrowIfCancellationRequested();
                            info = ChartAnalyzer.AnalyzeMaidata(simaiFile.Charts[(int)cardLevel]);
                            _chartInfoCache[key] = info;
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception e)
                        {
                            await UniTask.SwitchToMainThread();
                            MajDebug.LogWarning($"[SongListBrowser] Failed to analyze {song.Title}: {e.Message}");
                            continue;
                        }
                    }
                    token.ThrowIfCancellationRequested();
                    card.ChartInfo = info;
                    RefreshCardInfo(card);
                }
                foreach (var card in cards)
                {
                    token.ThrowIfCancellationRequested();
                    if (card.Song is not OnlineSongDetail onlineSong || _interactCache.ContainsKey(onlineSong.Hash))
                    {
                        continue;
                    }
                    var interact = await GetInteractAsync(onlineSong, token);
                    await UniTask.SwitchToMainThread();
                    token.ThrowIfCancellationRequested();
                    if (interact is MajNetSongInteract value)
                    {
                        _interactCache[onlineSong.Hash] = value;
                        RefreshCardInfo(card);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                await UniTask.SwitchToMainThread();
                MajDebug.LogError(e);
            }
        }
        /// <summary>
        /// Gets the plays, likes and comments of an online chart.
        /// </summary>
        /// <param name="song">The chart.</param>
        /// <param name="token">Cancels the request.</param>
        /// <param name="forceRefresh">Bypasses the cache of <see cref="Online.GetChartInteractAsync"/>.</param>
        /// <returns>The interaction data, or <see langword="null"/> when it could not be loaded.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="token"/> was canceled.</exception>
        static async UniTask<MajNetSongInteract?> GetInteractAsync(OnlineSongDetail song, CancellationToken token, bool forceRefresh = false)
        {
            try
            {
                return await Online.GetChartInteractAsync(song, token, forceRefresh);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(token);
                }
                MajDebug.LogWarning($"[SongListBrowser] Failed to get interaction of {song.Title}: {e.Message}");
                return null;
            }
        }
    }
}
