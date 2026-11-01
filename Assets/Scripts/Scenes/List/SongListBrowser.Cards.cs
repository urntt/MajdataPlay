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
            public MaidataAnalyzeResult? ChartInfo { get; set; }
        }

        CardView CreateCard(ISongDetail song, ChartLevel level, bool isSelected, CancellationToken token)
        {
            var card = CreatePanel(_listContent, isSelected ? ACCENT_COLOR : LINE_COLOR, 16f);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = CARD_HEIGHT;
            var inner = CreatePanel(card.transform, PAPER_COLOR, 15f);
            Stretch(inner.rectTransform, isSelected ? 2.5f : 1.5f);
            // 点在卡片空白处时由它接收，再交给卡片上的 Button
            inner.raycastTarget = true;
            var cardButton = card.gameObject.AddComponent<Button>();
            cardButton.targetGraphic = inner;
            SetButtonColors(cardButton);
            cardButton.onClick.AddListener(() => OpenDetail(song, null, true));
            var root = inner.rectTransform;

            var cover = CreateImage(root, LINE_COLOR, 12f);
            cover.preserveAspect = true;
            PlaceMiddleLeft(cover.rectTransform, 15f, COVER_SIZE, COVER_SIZE);
            LoadCoverAsync(song, cover, true, token).Forget();

            const float LEFT = 15f + COVER_SIZE + 24f;
            const float RIGHT = CARD_RIGHT_WIDTH + 28f;
            var title = CreateText(root, song.Title, 28f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            PlaceRow(title.rectTransform, LEFT, 12f, RIGHT, 44f);
            AddMarquee(title);
            var credits = CreateText(root, string.Empty, 18f, MUTED_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            PlaceRow(credits.rectTransform, LEFT, 56f, RIGHT, 29f);
            var info = CreateText(root, string.Empty, 17f, MUTED_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            PlaceRow(info.rectTransform, LEFT, 86f, RIGHT, 27f);
            // 难度：每个难度一列，上面是标签，下面是这个难度的达成率
            var levels = CreateRect("Levels", root);
            PlaceRow(levels, LEFT, 120f, RIGHT, LEVEL_TAG_HEIGHT + LEVEL_SCORE_HEIGHT + 4f);
            var levelLayout = levels.gameObject.AddComponent<HorizontalLayoutGroup>();
            levelLayout.spacing = 8f;
            levelLayout.childAlignment = TextAnchor.UpperLeft;
            levelLayout.childControlWidth = true;
            levelLayout.childControlHeight = true;
            levelLayout.childForceExpandWidth = false;
            levelLayout.childForceExpandHeight = true;

            // 右侧：爱心收藏、成绩
            var right = CreateRect("Right", root);
            right.anchorMin = new Vector2(1f, 0f);
            right.anchorMax = new Vector2(1f, 1f);
            right.pivot = new Vector2(1f, 0.5f);
            right.offsetMin = new Vector2(-CARD_RIGHT_WIDTH - 18f, 0f);
            right.offsetMax = new Vector2(-18f, 0f);
            var heart = CreateHeartButton(right, song, 54f, PAPER_COLOR, out var heartRect);
            PlaceTopRight(heartRect, -6f, 10f, 54f, 54f);
            var scoreArea = CreateRect("Score", right);
            PlaceRow(scoreArea, 0f, 70f, 0f, 126f);

            var view = new CardView()
            {
                Song = song,
                Heart = heart,
                Credits = credits,
                Info = info,
                Levels = levels,
                ScoreArea = scoreArea,
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
            BuildCardScore(card.ScoreArea, score);

            card.ChartInfo = _chartInfoCache.TryGetValue((song.Hash, level), out var info) ? info : null;
            RefreshCardInfo(card);
        }
        /// <summary>
        /// 卡片上的一个难度：全称加等级的标签，下方小字是这个难度的达成率（按评级着色，没玩过为灰色“—”）
        /// </summary>
        void CreateLevelColumn(RectTransform parent, ISongDetail song, ChartLevel level, string levelText, bool isCurrent)
        {
            var column = CreateRect("Level", parent);
            var columnElement = column.gameObject.AddComponent<LayoutElement>();
            columnElement.minWidth = LEVEL_TAG_MIN_WIDTH;
            columnElement.preferredWidth = LEVEL_TAG_PREFERRED_WIDTH;
            var columnLayout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            columnLayout.spacing = 4f;
            columnLayout.childAlignment = TextAnchor.UpperCenter;
            columnLayout.childControlWidth = true;
            columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = true;
            columnLayout.childForceExpandHeight = false;

            var tag = CreateLevelTag(column, level, $"{ScoreExchange.GetDifficultyName(level)} {levelText}", isCurrent, 15f, () => SetGlobalLevel(level));
            tag.gameObject.AddComponent<LayoutElement>().preferredHeight = LEVEL_TAG_HEIGHT;
            var label = tag.GetComponentInChildren<TextMeshProUGUI>();
            label.enableAutoSizing = true;
            label.fontSizeMin = 11f;
            label.fontSizeMax = 15f;

            var score = ScoreManager.GetScore(song, level);
            var isPlayed = score is not null && score.PlayCount > 0;
            var achievementText = isPlayed ? FormatAchievement(score!.Acc.DX) : "—";
            var achievementColor = isPlayed ? GetGradeColor(GetGrade(score!.Acc.DX)) : MUTED_COLOR;
            var achievement = CreateText(column, achievementText, 14f, achievementColor, TextAlignmentOptions.Center, isPlayed ? FontStyles.Bold : FontStyles.Normal);
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
        RectTransform CreateRightAlignedRow(RectTransform parent, float top, float height)
        {
            var row = CreateRect("Row", parent);
            PlaceRow(row, 0f, top, 0f, height);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleRight;
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
                    if (!HasLevel(song, level))
                    {
                        continue;
                    }
                    var key = (song.Hash, level);
                    if (!_chartInfoCache.TryGetValue(key, out var info))
                    {
                        try
                        {
                            var simaiFile = await song.GetMaidataAsync(false, token: token);
                            await UniTask.SwitchToMainThread();
                            token.ThrowIfCancellationRequested();
                            info = ChartAnalyzer.AnalyzeMaidata(simaiFile.Charts[(int)level]);
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
        static async UniTask<MajNetSongInteract?> GetInteractAsync(OnlineSongDetail song, CancellationToken token)
        {
            try
            {
                return await Online.GetChartInteractAsync(song, token);
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
