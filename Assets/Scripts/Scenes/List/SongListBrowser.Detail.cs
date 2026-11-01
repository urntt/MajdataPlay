using Cysharp.Threading.Tasks;
using LitMotion;
using MajdataPlay.Diagnostics;
using MajdataPlay.Utils;
using MajdataPlay.Utils.ChartRadar;
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
        const float DETAIL_MARGIN = 40f;
        const float DETAIL_GAP = 32f;
        const float DETAIL_TOP = 100f;
        const float INFO_ROW_HEIGHT = 46f;
        const float SCORE_CARD_HEIGHT = 156f;
        const float CHART_PANEL_HEIGHT = 400f;

        RectTransform _detailView = null!;
        RectTransform _detailViewport = null!;
        RectTransform _detailContent = null!;
        ScrollRect _detailScroll = null!;
        CanvasGroup _detailGroup = null!;
        Button _detailPlayButton = null!;
        Image? _detailHeart;

        ISongDetail? _detailSong;
        ChartLevel _detailLevel;
        Sprite? _detailCover;
        string? _detailCoverHash;
        float _lastDetailWidth = -1f;
        bool _isDetailClosing = false;
        readonly List<Texture> _createdTextures = new();

        /// <summary>
        /// 详情页内容的加载（曲绘、物量图、雷达、热度）：每次重新排版或关闭时取消
        /// </summary>
        CancellationTokenSource _detailCts = new();
        /// <summary>
        /// 预览：返回时先淡出再取消
        /// </summary>
        CancellationTokenSource _previewCts = new();
        MotionHandle _detailMotion;
        MotionHandle _detailAlphaMotion;
        MotionHandle _previewVolumeMotion;
        MotionHandle _bgmMotion;

        void BuildDetailView()
        {
            _detailView = CreateRect("DetailView", _root);
            Stretch(_detailView);
            _detailView.gameObject.AddComponent<Image>().color = PAGE_COLOR;
            _detailGroup = _detailView.gameObject.AddComponent<CanvasGroup>();
            _detailContent = CreateScroll(_detailView, 0f, 0f, 0f, 0f, out _detailScroll);
            _detailViewport = (RectTransform)_detailContent.parent;
            AddScrollbar(_detailScroll);

            // 固定在左上角的返回与右下角的游玩，不随内容滚动
            var back = CreateButton(_detailView, "<  " + "MAJTEXT_LIST_BACK".i18n(), PAPER_COLOR, TEXT_COLOR, 20f, CloseDetail);
            PlaceTopLeft((RectTransform)back.transform, DETAIL_MARGIN, 24f, 150f, 56f);

            var playShadow = CreatePanel(_detailView, new Color(0.2f, 0.25f, 0.55f, 0.18f), 24f);
            var playShadowRect = playShadow.rectTransform;
            playShadowRect.anchorMin = new Vector2(1f, 0f);
            playShadowRect.anchorMax = new Vector2(1f, 0f);
            playShadowRect.pivot = new Vector2(1f, 0f);
            playShadowRect.sizeDelta = new Vector2(252f, 88f);
            playShadowRect.anchoredPosition = new Vector2(-DETAIL_MARGIN + 6f, 30f);
            _detailPlayButton = CreateButton(_detailView, "▶  " + "MAJTEXT_LIST_PLAY".i18n(), ACCENT_COLOR, Color.white, 28f, PlaySelected);
            var playRect = (RectTransform)_detailPlayButton.transform;
            playRect.anchorMin = new Vector2(1f, 0f);
            playRect.anchorMax = new Vector2(1f, 0f);
            playRect.pivot = new Vector2(1f, 0f);
            playRect.sizeDelta = new Vector2(240f, 80f);
            playRect.anchoredPosition = new Vector2(-DETAIL_MARGIN, 36f);
            _detailPlayButton.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            _detailView.gameObject.SetActive(false);
        }
        /// <param name="level">为空时用全局难度；这首歌没有这个难度时用最高难度</param>
        /// <param name="isAnimated">从右侧滑入并淡入；玩完返回时直接显示</param>
        void OpenDetail(ISongDetail song, ChartLevel? level, bool isAnimated)
        {
            if (_detailSong is not null && !_isDetailClosing)
            {
                return;
            }
            HideModal();
            CancelDetailMotions();
            CancelAudioMotions();
            _isDetailClosing = false;
            _detailSong = song;
            var target = level ?? GlobalLevel;
            _detailLevel = HasLevel(song, target) ? target : GetHighestLevel(song);
            _detailView.gameObject.SetActive(true);
            _detailGroup.blocksRaycasts = true;
            RenderDetail(true);
            StartPreview(song);
            if (isAnimated)
            {
                _detailGroup.alpha = 0f;
                _detailView.anchoredPosition = new Vector2(DETAIL_SLIDE_DISTANCE, 0f);
                _detailAlphaMotion = LMotion.Create(0f, 1f, DETAIL_IN_SEC)
                                            .WithEase(Ease.OutQuad)
                                            .Bind(x => _detailGroup.alpha = x);
                _detailMotion = LMotion.Create(DETAIL_SLIDE_DISTANCE, 0f, DETAIL_IN_SEC)
                                       .WithEase(Ease.OutCubic)
                                       .WithOnComplete(() => _listView.gameObject.SetActive(false))
                                       .Bind(x => _detailView.anchoredPosition = new Vector2(x, 0f));
            }
            else
            {
                _detailGroup.alpha = 1f;
                _detailView.anchoredPosition = Vector2.zero;
                _listView.gameObject.SetActive(false);
            }
        }
        /// <summary>
        /// 返回列表：详情页向右滑出并淡出；预览淡出后停止，再淡入选歌背景音乐。列表的滚动位置不变
        /// </summary>
        void CloseDetail()
        {
            if (_detailSong is null || _isDetailClosing)
            {
                return;
            }
            HideModal();
            _isDetailClosing = true;
            _detailCts.Cancel();
            CancelDetailMotions();
            _listView.gameObject.SetActive(true);
            _detailGroup.blocksRaycasts = false;
            FadeOutPreviewThenBgm(_detailSong);
            _detailAlphaMotion = LMotion.Create(_detailGroup.alpha, 0f, DETAIL_OUT_SEC)
                                        .WithEase(Ease.InQuad)
                                        .Bind(x => _detailGroup.alpha = x);
            _detailMotion = LMotion.Create(_detailView.anchoredPosition.x, DETAIL_SLIDE_DISTANCE, DETAIL_OUT_SEC)
                                   .WithEase(Ease.InCubic)
                                   .WithOnComplete(FinishCloseDetail)
                                   .Bind(x => _detailView.anchoredPosition = new Vector2(x, 0f));
        }
        void FinishCloseDetail()
        {
            _isDetailClosing = false;
            _detailSong = null;
            _detailHeart = null;
            _detailCover = null;
            _detailCoverHash = null;
            _detailView.gameObject.SetActive(false);
            _detailGroup.alpha = 1f;
            _detailGroup.blocksRaycasts = true;
            _detailView.anchoredPosition = Vector2.zero;
            ClearChildren(_detailContent);
            DestroyCreatedTextures();
        }
        void CancelDetailMotions()
        {
            _detailMotion.TryCancel();
            _detailAlphaMotion.TryCancel();
        }
        void CancelAudioMotions()
        {
            _previewVolumeMotion.TryCancel();
            _bgmMotion.TryCancel();
        }
        /// <summary>
        /// 进入详情：预览从静音开始，真正播放后在 0.6 秒内淡入；选歌背景音乐的淡出沿用原来的逻辑
        /// </summary>
        void StartPreview(ISongDetail song)
        {
            _previewCts.Cancel();
            _previewCts = new CancellationTokenSource();
            var player = _previewPlayer;
            if (player == null)
            {
                return;
            }
            player.VolumeScale = 0f;
            player.PlayPreviewSound(song, 0, _previewCts.Token, true);
            FadeInPreviewAsync(player, song, _previewCts.Token).Forget();
        }
        async UniTaskVoid FadeInPreviewAsync(PreviewSoundPlayer player, ISongDetail song, CancellationToken token)
        {
            try
            {
                await UniTask.WaitUntil(() => player == null || player.IsPreviewPlaying(song), cancellationToken: token);
                if (player == null || token.IsCancellationRequested)
                {
                    return;
                }
                _previewVolumeMotion.TryCancel();
                _previewVolumeMotion = LMotion.Create(player.VolumeScale, 1f, PREVIEW_FADE_IN_SEC)
                                              .WithEase(Ease.OutQuad)
                                              .Bind(x =>
                                              {
                                                  if (player != null)
                                                  {
                                                      player.VolumeScale = x;
                                                  }
                                              });
            }
            catch (OperationCanceledException)
            {
            }
        }
        void FadeOutPreviewThenBgm(ISongDetail song)
        {
            CancelAudioMotions();
            var previewCts = _previewCts;
            var player = _previewPlayer;
            if (player == null || !player.IsPreviewPlaying(song) || player.VolumeScale <= 0f)
            {
                previewCts.Cancel();
                FadeInSelectBgm();
                return;
            }
            _previewVolumeMotion = LMotion.Create(player.VolumeScale, 0f, PREVIEW_FADE_OUT_SEC)
                                          .WithEase(Ease.OutQuad)
                                          .WithOnComplete(() =>
                                          {
                                              previewCts.Cancel();
                                              FadeInSelectBgm();
                                          })
                                          .Bind(x =>
                                          {
                                              if (player != null)
                                              {
                                                  player.VolumeScale = x;
                                              }
                                          });
        }
        void FadeInSelectBgm()
        {
            var selectSound = MajInstances.AudioManager.GetSFX("bgm_select.mp3");
            var target = MajEnv.Settings.Audio.Volume.BGM;
            _bgmMotion.TryCancel();
            _bgmMotion = LMotion.Create(selectSound.Volume, target, BGM_FADE_IN_SEC)
                                .WithEase(Ease.OutQuad)
                                .Bind(x => selectSound.Volume = x);
        }
        void SetDetailLevel(ChartLevel level)
        {
            if (level == _detailLevel || _isDetailClosing)
            {
                return;
            }
            _detailLevel = level;
            RenderDetail(false);
        }
        void RenderDetail(bool isScrollReset = false)
        {
            var song = _detailSong;
            if (song is null)
            {
                return;
            }
            _detailCts.Cancel();
            _detailCts = new CancellationTokenSource();
            var token = _detailCts.Token;
            var scrollY = _detailContent.anchoredPosition.y;
            ClearChildren(_detailContent);
            DestroyCreatedTextures();
            _detailHeart = null;
            var level = _detailLevel;
            var hasLevel = HasLevel(song, level);
            var width = _detailViewport.rect.width;
            _lastDetailWidth = width;
            if (width < 800f)
            {
                width = 800f;
            }

            var leftWidth = Mathf.Clamp((width - DETAIL_MARGIN * 2f - DETAIL_GAP) * 0.32f, 320f, 480f);
            var rightX = DETAIL_MARGIN + leftWidth + DETAIL_GAP;
            var rightWidth = Mathf.Max(360f, width - rightX - DETAIL_MARGIN);

            // 左栏：曲绘（右上角爱心）、分享与导出、信息卡
            var cover = CreateImage(_detailContent, LINE_COLOR, 16f);
            cover.preserveAspect = true;
            PlaceTopLeft(cover.rectTransform, DETAIL_MARGIN, DETAIL_TOP, leftWidth, leftWidth);
            if (_detailCover != null && _detailCoverHash == song.Hash)
            {
                SetCover(cover, _detailCover);
            }
            else
            {
                LoadDetailCoverAsync(song, cover, token).Forget();
            }
            _detailHeart = CreateHeartButton(_detailContent, song, 60f, new Color(1f, 1f, 1f, 0.92f), out var heartRect);
            PlaceTopLeft(heartRect, DETAIL_MARGIN + leftWidth - 74f, DETAIL_TOP + 14f, 60f, 60f);

            var leftY = DETAIL_TOP + leftWidth + 18f;
            var buttonWidth = (leftWidth - 12f) / 2f;
            var share = CreateButton(_detailContent, "MAJTEXT_LIST_SHARE".i18n(), PAPER_COLOR, TEXT_COLOR, 20f, () => ShowShareModal(false));
            PlaceTopLeft((RectTransform)share.transform, DETAIL_MARGIN, leftY, buttonWidth, 56f);
            var export = CreateButton(_detailContent, "MAJTEXT_LIST_EXPORT".i18n(), PAPER_COLOR, TEXT_COLOR, 20f, () => ShowShareModal(true));
            PlaceTopLeft((RectTransform)export.transform, DETAIL_MARGIN + buttonWidth + 12f, leftY, buttonWidth, 56f);
            leftY += 56f + 18f;

            var onlineSong = song as OnlineSongDetail;
            var rowLabels = new List<string>
            {
                "MAJTEXT_LIST_LABEL_ARTIST".i18n(),
                "MAJTEXT_LIST_LABEL_DESIGNER".i18n(),
                "MAJTEXT_LIST_LABEL_LENGTH".i18n(),
                "BPM",
            };
            if (onlineSong is not null)
            {
                rowLabels.Add("ID");
                rowLabels.Add("MAJTEXT_LIST_LABEL_PLAYS".i18n());
                rowLabels.Add("MAJTEXT_LIST_LABEL_LIKES".i18n());
                rowLabels.Add("MAJTEXT_LIST_LABEL_COMMENTS".i18n());
            }
            var infoHeight = rowLabels.Count * INFO_ROW_HEIGHT + 20f;
            var infoCard = CreatePanel(_detailContent, PAPER_COLOR, 16f);
            PlaceTopLeft(infoCard.rectTransform, DETAIL_MARGIN, leftY, leftWidth, infoHeight);
            var values = new TextMeshProUGUI[rowLabels.Count];
            for (var i = 0; i < rowLabels.Count; i++)
            {
                var rowTop = 10f + i * INFO_ROW_HEIGHT;
                if (i > 0)
                {
                    var line = CreateImage(infoCard.rectTransform, LINE_COLOR, 0f);
                    PlaceRow(line.rectTransform, 20f, rowTop, 20f, 1f);
                }
                var label = CreateText(infoCard.rectTransform, rowLabels[i], 17f, MUTED_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
                PlaceRow(label.rectTransform, 22f, rowTop, 22f, INFO_ROW_HEIGHT);
                values[i] = CreateText(infoCard.rectTransform, "—", 18f, TEXT_COLOR, TextAlignmentOptions.MidlineRight, FontStyles.Normal);
                PlaceRow(values[i].rectTransform, 96f, rowTop, 22f, INFO_ROW_HEIGHT);
            }
            values[0].text = string.IsNullOrWhiteSpace(song.Artist) ? "—" : song.Artist;
            values[1].text = GetDesigner(song, level);
            var lengthValue = values[2];
            var bpmValue = values[3];
            if (onlineSong is not null)
            {
                values[4].text = onlineSong.Id;
                if (_interactCache.TryGetValue(song.Hash, out var cachedInteract))
                {
                    SetInteractValues(values, cachedInteract);
                }
                else
                {
                    LoadDetailInteractAsync(onlineSong, values, token).Forget();
                }
            }
            var leftBottom = leftY + infoHeight;

            // 右栏：曲名、难度分段条、成绩卡、雷达与物量分布
            var title = CreateText(_detailContent, song.Title, 36f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            PlaceTopLeft(title.rectTransform, rightX, DETAIL_TOP - 6f, rightWidth, 56f);
            AddMarquee(title);
            var rightY = DETAIL_TOP + 62f;

            var segments = CreateRect("Levels", _detailContent);
            PlaceTopLeft(segments, rightX, rightY, rightWidth, 54f);
            var segmentLayout = segments.gameObject.AddComponent<HorizontalLayoutGroup>();
            segmentLayout.spacing = 8f;
            segmentLayout.childControlWidth = true;
            segmentLayout.childControlHeight = true;
            segmentLayout.childForceExpandWidth = true;
            segmentLayout.childForceExpandHeight = true;
            var songLevels = song.Levels;
            for (var i = 0; i < songLevels.Length && i < LEVEL_SHORT_NAMES.Length; i++)
            {
                if (string.IsNullOrEmpty(songLevels[i]))
                {
                    continue;
                }
                var target = (ChartLevel)i;
                var segment = CreateLevelTag(segments, target, $"{ScoreExchange.GetDifficultyName(target)} {songLevels[i]}", target == level, 18f, () => SetDetailLevel(target));
                segment.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            }
            rightY += 54f + 20f;

            var scoreCard = CreatePanel(_detailContent, PAPER_COLOR, 16f);
            PlaceTopLeft(scoreCard.rectTransform, rightX, rightY, rightWidth, SCORE_CARD_HEIGHT);
            BuildDetailScore(scoreCard.rectTransform, song, level, hasLevel);
            rightY += SCORE_CARD_HEIGHT + 20f;

            var radarWidth = Mathf.Floor((rightWidth - 20f) * 0.44f);
            var radarPanel = CreatePanel(_detailContent, PAPER_COLOR, 16f);
            PlaceTopLeft(radarPanel.rectTransform, rightX, rightY, radarWidth, CHART_PANEL_HEIGHT);
            var radarTitle = CreateText(radarPanel.rectTransform, "MAJTEXT_LIST_RADAR".i18n(), 18f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            PlaceRow(radarTitle.rectTransform, 22f, 16f, 22f, 30f);
            var constantText = CreateText(radarPanel.rectTransform, string.Empty, 16f, MUTED_COLOR, TextAlignmentOptions.MidlineRight, FontStyles.Normal);
            PlaceRow(constantText.rectTransform, 22f, 16f, 22f, 30f);
            var radarSize = Mathf.Max(160f, Mathf.Min(radarWidth - 180f, CHART_PANEL_HEIGHT - 150f));
            var radarRect = CreateRect("Radar", radarPanel.rectTransform);
            radarRect.anchorMin = new Vector2(0.5f, 0.5f);
            radarRect.anchorMax = new Vector2(0.5f, 0.5f);
            radarRect.pivot = new Vector2(0.5f, 0.5f);
            radarRect.sizeDelta = new Vector2(radarSize, radarSize);
            radarRect.anchoredPosition = new Vector2(0f, -22f);
            var radar = radarRect.gameObject.AddComponent<ListRadarGraphic>();
            radar.raycastTarget = false;
            radar.SetDimensionCount(RADAR_DIMENSIONS.Length);
            for (var i = 0; i < RADAR_DIMENSIONS.Length; i++)
            {
                var label = CreateText(radarPanel.rectTransform, GetDimensionName(RADAR_DIMENSIONS[i]), 15f, MUTED_COLOR, TextAlignmentOptions.Center, FontStyles.Normal);
                var labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0.5f, 0.5f);
                labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.sizeDelta = new Vector2(120f, 26f);
                labelRect.anchoredPosition = radarRect.anchoredPosition + radar.GetDirection(i) * (radarSize * 0.5f + 26f);
            }

            var densityX = rightX + radarWidth + 20f;
            var densityPanel = CreatePanel(_detailContent, PAPER_COLOR, 16f);
            PlaceTopLeft(densityPanel.rectTransform, densityX, rightY, rightWidth - radarWidth - 20f, CHART_PANEL_HEIGHT);
            var densityTitle = CreateText(densityPanel.rectTransform, "MAJTEXT_LIST_DENSITY".i18n(), 18f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            PlaceRow(densityTitle.rectTransform, 22f, 16f, 22f, 30f);
            var densityInfo = CreateText(densityPanel.rectTransform, string.Empty, 16f, MUTED_COLOR, TextAlignmentOptions.MidlineRight, FontStyles.Normal);
            PlaceRow(densityInfo.rectTransform, 140f, 16f, 22f, 30f);
            var densityImage = CreateRect("Density", densityPanel.rectTransform).gameObject.AddComponent<RawImage>();
            densityImage.raycastTarget = false;
            densityImage.color = new Color(1f, 1f, 1f, 0f);
            var densityRect = densityImage.rectTransform;
            densityRect.anchorMin = Vector2.zero;
            densityRect.anchorMax = Vector2.one;
            densityRect.offsetMin = new Vector2(22f, 22f);
            densityRect.offsetMax = new Vector2(-22f, -64f);
            var rightBottom = rightY + CHART_PANEL_HEIGHT;

            // 底部给悬浮的游玩按钮留出空间
            _detailContent.sizeDelta = new Vector2(0f, Mathf.Max(leftBottom, rightBottom) + 150f);
            _detailPlayButton.interactable = hasLevel;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_detailContent);
            var maxY = Mathf.Max(0f, _detailContent.rect.height - _detailViewport.rect.height);
            _detailScroll.StopMovement();
            _detailContent.anchoredPosition = new Vector2(_detailContent.anchoredPosition.x, isScrollReset ? 0f : Mathf.Clamp(scrollY, 0f, maxY));

            if (hasLevel)
            {
                var detailChart = new DetailChartViews(radar, constantText, densityImage, densityInfo, lengthValue, bpmValue);
                LoadDetailChartAsync(song, level, detailChart, token).Forget();
            }
            else
            {
                densityInfo.text = "MAJTEXT_LIST_NO_CHART".i18n();
            }
        }
        /// <summary>
        /// 成绩卡：大字达成率，旁边是评级与 FC/AP；下一行是“DX 分 / 满分”和“X PC · 最近日期”
        /// </summary>
        void BuildDetailScore(RectTransform card, ISongDetail song, ChartLevel level, bool hasLevel)
        {
            var score = hasLevel ? ScoreManager.GetScore(song, level) : null;
            if (score is null || score.PlayCount <= 0)
            {
                var text = hasLevel ? "MAJTEXT_SCORE_UNPLAYED".i18n() : "MAJTEXT_LIST_NO_CHART".i18n();
                var unplayed = CreateText(card, text, 30f, MUTED_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                Stretch(unplayed.rectTransform);
                unplayed.rectTransform.offsetMin = new Vector2(30f, 0f);
                unplayed.rectTransform.offsetMax = new Vector2(-30f, 0f);
                return;
            }
            var achievement = score.Acc.DX;
            var grade = GetGrade(achievement);
            var top = CreateRect("Top", card);
            PlaceRow(top, 30f, 12f, 30f, 76f);
            var layout = top.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            AddFittedText(top, FormatAchievement(achievement), 50f, TEXT_COLOR, FontStyles.Bold);
            AddBadge(top, grade, GetGradeColor(grade), 24f);
            var combo = GetComboText(score.ComboState);
            if (!string.IsNullOrEmpty(combo))
            {
                AddBadge(top, combo, GetComboColor(score.ComboState), 22f);
            }
            var dxText = $"DX {score.DXScore.ToString(CultureInfo.InvariantCulture)} / {score.TotalDXScore.ToString(CultureInfo.InvariantCulture)}";
            var dx = CreateText(card, dxText, 20f, MUTED_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            PlaceRow(dx.rectTransform, 30f, 100f, 30f, 36f);
            var stats = CreateText(card, FormatPlayStats(score), 18f, MUTED_COLOR, TextAlignmentOptions.MidlineRight, FontStyles.Normal);
            PlaceRow(stats.rectTransform, 30f, 100f, 30f, 36f);
        }
        static void SetInteractValues(TextMeshProUGUI[] values, MajNetSongInteract interact)
        {
            values[5].text = interact.Plays.ToString(CultureInfo.InvariantCulture);
            values[6].text = GetLikeCount(interact).ToString(CultureInfo.InvariantCulture);
            values[7].text = (interact.Comments?.Length ?? 0).ToString(CultureInfo.InvariantCulture);
        }
        async UniTaskVoid LoadDetailInteractAsync(OnlineSongDetail song, TextMeshProUGUI[] values, CancellationToken token)
        {
            try
            {
                var interact = await GetInteractAsync(song, token);
                await UniTask.SwitchToMainThread();
                if (token.IsCancellationRequested || interact is not MajNetSongInteract value)
                {
                    return;
                }
                _interactCache[song.Hash] = value;
                if (values[5] != null)
                {
                    SetInteractValues(values, value);
                }
                foreach (var card in _cards)
                {
                    if (ReferenceEquals(card.Song, song))
                    {
                        RefreshCardInfo(card);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
        async UniTaskVoid LoadDetailCoverAsync(ISongDetail song, Image image, CancellationToken token)
        {
            try
            {
                var sprite = await song.GetCoverAsync(false, token: token);
                await UniTask.SwitchToMainThread();
                if (token.IsCancellationRequested || image == null)
                {
                    return;
                }
                _detailCover = sprite;
                _detailCoverHash = song.Hash;
                SetCover(image, sprite);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                MajDebug.LogWarning($"[SongListBrowser] Failed to load cover of {song.Title}: {e.Message}");
            }
        }
        readonly struct DetailChartViews
        {
            public readonly ListRadarGraphic Radar;
            public readonly TextMeshProUGUI ConstantText;
            public readonly RawImage DensityImage;
            public readonly TextMeshProUGUI DensityInfo;
            public readonly TextMeshProUGUI LengthValue;
            public readonly TextMeshProUGUI BpmValue;

            public DetailChartViews(ListRadarGraphic radar, TextMeshProUGUI constantText, RawImage densityImage, TextMeshProUGUI densityInfo,
                                    TextMeshProUGUI lengthValue, TextMeshProUGUI bpmValue)
            {
                Radar = radar;
                ConstantText = constantText;
                DensityImage = densityImage;
                DensityInfo = densityInfo;
                LengthValue = lengthValue;
                BpmValue = bpmValue;
            }
        }
        async UniTaskVoid LoadDetailChartAsync(ISongDetail song, ChartLevel level, DetailChartViews views, CancellationToken token)
        {
            try
            {
                views.DensityInfo.text = "MAJTEXT_LIST_LOADING".i18n();
                var simaiFile = await song.GetMaidataAsync(false, token: token);
                await UniTask.SwitchToMainThread();
                if (token.IsCancellationRequested || views.DensityImage == null)
                {
                    return;
                }
                var chart = simaiFile.Charts[(int)level];
                var rect = views.DensityImage.rectTransform.rect;
                var graphResult = ChartAnalyzer.AnalyzeMaidataWithGraph(chart, Math.Max(64, (int)rect.height), Math.Max(64, (int)rect.width));
                views.DensityInfo.text = string.Empty;
                if (graphResult.Length > TimeSpan.Zero)
                {
                    var length = FormatLength(graphResult.Length);
                    var bpm = FormatBpm(graphResult.MinBPM, graphResult.MaxBPM);
                    views.DensityInfo.text = $"{"MAJTEXT_LIST_LABEL_LENGTH".i18n()} {length}  ·  BPM {bpm}";
                    views.LengthValue.text = length;
                    views.BpmValue.text = bpm;
                    _chartInfoCache[(song.Hash, level)] = new MaidataAnalyzeResult()
                    {
                        Length = graphResult.Length,
                        MinBPM = graphResult.MinBPM,
                        MaxBPM = graphResult.MaxBPM,
                    };
                }
                if (graphResult.LineGraph != null)
                {
                    _createdTextures.Add(graphResult.LineGraph);
                    views.DensityImage.texture = graphResult.LineGraph;
                    views.DensityImage.color = Color.white;
                }

                var radarResult = await ChartRadarService.AnalyzeAsync(chart, token);
                await UniTask.SwitchToMainThread();
                if (token.IsCancellationRequested || views.Radar == null || !radarResult.IsSuccess)
                {
                    return;
                }
                // 与原版雷达一样只画 6 个维度；fitted_constant 就是右上角的估算定数
                var values = new float[RADAR_DIMENSIONS.Length];
                for (var i = 0; i < RADAR_DIMENSIONS.Length; i++)
                {
                    radarResult.Scores.TryGetValue(RADAR_DIMENSIONS[i], out var value);
                    values[i] = Mathf.Clamp01((float)((value ?? 0d) / 250d));
                }
                views.Radar.SetValues(values);
                if (radarResult.FittedConstant is double constant && constant > 0)
                {
                    views.ConstantText.text = "MAJTEXT_LIST_CONSTANT".i18n(constant.ToString("0.0", CultureInfo.InvariantCulture));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                await UniTask.SwitchToMainThread();
                MajDebug.LogError(e);
                if (!token.IsCancellationRequested && views.DensityInfo != null)
                {
                    views.DensityInfo.text = string.Empty;
                }
            }
        }
        void PlaySelected()
        {
            var song = _detailSong;
            if (song is null || _isDetailClosing || !HasLevel(song, _detailLevel))
            {
                return;
            }
            // 打完回到选歌时重新打开这首歌的详情，列表回到现在的分类、页码和滚动位置
            SaveState(song, _detailLevel);
            // 新的加载界面盖在最上面，转场和场景切换都在它下面进行
            var cover = _detailCoverHash == song.Hash ? _detailCover : null;
            ListLoadingScreen.Show(song, _detailLevel, GetDifficultyColor(_detailLevel), cover, _font);
            LeaveList();
            _listManager.EnterGame(song, _detailLevel);
        }
        void DestroyCreatedTextures()
        {
            foreach (var texture in _createdTextures)
            {
                if (texture != null)
                {
                    Destroy(texture);
                }
            }
            _createdTextures.Clear();
        }
    }
}
