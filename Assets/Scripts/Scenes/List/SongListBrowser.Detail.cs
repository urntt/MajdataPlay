using Cysharp.Threading.Tasks;
using LitMotion;
using MajdataPlay.Diagnostics;
using MajdataPlay.Net;
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
        /// <summary>
        /// The width of the Back button at the top left of the detail page.
        /// </summary>
        const float DETAIL_BACK_WIDTH = 150f;
        const float DETAIL_GAP = 32f;
        /// <summary>
        /// The distance of the detail top bar from the top of the page.
        /// </summary>
        const float DETAIL_BAR_TOP = 24f;
        /// <summary>
        /// The top of the detail content below a one-row top bar.
        /// </summary>
        const float DETAIL_TOP = 100f;
        /// <summary>
        /// The largest cover in the single-column detail page.
        /// </summary>
        const float SINGLE_COLUMN_COVER_MAX = 520f;
        /// <summary>
        /// The smallest cover when the cover is limited by the page height.
        /// </summary>
        const float MIN_DETAIL_COVER = 200f;
        const float INFO_ROW_HEIGHT = 46f;
        /// <summary>
        /// The height of the score card: achievement row, DX score with stars, the DX bar, then plays and the last play date.
        /// </summary>
        const float SCORE_CARD_HEIGHT = 190f;
        /// <summary>
        /// The height of one difficulty tab on the detail page.
        /// </summary>
        const float DETAIL_LEVEL_TAG_HEIGHT = 54f;
        const float CHART_PANEL_HEIGHT = 400f;

        RectTransform _detailView = null!;
        RectTransform _detailViewport = null!;
        RectTransform _detailContent = null!;
        ScrollRect _detailScroll = null!;
        CanvasGroup _detailGroup = null!;
        Button _detailPlayButton = null!;
        /// <summary>
        /// The holder of the play button and its shadow, placed and sized by <see cref="PlaceDetailActionButtons"/>.
        /// </summary>
        RectTransform _detailPlayHolder = null!;
        /// <summary>
        /// The icon of the feedback button.
        /// </summary>
        Image _detailFeedbackIcon = null!;
        /// <summary>
        /// The play icon, faded together with the play button when the song has no chart of the chosen difficulty.
        /// </summary>
        Image _detailPlayIcon = null!;
        /// <summary>
        /// The feedback button left of the play button, shown only for charts that accept feedback.
        /// </summary>
        RectTransform _detailFeedbackHolder = null!;
        /// <summary>
        /// The values of the info card on the detail page; the like and comment counts are updated after feedback is sent.
        /// </summary>
        TextMeshProUGUI[]? _detailInfoValues;
        Image? _detailHeart;
        TextMeshProUGUI _detailLevelText = null!;
        /// <summary>
        /// The holder of Cover, −, the difficulty, + and Settings on the detail page, scaled down or moved to a second row on narrow pages.
        /// </summary>
        RectTransform _detailTopControls = null!;
        /// <summary>
        /// The player nickname and avatar on the detail page; only the avatar on narrow pages.
        /// </summary>
        RectTransform _detailProfile = null!;

        ISongDetail? _detailSong;
        ChartLevel _detailLevel;
        Sprite? _detailCover;
        string? _detailCoverHash;
        float _lastDetailWidth = -1f;
        /// <summary>
        /// The page height the detail page was last laid out for; a change (rotation, keyboard-free resize) lays it out again.
        /// </summary>
        float _lastDetailHeight = -1f;
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
            PlaceTopLeft((RectTransform)back.transform, DETAIL_MARGIN, DETAIL_BAR_TOP, DETAIL_BACK_WIDTH, TOPBAR_HEIGHT);

            // 返回键右边是与列表相同的一组控件；这里的 −/+ 只切换当前谱面的难度，不改全局难度
            // 位置与大小由 LayoutDetailTopBar 按页面宽度决定
            _detailTopControls = CreateRect("TopControls", _detailView);
            PlaceTopLeft(_detailTopControls, DETAIL_MARGIN + DETAIL_BACK_WIDTH + 16f, DETAIL_BAR_TOP, TOP_CONTROLS_WIDTH, TOPBAR_HEIGHT);
            _detailLevelText = BuildTopControls(_detailTopControls, SwitchToCover, () => ChangeDetailLevel(-1), () => ChangeDetailLevel(1), EnterSetting);
            // 右上角：玩家昵称与头像，与列表同一位置
            _detailProfile = new LightUi(_font).CreatePlayerProfile(_detailView, TOPBAR_HEIGHT, PAGE_COLOR);
            PlaceTopRight(_detailProfile, MAIN_RIGHT_PADDING, DETAIL_BAR_TOP, PROFILE_WIDTH, TOPBAR_HEIGHT);

            // 右下角：评价、游玩两个图标键；柔和投影与按钮同一圆角、中心对齐，大小由 PlaceDetailActionButtons 按页面高度决定
            _detailPlayHolder = LightUi.CreateActionButton(_detailView, UiSprites.Play, ACCENT_COLOR, Color.white, 0.2f, PlaySelected,
                                                           out _detailPlayButton, out _detailPlayIcon);
            _detailFeedbackHolder = LightUi.CreateActionButton(_detailView, UiSprites.Comment, PAPER_COLOR, FAVORITE_COLOR, 0.16f, ShowFeedbackDialog,
                                                               out _, out _detailFeedbackIcon);
            _detailFeedbackHolder.gameObject.SetActive(false);
            _detailView.gameObject.SetActive(false);
        }
        /// <summary>
        /// Fits the detail top bar into the page width: one row with the nickname on wide pages, one row with only the avatar on
        /// medium pages, and on narrow pages Back and the avatar in the first row and the other controls in a second row.
        /// </summary>
        /// <param name="width">The width of the detail page.</param>
        /// <returns>The top of the detail content, below the top bar.</returns>
        float LayoutDetailTopBar(float width)
        {
            var isProfileFull = width >= SidebarDrawer.COMPACT_WIDTH;
            var profileWidth = SetProfileNameVisible(_detailProfile, isProfileFull);
            PlaceTopRight(_detailProfile, MAIN_RIGHT_PADDING, DETAIL_BAR_TOP, profileWidth, TOPBAR_HEIGHT);
            if (width < DETAIL_STACKED_TOP_WIDTH)
            {
                var controlsTop = DETAIL_BAR_TOP + TOPBAR_HEIGHT + 10f;
                FitTopControls(_detailTopControls, DETAIL_MARGIN, controlsTop, width - DETAIL_MARGIN - MAIN_RIGHT_PADDING);
                return controlsTop + TOPBAR_HEIGHT + 20f;
            }
            var controlsLeft = DETAIL_MARGIN + DETAIL_BACK_WIDTH + 16f;
            FitTopControls(_detailTopControls, controlsLeft, DETAIL_BAR_TOP, width - controlsLeft - profileWidth - TOPBAR_GAP - MAIN_RIGHT_PADDING);
            return DETAIL_TOP;
        }
        /// <summary>
        /// Places the play button at the bottom right and the feedback button left of it, sized for the height of the page.
        /// </summary>
        /// <returns>The size of the buttons, see <see cref="LightUi.GetActionButtonSize"/>.</returns>
        float PlaceDetailActionButtons()
        {
            var size = LightUi.GetActionButtonSize(_detailView.rect.height);
            LightUi.PlaceActionButton(_detailPlayHolder, _detailPlayIcon, DETAIL_MARGIN, size);
            LightUi.PlaceActionButton(_detailFeedbackHolder, _detailFeedbackIcon, DETAIL_MARGIN + size + LightUi.ACTION_BUTTON_GAP, size);
            return size;
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
        void UpdateDetailLevelText()
        {
            _detailLevelText.text = ScoreExchange.GetDifficultyName(_detailLevel);
            _detailLevelText.color = GetDifficultyColor(_detailLevel);
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
        /// <summary>
        /// 详情页的 −/+：只在这首歌已有的难度之间切换，到头循环
        /// </summary>
        void ChangeDetailLevel(int direction)
        {
            var song = _detailSong;
            if (song is null || _isDetailClosing)
            {
                return;
            }
            var count = LEVEL_SHORT_NAMES.Length;
            var index = (int)_detailLevel;
            for (var step = 1; step <= count; step++)
            {
                var candidate = ((index + direction * step) % count + count) % count;
                if (HasLevel(song, (ChartLevel)candidate))
                {
                    SetDetailLevel((ChartLevel)candidate);
                    return;
                }
            }
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
            var height = _detailViewport.rect.height;
            _lastDetailWidth = width;
            _lastDetailHeight = height;
            var detailTop = LayoutDetailTopBar(width);
            // 竖屏或窄屏为单栏：曲绘、分享与导出、曲名与难度、成绩卡、信息卡、雷达、物量分布
            var isSingleColumn = width < SidebarDrawer.COMPACT_WIDTH || height > width;

            float leftWidth;
            float rightX;
            float rightWidth;
            float coverSize;
            if (isSingleColumn)
            {
                leftWidth = Mathf.Max(1f, width - DETAIL_MARGIN * 2f);
                rightX = DETAIL_MARGIN;
                rightWidth = leftWidth;
                coverSize = Mathf.Min(leftWidth, SINGLE_COLUMN_COVER_MAX);
            }
            else
            {
                leftWidth = Mathf.Clamp((width - DETAIL_MARGIN * 2f - DETAIL_GAP) * 0.32f, 320f, 480f);
                rightX = DETAIL_MARGIN + leftWidth + DETAIL_GAP;
                rightWidth = Mathf.Max(360f, width - rightX - DETAIL_MARGIN);
                // 矮屏（横屏手机）上曲绘边长也受高度限制，曲绘和分享键在第一屏放得下
                coverSize = Mathf.Min(leftWidth, Mathf.Max(MIN_DETAIL_COVER, height - detailTop - 98f));
            }
            var coverX = DETAIL_MARGIN + (leftWidth - coverSize) * 0.5f;

            // 左栏：曲绘（右上角爱心）、分享与导出、信息卡
            var cover = LightUi.CreateRoundedCover(_detailContent, 20f, PAGE_COLOR, out var coverFrame);
            PlaceTopLeft(coverFrame, coverX, detailTop, coverSize, coverSize);
            if (_detailCover != null && _detailCoverHash == song.Hash)
            {
                SetCover(cover, _detailCover);
            }
            else
            {
                LoadDetailCoverAsync(song, cover, token).Forget();
            }
            _detailHeart = CreateHeartButton(_detailContent, song, 60f, new Color(1f, 1f, 1f, 0.92f), out var heartRect);
            PlaceTopLeft(heartRect, coverX + coverSize - 74f, detailTop + 14f, 60f, 60f);

            var leftY = detailTop + coverSize + 18f;
            // 分享成绩、导出成绩两个并排
            var buttonWidth = (leftWidth - 10f) / 2f;
            var share = CreateButton(_detailContent, "MAJTEXT_LIST_SHARE".i18n(), PAPER_COLOR, TEXT_COLOR, 19f, () => ShowShareModal(false));
            PlaceTopLeft((RectTransform)share.transform, DETAIL_MARGIN, leftY, buttonWidth, 56f);
            var export = CreateButton(_detailContent, "MAJTEXT_LIST_EXPORT".i18n(), PAPER_COLOR, TEXT_COLOR, 19f, () => ShowShareModal(true));
            PlaceTopLeft((RectTransform)export.transform, DETAIL_MARGIN + buttonWidth + 10f, leftY, buttonWidth, 56f);
            UpdateDetailLevelText();
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
            // 右栏（单栏时接在分享键下面）：曲名、难度分段条、成绩卡；单栏时信息卡在成绩卡下面
            var segmentsHeight = DETAIL_LEVEL_TAG_HEIGHT + 4f + LEVEL_SCORE_HEIGHT;
            var titleY = isSingleColumn ? leftY : detailTop;
            var scoreY = titleY + 62f + segmentsHeight + 20f;
            var infoY = isSingleColumn ? scoreY + SCORE_CARD_HEIGHT + 20f : leftY;
            var chartsY = isSingleColumn ? infoY + infoHeight + 20f : scoreY + SCORE_CARD_HEIGHT + 20f;
            var infoCard = CreatePanel(_detailContent, PAPER_COLOR, 16f);
            PlaceTopLeft(infoCard.rectTransform, DETAIL_MARGIN, infoY, leftWidth, infoHeight);
            var values = new TextMeshProUGUI[rowLabels.Count];
            _detailInfoValues = values;
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
            var leftBottom = infoY + infoHeight;

            var title = CreateText(_detailContent, song.Title, 36f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            PlaceTopLeft(title.rectTransform, rightX, titleY - 6f, rightWidth, 56f);
            AddMarquee(title);
            var rightY = titleY + 62f;

            // 难度分段条：每段下面是这个难度的达成率，与选歌卡片一致
            var segments = CreateRect("Levels", _detailContent);
            PlaceTopLeft(segments, rightX, rightY, rightWidth, segmentsHeight);
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
                var column = CreateRect("Level", segments);
                column.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                var columnLayout = column.gameObject.AddComponent<VerticalLayoutGroup>();
                columnLayout.spacing = 4f;
                columnLayout.childAlignment = TextAnchor.UpperCenter;
                columnLayout.childControlWidth = true;
                columnLayout.childControlHeight = true;
                columnLayout.childForceExpandWidth = true;
                columnLayout.childForceExpandHeight = false;
                var segment = CreateLevelTag(column, target, $"{ScoreExchange.GetDifficultyName(target)} {songLevels[i]}", target == level, 18f, () => SetDetailLevel(target));
                segment.gameObject.AddComponent<LayoutElement>().preferredHeight = DETAIL_LEVEL_TAG_HEIGHT;
                var segmentLabel = segment.GetComponentInChildren<TextMeshProUGUI>();
                segmentLabel.enableAutoSizing = true;
                segmentLabel.fontSizeMin = isSingleColumn ? 10f : 12f;
                segmentLabel.fontSizeMax = 18f;
                AddLevelAchievement(column, song, target, 16f);
            }
            var scoreCard = CreatePanel(_detailContent, PAPER_COLOR, 16f);
            PlaceTopLeft(scoreCard.rectTransform, rightX, scoreY, rightWidth, SCORE_CARD_HEIGHT);
            BuildDetailScore(scoreCard.rectTransform, song, level, hasLevel);

            // 雷达与物量分布：两栏时并排，单栏时上下各占整行
            var radarWidth = isSingleColumn ? rightWidth : Mathf.Floor((rightWidth - 20f) * 0.44f);
            var densityX = isSingleColumn ? rightX : rightX + radarWidth + 20f;
            var densityY = isSingleColumn ? chartsY + CHART_PANEL_HEIGHT + 20f : chartsY;
            var densityWidth = isSingleColumn ? rightWidth : rightWidth - radarWidth - 20f;
            var radarPanel = CreatePanel(_detailContent, PAPER_COLOR, 16f);
            PlaceTopLeft(radarPanel.rectTransform, rightX, chartsY, radarWidth, CHART_PANEL_HEIGHT);
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

            var densityPanel = CreatePanel(_detailContent, PAPER_COLOR, 16f);
            PlaceTopLeft(densityPanel.rectTransform, densityX, densityY, densityWidth, CHART_PANEL_HEIGHT);
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
            var rightBottom = densityY + CHART_PANEL_HEIGHT;

            // 底部给右下角悬浮的图标键留出空间
            var actionButtonSize = PlaceDetailActionButtons();
            _detailContent.sizeDelta = new Vector2(0f, Mathf.Max(leftBottom, rightBottom) + LightUi.GetActionButtonClearance(actionButtonSize));
            LightUi.SetIconButtonInteractable(_detailPlayButton, _detailPlayIcon, hasLevel);
            _detailFeedbackHolder.gameObject.SetActive(ChartFeedbackSession.IsAvailable(song));
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
            // DX 分与星级、进度条，与结算页相同
            var dxText = $"DX  {score.DXScore.ToString(CultureInfo.InvariantCulture)} / {score.TotalDXScore.ToString(CultureInfo.InvariantCulture)}";
            var dx = CreateText(card, dxText, 20f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            PlaceRow(dx.rectTransform, 30f, 94f, 120f, 32f);
            var stars = LightUi.GetDxStarCount(score.DXScore, score.TotalDXScore);
            if (stars > 0)
            {
                var starText = CreateText(card, $"✧{stars}", 22f, GOLD_COLOR, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                PlaceRow(starText.rectTransform, 30f, 92f, 30f, 34f);
            }
            LightUi.AddDxProgressBar(card, 30f, 132f, 30f, 10f, score.DXScore, score.TotalDXScore);
            var stats = CreateText(card, FormatPlayStats(score), 17f, MUTED_COLOR, TextAlignmentOptions.MidlineRight, FontStyles.Normal);
            PlaceRow(stats.rectTransform, 30f, 150f, 30f, 30f);
        }
        static void SetInteractValues(TextMeshProUGUI[] values, MajNetSongInteract interact)
        {
            values[5].text = interact.Plays.ToString(CultureInfo.InvariantCulture);
            values[6].text = GetLikeCount(interact).ToString(CultureInfo.InvariantCulture);
            values[7].text = (interact.Comments?.Length ?? 0).ToString(CultureInfo.InvariantCulture);
        }
        /// <summary>
        /// Loads the like and comment counts of the chart on the detail page again from the server, after feedback was sent.
        /// </summary>
        /// <param name="song">The chart the feedback was sent for.</param>
        /// <param name="token">Cancels the request when the browser is destroyed.</param>
        void RefreshDetailInteract(OnlineSongDetail song, CancellationToken token)
        {
            var values = _detailInfoValues;
            if (values is null || !ReferenceEquals(_detailSong, song))
            {
                return;
            }
            LoadDetailInteractAsync(song, values, token, true).Forget();
        }
        /// <summary>
        /// Loads the plays, likes and comments of an online chart into the info card of the detail page and into its song card.
        /// </summary>
        /// <param name="song">The chart.</param>
        /// <param name="values">The values of the info card.</param>
        /// <param name="token">Cancels the request.</param>
        /// <param name="forceRefresh">Bypasses the cached counts, used after the player sent feedback.</param>
        async UniTaskVoid LoadDetailInteractAsync(OnlineSongDetail song, TextMeshProUGUI[] values, CancellationToken token, bool forceRefresh = false)
        {
            try
            {
                var interact = await GetInteractAsync(song, token, forceRefresh);
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
                // 按屏幕上的实际像素尺寸生成，避免放大后出锯齿
                var rect = views.DensityImage.rectTransform.rect;
                var canvas = _root.GetComponent<Canvas>();
                var pixelScale = canvas != null ? Mathf.Max(1f, canvas.scaleFactor) : 1f;
                var graphHeight = Mathf.Clamp(Mathf.RoundToInt(rect.height * pixelScale), 64, 2048);
                var graphWidth = Mathf.Clamp(Mathf.RoundToInt(rect.width * pixelScale), 64, 4096);
                var graphResult = ChartAnalyzer.AnalyzeMaidataWithGraph(chart, graphHeight, graphWidth);
                views.DensityInfo.text = string.Empty;
                if (graphResult.Length > TimeSpan.Zero)
                {
                    var length = FormatLength(graphResult.Length);
                    var bpm = FormatBpm(graphResult.MinBPM, graphResult.MaxBPM);
                    views.DensityInfo.text = $"{"MAJTEXT_LIST_LABEL_LENGTH".i18n()} {length}  ·  BPM {bpm}";
                    views.LengthValue.text = length;
                    views.BpmValue.text = bpm;
                    var chartInfo = new MaidataAnalyzeResult()
                    {
                        Length = graphResult.Length,
                        MinBPM = graphResult.MinBPM,
                        MaxBPM = graphResult.MaxBPM,
                    };
                    _chartInfoCache[(song.Hash, level)] = chartInfo;
                    // 回到列表时这首歌的卡片立即显示时长和 BPM（在线谱面在打开详情页时才下载）
                    foreach (var card in _cards)
                    {
                        if (ReferenceEquals(card.Song, song) && (card.ChartInfo is not MaidataAnalyzeResult shown || shown.Length <= TimeSpan.Zero))
                        {
                            card.ChartInfo = chartInfo;
                            RefreshCardInfo(card);
                        }
                    }
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
