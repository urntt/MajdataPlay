using Cysharp.Threading.Tasks;
using LitMotion;
using MajdataPlay.Collections;
using MajdataPlay.Diagnostics;
using MajdataPlay.IO;
using MajdataPlay.Misc.Base;
using MajdataPlay.Scenes.Game;
using MajdataPlay.Scenes.Game.Notes;
using MajdataPlay.Settings;
using System;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#nullable enable
namespace MajdataPlay.Scenes.Result
{
    /// <summary>
    /// 新结算界面需要的数据：本次成绩、保存之前的历史最佳，以及已展开的判定表（判定统计是 ref struct，不能保存，先转成文字）
    /// </summary>
    internal sealed class ResultViewData
    {
        public static readonly ScoreNoteType[] NOTE_TYPES = { ScoreNoteType.Tap, ScoreNoteType.Hold, ScoreNoteType.Slide, ScoreNoteType.Touch, ScoreNoteType.Break };
        public static readonly string[] NOTE_TYPE_NAMES = { "TAP", "HOLD", "SLIDE", "TOUCH", "BREAK" };

        public required GameResult Result { get; init; }
        public required bool IsClassic { get; init; }
        public required double Achievement { get; init; }
        public required double HistoryAchievement { get; init; }
        public required long HistoryPlayCount { get; init; }
        /// <summary>
        /// 开启了 Mod，本次成绩不保存
        /// </summary>
        public required bool IsScoreDiscarded { get; init; }
        /// <summary>
        /// 行：TAP、HOLD、SLIDE、TOUCH、BREAK、合计；列：CRITICAL、PERFECT、GREAT、GOOD、MISS
        /// </summary>
        public required string[,] JudgeTable { get; init; }
        public required string AverageOffsetText { get; init; }

        public static ResultViewData Create(GameResult result, bool isClassic, double historyAchievement, long historyPlayCount, bool isScoreDiscarded)
        {
            var table = new string[NOTE_TYPES.Length + 1, 5];
            for (var i = 0; i < NOTE_TYPES.Length; i++)
            {
                var info = JudgeDetail.UnpackJudgeRecord(result.JudgeRecord[NOTE_TYPES[i]]);
                table[i, 0] = info.CriticalPerfect.ToString(CultureInfo.InvariantCulture);
                table[i, 1] = info.Perfect.ToString(CultureInfo.InvariantCulture);
                table[i, 2] = info.Great.ToString(CultureInfo.InvariantCulture);
                table[i, 3] = info.Good.ToString(CultureInfo.InvariantCulture);
                table[i, 4] = info.Miss.ToString(CultureInfo.InvariantCulture);
            }
            // BREAK 的 PERFECT 与原版相同：写成“2550 的个数+2500 的个数”
            var breakRecord = result.JudgeRecord[ScoreNoteType.Break];
            var break2550 = breakRecord[JudgeGrade.FastPerfect2nd] + breakRecord[JudgeGrade.LatePerfect2nd];
            var break2500 = breakRecord[JudgeGrade.FastPerfect3rd] + breakRecord[JudgeGrade.LatePerfect3rd];
            table[NOTE_TYPES.Length - 1, 1] = break2550 + break2500 == 0 ? "0" : $"{break2550}+{break2500}";
            var total = JudgeDetail.UnpackJudgeRecord(result.JudgeRecord.TotalJudgeInfo);
            var totalRow = NOTE_TYPES.Length;
            table[totalRow, 0] = total.CriticalPerfect.ToString(CultureInfo.InvariantCulture);
            table[totalRow, 1] = total.Perfect.ToString(CultureInfo.InvariantCulture);
            table[totalRow, 2] = total.Great.ToString(CultureInfo.InvariantCulture);
            table[totalRow, 3] = total.Good.ToString(CultureInfo.InvariantCulture);
            table[totalRow, 4] = total.Miss.ToString(CultureInfo.InvariantCulture);

            // 平均偏移：单位跟随设置，负数为偏早
            var diffs = result.NoteJudgeDiffs;
            string averageOffset;
            if (MajEnv.Settings.Debug.OffsetUnit == OffsetUnitOption.Second)
            {
                averageOffset = diffs.IsEmpty ? "0.000s" : $"{(diffs.ToArray().Average() / 1000f).ToString("F3", CultureInfo.InvariantCulture)}s";
            }
            else
            {
                averageOffset = diffs.IsEmpty ? "0.0f" : $"{(diffs.ToArray().Average() / MajEnv.FRAME_LENGTH_MSEC).ToString("F1", CultureInfo.InvariantCulture)}f";
            }
            return new ResultViewData()
            {
                Result = result,
                IsClassic = isClassic,
                Achievement = isClassic ? result.Acc.Classic : result.Acc.DX,
                HistoryAchievement = historyAchievement,
                HistoryPlayCount = historyPlayCount,
                IsScoreDiscarded = isScoreDiscarded,
                JudgeTable = table,
                AverageOffsetText = averageOffset,
            };
        }
    }

    /// <summary>
    /// 列表式选歌时使用的新结算界面：浅色风格，左栏曲绘与曲目信息、收藏、点赞与上传；右栏达成率、DX 分、判定表与判定分布；
    /// 右下角“继续”。保存、上传、语音与 BGM 仍由 <see cref="ResultScreenManager"/> 负责，原界面停止渲染
    /// </summary>
    internal sealed class ResultBrowser : MonoBehaviour
    {
        const float MARGIN = 48f;
        const float COLUMN_GAP = 40f;
        const float CARD_GAP = 20f;
        const float ROLL_DURATION_SEC = 1.5f;

        ResultScreenManager _manager = null!;
        ResultViewData _data = null!;
        LightUi _ui = null!;
        RectTransform _root = null!;
        CanvasGroup _group = null!;
        readonly SceneCanvasHider _canvasHider = new();
        RectTransform _content = null!;
        RectTransform _viewport = null!;
        Vector2 _lastSize = Vector2.zero;

        TextMeshProUGUI? _achievementText;
        Image? _coverImage;
        Image? _heart;
        Button? _likeButton;
        TextMeshProUGUI? _likeStatus;
        Button? _uploadButton;
        TextMeshProUGUI? _uploadStatus;
        Button _continueButton = null!;
        TextMeshProUGUI _continueLabel = null!;
        Texture? _graphTexture;
        Sprite? _cover;
        MotionHandle _rollMotion;
        bool _hasRolled = false;
        bool _isLeaving = false;
        string _likeInfoText = string.Empty;

        public static ResultBrowser? Create(ResultScreenManager manager, ResultViewData data)
        {
            try
            {
                var obj = new GameObject("ResultBrowser");
                var browser = obj.AddComponent<ResultBrowser>();
                browser.Init(manager, data);
                return browser;
            }
            catch (Exception e)
            {
                MajDebug.LogError(e);
                InputManager.SuppressTouchPanelSimulation = false;
                return null;
            }
        }

        #region Lifecycle
        void Init(ResultScreenManager manager, ResultViewData data)
        {
            _manager = manager;
            _data = data;
            _ui = new LightUi(LightUi.FindFont());
            _likeInfoText = "MAJTEXT_THUMBUP_INFO".i18n();
            LightUi.EnsureEventSystem();
            _root = LightUi.CreateOverlayCanvas(transform, "ResultBrowserCanvas", 30, out _group);
            LightUi.FollowTransition(_group, false);

            var page = LightUi.CreateImage(_root, LightUi.PAGE, 0f);
            LightUi.Stretch(page.rectTransform);
            _content = LightUi.CreateScroll(_root, 0f, 0f, 0f, 0f, out var scroll);
            _viewport = (RectTransform)_content.parent;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            // 右下角悬浮的“继续”：保存与上传完成之前不能点
            _continueButton = _ui.CreateButton(_root, "MAJTEXT_RESULT_CONTINUE".i18n(), LightUi.ACCENT, Color.white, 28f, Continue, 14f);
            LightUi.PlaceBottomRight((RectTransform)_continueButton.transform, MARGIN, 36f, 240f, 80f);
            _continueLabel = _continueButton.GetComponentInChildren<TextMeshProUGUI>();
            _continueLabel.fontStyle = FontStyles.Bold;

            _canvasHider.Hide(gameObject);
            // 屏幕触摸不再模拟机台按键（否则点按钮会同时触发 A4 退出、A5 收藏等）；实体按键照常可用
            InputManager.SuppressTouchPanelSimulation = true;
            LoadCoverAsync().Forget();
        }
        async UniTaskVoid LoadCoverAsync()
        {
            var song = _data.Result.SongDetail;
            try
            {
                var sprite = await song.GetCoverAsync(true, token: this.GetCancellationTokenOnDestroy());
                await UniTask.SwitchToMainThread();
                if (this == null)
                {
                    return;
                }
                _cover = sprite;
                if (_coverImage != null)
                {
                    LightUi.SetCover(_coverImage, sprite);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                MajDebug.LogWarning($"[ResultBrowser] Failed to load cover of {song.Title}: {e.Message}");
            }
        }
        void OnDestroy()
        {
            InputManager.SuppressTouchPanelSimulation = false;
            _rollMotion.TryCancel();
            if (_graphTexture != null)
            {
                Destroy(_graphTexture);
            }
        }
        void Update()
        {
            LightUi.FollowTransition(_group, _isLeaving || _manager.IsExited);
            // 转场基本打开后再开始滚动达成率
            if (!_hasRolled && SceneSwitcher.TransitionProgress < 0.3f && _achievementText != null)
            {
                _hasRolled = true;
                PlayAchievementRoll();
            }
            UpdateDynamicParts();
        }
        void LateUpdate()
        {
            // 屏幕方向或尺寸变化（或曲绘加载完成）时重新排版
            var size = _viewport.rect.size;
            if (size.x > 0f && size.y > 0f && (size - _lastSize).sqrMagnitude > 1f)
            {
                _lastSize = size;
                Layout(size.x, size.y);
            }
        }
        #endregion

        #region Layout
        void Layout(float width, float height)
        {
            _rollMotion.TryCancel();
            LightUi.ClearChildren(_content);
            var isLandscape = width >= height * 1.1f;
            float leftX, leftWidth, rightX, rightWidth, rightTop;
            if (isLandscape)
            {
                leftWidth = Mathf.Clamp(width * 0.3f, 360f, 520f);
                leftX = MARGIN;
                rightX = leftX + leftWidth + COLUMN_GAP;
                rightWidth = width - rightX - MARGIN;
                rightTop = 56f;
            }
            else
            {
                leftWidth = Mathf.Min(width - MARGIN * 2f, 520f);
                leftX = (width - leftWidth) * 0.5f;
                rightX = MARGIN;
                rightWidth = width - MARGIN * 2f;
                rightTop = 0f;
            }
            var leftBottom = BuildLeftColumn(leftX, 56f, leftWidth);
            if (!isLandscape)
            {
                rightTop = leftBottom + 32f;
            }
            var rightBottom = BuildRightColumn(rightX, rightTop, rightWidth);
            // 底部给悬浮的“继续”留出空间
            _content.sizeDelta = new Vector2(0f, Mathf.Max(leftBottom, rightBottom) + 140f);
            if (_hasRolled)
            {
                SetAchievementText(_data.Achievement);
            }
        }
        /// <summary>
        /// 左栏：圆角曲绘（右上角爱心）、难度、曲名、曲与谱、点赞与上传、成绩是否保存
        /// </summary>
        float BuildLeftColumn(float x, float y, float width)
        {
            var result = _data.Result;
            var song = result.SongDetail;
            var level = result.Level;
            var cover = LightUi.CreateRoundedCover(_content, 22f, out var coverFrame);
            LightUi.PlaceTopLeft(coverFrame, x, y, width, width);
            LightUi.SetCover(cover, _cover);
            _coverImage = cover;

            // 爱心收藏：调用原有的收藏组件（A5 也走它），状态以收藏夹为准
            var heartBack = LightUi.CreateImage(_content, new Color(1f, 1f, 1f, 0.92f), 30f);
            heartBack.raycastTarget = true;
            LightUi.PlaceTopLeft(heartBack.rectTransform, x + width - 74f, y + 14f, 60f, 60f);
            var heartButton = heartBack.gameObject.AddComponent<Button>();
            heartButton.targetGraphic = heartBack;
            LightUi.SetButtonColors(heartButton);
            heartButton.onClick.AddListener(ToggleFavorite);
            _heart = LightUi.CreateImage(heartBack.transform, Color.white, 0f);
            _heart.preserveAspect = true;
            var heartRect = _heart.rectTransform;
            heartRect.anchorMin = new Vector2(0.5f, 0.5f);
            heartRect.anchorMax = new Vector2(0.5f, 0.5f);
            heartRect.sizeDelta = new Vector2(36f, 36f);
            heartRect.anchoredPosition = new Vector2(0f, -1f);
            UpdateHeart();

            var cursor = y + width + 20f;
            var levels = song.Levels;
            var levelIndex = (int)level;
            var levelText = levelIndex < levels.Length ? levels[levelIndex] : string.Empty;
            var difficulty = ScoreExchange.GetDifficultyName(level);
            var levelColor = LightUi.GetDifficultyColor(level);
            var tag = LightUi.CreateImage(_content, LightUi.WithAlpha(levelColor, 0.12f), 12f);
            LightUi.PlaceTopLeft(tag.rectTransform, x, cursor, 220f, 44f);
            var tagLabel = _ui.CreateText(tag.transform, string.IsNullOrEmpty(levelText) ? difficulty : $"{difficulty} {levelText}", 20f, levelColor, TextAlignmentOptions.Center, FontStyles.Bold);
            LightUi.Stretch(tagLabel.rectTransform, 4f);
            cursor += 56f;

            var title = _ui.CreateText(_content, song.Title, 36f, LightUi.TEXT, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            LightUi.PlaceTopLeft(title.rectTransform, x, cursor, width, 56f);
            title.gameObject.AddComponent<TMPMarqueeText>();
            cursor += 58f;
            var designers = song.Designers;
            var designer = levelIndex < designers.Length && !string.IsNullOrWhiteSpace(designers[levelIndex]) ? designers[levelIndex] : "—";
            var artist = string.IsNullOrWhiteSpace(song.Artist) ? "—" : song.Artist;
            var credits = _ui.CreateText(_content, $"{"MAJTEXT_LIST_LABEL_ARTIST".i18n()} {artist}    {"MAJTEXT_LIST_LABEL_DESIGNER".i18n()} {designer}", 19f, LightUi.MUTED, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            LightUi.PlaceTopLeft(credits.rectTransform, x, cursor, width, 30f);
            cursor += 44f;

            // 网络谱面：点赞与上传
            var sender = _manager.OnlineSender;
            _likeButton = null;
            _likeStatus = null;
            _uploadButton = null;
            _uploadStatus = null;
            if (sender != null && sender.CanInteract)
            {
                _likeButton = _ui.CreateButton(_content, "MAJTEXT_RESULT_LIKE".i18n(), LightUi.PAPER, LightUi.FAVORITE, 19f, () => _manager.OnlineSender.RequestLike());
                LightUi.PlaceTopLeft((RectTransform)_likeButton.transform, x, cursor, 150f, 50f);
                _likeStatus = _ui.CreateText(_content, string.Empty, 17f, LightUi.MUTED, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
                LightUi.PlaceTopLeft(_likeStatus.rectTransform, x + 166f, cursor, width - 166f, 50f);
                cursor += 62f;
                if (!MajEnv.Settings.IsAnyModActive())
                {
                    _uploadButton = _ui.CreateButton(_content, "MAJTEXT_RESULT_UPLOAD".i18n(), LightUi.PAPER, LightUi.ACCENT, 19f, () => _manager.OnlineSender.RequestUpload());
                    LightUi.PlaceTopLeft((RectTransform)_uploadButton.transform, x, cursor, 150f, 50f);
                    _uploadStatus = _ui.CreateText(_content, string.Empty, 17f, LightUi.MUTED, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
                    LightUi.PlaceTopLeft(_uploadStatus.rectTransform, x + 166f, cursor, width - 166f, 50f);
                    cursor += 62f;
                }
            }
            if (_data.IsScoreDiscarded)
            {
                var notice = _ui.CreateText(_content, "MAJTEXT_RESULT_NOT_SAVED".i18n(), 17f, LightUi.RED, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
                LightUi.PlaceTopLeft(notice.rectTransform, x, cursor, width, 30f);
                cursor += 40f;
            }
            return cursor;
        }
        /// <summary>
        /// 右栏：达成率卡片、DX 分卡片、判定表、FAST/LATE 与平均偏移、判定分布
        /// </summary>
        float BuildRightColumn(float x, float y, float width)
        {
            var result = _data.Result;
            var cursor = y;

            // 达成率：大字（滚动）、评级、FC/AP；下一行与历史最佳比较
            var accCard = CreateCard(x, cursor, width, 168f);
            var topRow = LightUi.CreateRow(accCard, TextAnchor.MiddleLeft, 18f);
            LightUi.PlaceRow(topRow, 32f, 14f, 32f, 96f);
            _achievementText = _ui.AddFittedText(topRow, FormatAchievement(0d), 64f, LightUi.TEXT, FontStyles.Bold);
            var grade = LightUi.GetGrade(result.Acc.DX);
            _ui.AddBadge(topRow, grade, LightUi.GetGradeColor(grade), 28f);
            var combo = LightUi.GetComboText(result.ComboState);
            if (!string.IsNullOrEmpty(combo))
            {
                _ui.AddBadge(topRow, combo, LightUi.GetComboColor(result.ComboState), 26f);
            }
            var compareRow = LightUi.CreateRow(accCard, TextAnchor.MiddleLeft, 12f);
            LightUi.PlaceRow(compareRow, 34f, 114f, 32f, 36f);
            var diff = _data.Achievement - _data.HistoryAchievement;
            var diffText = $"{diff.ToString("+0.0000;-0.0000;0", CultureInfo.InvariantCulture)}%";
            var diffColor = diff > 0 ? LightUi.GREEN : diff < 0 ? LightUi.RED : LightUi.MUTED;
            if (_data.HistoryPlayCount <= 0)
            {
                _ui.AddBadge(compareRow, "MAJTEXT_RESULT_FIRST_PLAY".i18n(), LightUi.ACCENT, 18f);
            }
            else
            {
                _ui.AddFittedText(compareRow, "MAJTEXT_RESULT_VS_BEST".i18n(diffText), 21f, diffColor, FontStyles.Bold);
                if (diff > 0)
                {
                    _ui.AddBadge(compareRow, "MAJTEXT_RESULT_NEW_RECORD".i18n(), LightUi.GOLD, 18f);
                }
            }
            cursor += 168f + CARD_GAP;

            // DX 分与星级、进度条
            var dxCard = CreateCard(x, cursor, width, 116f);
            var total = Math.Max(1L, result.TotalDXScore);
            var ratio = Mathf.Clamp01((float)(result.DXScore / (double)total));
            var dxText = _ui.CreateText(dxCard, $"DX  {result.DXScore.ToString(CultureInfo.InvariantCulture)} / {result.TotalDXScore.ToString(CultureInfo.InvariantCulture)}", 26f, LightUi.TEXT, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            LightUi.PlaceRow(dxText.rectTransform, 32f, 16f, 160f, 40f);
            var stars = GetDxStarCount(result.DXScore, result.TotalDXScore);
            if (stars > 0)
            {
                var starText = _ui.CreateText(dxCard, $"✧{stars}", 28f, LightUi.GOLD, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                LightUi.PlaceRow(starText.rectTransform, 32f, 14f, 32f, 42f);
            }
            var track = LightUi.CreateImage(dxCard, LightUi.LINE, 6f);
            LightUi.PlaceRow(track.rectTransform, 32f, 74f, 32f, 12f);
            var fill = LightUi.CreateImage(track.transform, LightUi.GOLD, 6f);
            var fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(ratio, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            cursor += 116f + CARD_GAP;

            // 判定表
            const float ROW_HEIGHT = 36f;
            var tableHeight = 64f + ROW_HEIGHT * (ResultViewData.NOTE_TYPES.Length + 2);
            var judgeCard = CreateCard(x, cursor, width, tableHeight);
            var judgeTitle = _ui.CreateText(judgeCard, "MAJTEXT_RESULT_JUDGE".i18n(), 19f, LightUi.TEXT, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            LightUi.PlaceRow(judgeTitle.rectTransform, 32f, 14f, 32f, 32f);
            var columnColors = new[] { _manager.critColor, _manager.perfectColor, _manager.greatColor, _manager.goodColor, LightUi.MUTED };
            var columnNames = new[] { "CRITICAL", "PERFECT", "GREAT", "GOOD", "MISS" };
            const float LABEL_WIDTH = 130f;
            var columnWidth = (width - 64f - LABEL_WIDTH) / columnNames.Length;
            var tableTop = 52f;
            for (var c = 0; c < columnNames.Length; c++)
            {
                var header = _ui.CreateText(judgeCard, columnNames[c], 16f, columnColors[c], TextAlignmentOptions.Center, FontStyles.Bold);
                LightUi.PlaceTopLeft(header.rectTransform, 32f + LABEL_WIDTH + c * columnWidth, tableTop, columnWidth, ROW_HEIGHT);
            }
            var rowCount = ResultViewData.NOTE_TYPES.Length + 1;
            for (var r = 0; r < rowCount; r++)
            {
                var rowTop = tableTop + ROW_HEIGHT * (r + 1);
                var isTotal = r == rowCount - 1;
                if (isTotal)
                {
                    var line = LightUi.CreateImage(judgeCard, LightUi.LINE, 0f);
                    LightUi.PlaceRow(line.rectTransform, 32f, rowTop, 32f, 1f);
                }
                var label = isTotal ? "MAJTEXT_RESULT_TOTAL".i18n() : ResultViewData.NOTE_TYPE_NAMES[r];
                var labelText = _ui.CreateText(judgeCard, label, 17f, isTotal ? LightUi.TEXT : LightUi.MUTED, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                LightUi.PlaceTopLeft(labelText.rectTransform, 32f, rowTop, LABEL_WIDTH, ROW_HEIGHT);
                for (var c = 0; c < columnNames.Length; c++)
                {
                    var value = _ui.CreateText(judgeCard, _data.JudgeTable[r, c], 19f, LightUi.TEXT, TextAlignmentOptions.Center, isTotal ? FontStyles.Bold : FontStyles.Normal);
                    LightUi.PlaceTopLeft(value.rectTransform, 32f + LABEL_WIDTH + c * columnWidth, rowTop, columnWidth, ROW_HEIGHT);
                }
            }
            cursor += tableHeight + CARD_GAP;

            // FAST / LATE 与平均偏移；判定分布
            const float BOTTOM_HEIGHT = 210f;
            var statsWidth = Mathf.Floor((width - CARD_GAP) * 0.34f);
            var statsCard = CreateCard(x, cursor, statsWidth, BOTTOM_HEIGHT);
            AddStat(statsCard, "FAST", result.Fast.ToString(CultureInfo.InvariantCulture), LightUi.BLUE, 18f);
            AddStat(statsCard, "LATE", result.Late.ToString(CultureInfo.InvariantCulture), LightUi.RED, 78f);
            AddStat(statsCard, "MAJTEXT_RESULT_AVG_OFFSET".i18n(), _data.AverageOffsetText, LightUi.TEXT, 138f);
            var graphCard = CreateCard(x + statsWidth + CARD_GAP, cursor, width - statsWidth - CARD_GAP, BOTTOM_HEIGHT);
            var graphTitle = _ui.CreateText(graphCard, "MAJTEXT_RESULT_DISTRIBUTION".i18n(), 19f, LightUi.TEXT, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            LightUi.PlaceRow(graphTitle.rectTransform, 28f, 14f, 28f, 32f);
            var graph = LightUi.CreateRect("Graph", graphCard).gameObject.AddComponent<RawImage>();
            graph.raycastTarget = false;
            var graphRect = graph.rectTransform;
            graphRect.anchorMin = Vector2.zero;
            graphRect.anchorMax = Vector2.one;
            graphRect.offsetMin = new Vector2(24f, 18f);
            graphRect.offsetMax = new Vector2(-24f, -56f);
            var graphTexture = GetGraphTexture();
            if (graphTexture != null)
            {
                graph.texture = graphTexture;
            }
            else
            {
                graph.color = new Color(1f, 1f, 1f, 0f);
            }
            cursor += BOTTOM_HEIGHT;
            return cursor;
        }
        RectTransform CreateCard(float x, float y, float width, float height)
        {
            var card = LightUi.CreateImage(_content, LightUi.PAPER, 18f);
            LightUi.PlaceTopLeft(card.rectTransform, x, y, width, height);
            return card.rectTransform;
        }
        void AddStat(RectTransform card, string label, string value, Color valueColor, float top)
        {
            var labelText = _ui.CreateText(card, label, 17f, LightUi.MUTED, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            LightUi.PlaceRow(labelText.rectTransform, 28f, top, 28f, 50f);
            var valueText = _ui.CreateText(card, value, 30f, valueColor, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            LightUi.PlaceRow(valueText.rectTransform, 28f, top, 28f, 50f);
        }
        /// <summary>
        /// 判定分布图用原结算界面的绘制方法生成一次，之后重新排版时复用
        /// </summary>
        Texture? GetGraphTexture()
        {
            if (_graphTexture != null)
            {
                return _graphTexture;
            }
            try
            {
                _graphTexture = _manager.DrawNoteJudgeDiffGraph(_data.Result.NoteJudgeDiffs.Span, 260, 900);
            }
            catch (Exception e)
            {
                MajDebug.LogError(e);
                _graphTexture = null;
            }
            return _graphTexture;
        }
        /// <summary>
        /// DX 星级：与原版相同，按 DX 分比例 85% / 90% / 93% / 95% / 97% 分为 1~5 星
        /// </summary>
        static int GetDxStarCount(long dxScore, long totalDxScore)
        {
            if (totalDxScore <= 0)
            {
                return 0;
            }
            var percent = dxScore / (double)totalDxScore;
            return percent switch
            {
                > 0.97 => 5,
                > 0.95 => 4,
                > 0.93 => 3,
                > 0.90 => 2,
                > 0.85 => 1,
                _ => 0,
            };
        }
        #endregion

        #region Dynamic
        void PlayAchievementRoll()
        {
            _rollMotion.TryCancel();
            _rollMotion = LMotion.Create(0d, _data.Achievement, ROLL_DURATION_SEC)
                                 .WithEase(Ease.OutQuart)
                                 .Bind(SetAchievementText);
        }
        void SetAchievementText(double value)
        {
            if (_achievementText != null)
            {
                _achievementText.text = FormatAchievement(value);
            }
        }
        /// <summary>
        /// 与原版相同：截断而不是四舍五入，经典模式两位小数，否则四位
        /// </summary>
        string FormatAchievement(double achievement)
        {
            var precision = _data.IsClassic ? 100d : 10000d;
            var digits = _data.IsClassic ? 2 : 4;
            var truncated = Math.Floor(achievement * precision) / precision;
            return $"{truncated.ToString($"F{digits}", CultureInfo.InvariantCulture)}%";
        }
        /// <summary>
        /// 每帧更新：继续按钮是否可点、收藏状态、点赞与上传的状态文字
        /// </summary>
        void UpdateDynamicParts()
        {
            var canContinue = _manager.IsAllTaskFinished && _manager.IsInited && !_manager.IsExited && !_isLeaving;
            if (_continueButton.interactable != canContinue)
            {
                _continueButton.interactable = canContinue;
            }
            var continueText = _manager.IsAllTaskFinished ? "MAJTEXT_RESULT_CONTINUE".i18n() : "MAJTEXT_RESULT_SAVING".i18n();
            if (_continueLabel.text != continueText)
            {
                _continueLabel.text = continueText;
            }
            UpdateHeart();
            var sender = _manager.OnlineSender;
            if (sender == null)
            {
                return;
            }
            if (_likeButton != null)
            {
                _likeButton.interactable = !sender.IsAlreadyThumbUp;
            }
            if (_likeStatus != null)
            {
                var info = sender.InfoText;
                var likeText = info == _likeInfoText ? string.Empty : info;
                if (_likeStatus.text != likeText)
                {
                    _likeStatus.text = likeText;
                }
            }
            if (_uploadButton != null)
            {
                // 自动上传完成后，只在失败时允许重新上传
                _uploadButton.interactable = !sender.IsScorePosted && _manager.IsAllTaskFinished;
            }
            if (_uploadStatus != null)
            {
                var uploadText = sender.UploadText;
                if (_uploadStatus.text != uploadText)
                {
                    _uploadStatus.text = uploadText;
                }
            }
        }
        void UpdateHeart()
        {
            if (_heart == null)
            {
                return;
            }
            var isFavorite = SongStorage.IsInMyFavorites(_data.Result.SongDetail);
            _heart.sprite = isFavorite ? UiSprites.HeartFilled : UiSprites.HeartOutline;
            _heart.color = isFavorite ? LightUi.FAVORITE : LightUi.MUTED;
        }
        void ToggleFavorite()
        {
            // 走原有的收藏组件，A5 与按钮的状态保持一致
            if (_manager.favoriteAdder != null)
            {
                _manager.favoriteAdder.FavoratePressed();
            }
            UpdateHeart();
        }
        void Continue()
        {
            if (_isLeaving)
            {
                return;
            }
            if (_manager.TryExit())
            {
                _isLeaving = true;
            }
        }
        #endregion
    }
}
