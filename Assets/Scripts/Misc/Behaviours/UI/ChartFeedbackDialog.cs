#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MajdataPlay.Diagnostics;
using MajdataPlay.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MajdataPlay
{
    /// <summary>
    /// The touch dialog for rating an online chart: "Fun?" with like and dislike, a comment field, a status line, then Cancel
    /// (or Skip) and Submit. Used by the detail page of the touch song list and by the touch result screen.
    /// </summary>
    /// <remarks>
    /// The owner creates a modal panel of <see cref="WIDTH"/> by <see cref="HEIGHT"/> (see <see cref="LightUi.CreateModalPanel"/>)
    /// and calls <see cref="Show"/>; the dialog lives on the panel and stops its requests when the panel is destroyed. It loads the
    /// player's vote and sends the feedback through <see cref="ChartFeedbackSession"/>, and moves the panel above the on-screen
    /// keyboard while the comment is typed.
    /// </remarks>
    internal sealed class ChartFeedbackDialog : MonoBehaviour
    {
        /// <summary>
        /// The width of the dialog panel.
        /// </summary>
        public const float WIDTH = 560f;
        /// <summary>
        /// The height of the dialog panel.
        /// </summary>
        public const float HEIGHT = 362f;
        /// <summary>
        /// The height of the row holding the question and the vote buttons.
        /// </summary>
        const float ROW_HEIGHT = 58f;
        /// <summary>
        /// The width of the like and dislike buttons.
        /// </summary>
        const float VOTE_WIDTH = 110f;
        /// <summary>
        /// The distance between the dialog edges and its content.
        /// </summary>
        const float PADDING = 28f;
        /// <summary>
        /// The space kept between the bottom of the panel and the top of the on-screen keyboard.
        /// </summary>
        const float KEYBOARD_GAP = 16f;
        /// <summary>
        /// The space kept between the top of the panel and the top of the screen when it moves above the keyboard.
        /// </summary>
        const float TOP_MARGIN = 8f;

        ChartFeedbackSession _session = null!;
        /// <summary>
        /// Called once when the dialog is closed by its buttons: <see langword="true"/> after the feedback was sent,
        /// <see langword="false"/> when Cancel or Skip was tapped.
        /// </summary>
        Action<bool> _onClosed = null!;
        RectTransform _panel = null!;
        Button _likeButton = null!;
        Button _dislikeButton = null!;
        TMP_InputField _input = null!;
        TextMeshProUGUI _status = null!;
        Button _submitButton = null!;
        /// <summary>
        /// Where the owner placed the panel; the panel returns here when the keyboard hides.
        /// </summary>
        Vector2 _restPosition;
        /// <summary>
        /// How far the panel is currently moved up to stay above the keyboard.
        /// </summary>
        float _keyboardOffset;
        /// <summary>
        /// Whether the vote is being loaded or the feedback is being sent.
        /// </summary>
        bool _isBusy;
        /// <summary>
        /// Whether <see cref="_onClosed"/> was called.
        /// </summary>
        bool _isClosed;

        /// <summary>
        /// Gets the chart the dialog rates.
        /// </summary>
        public OnlineSongDetail Song => _session.Song;

        /// <summary>
        /// Builds the dialog inside a modal panel and starts loading the player's vote.
        /// </summary>
        /// <param name="ui">The light UI factory holding the font.</param>
        /// <param name="panel">The panel of <see cref="WIDTH"/> by <see cref="HEIGHT"/>, centered in a full-screen modal layer.</param>
        /// <param name="song">The chart to rate.</param>
        /// <param name="cancelLabel">The label of the left button: Cancel, or Skip when the dialog is the first-play prompt.</param>
        /// <param name="onClosed">
        /// Called once when a button closes the dialog: <see langword="true"/> after the feedback was sent, <see langword="false"/>
        /// for Cancel or Skip. The owner hides the modal then; the dialog does not hide itself.
        /// </param>
        /// <returns>The dialog.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="song"/> or <paramref name="onClosed"/> is null.</exception>
        public static ChartFeedbackDialog Show(LightUi ui, RectTransform panel, OnlineSongDetail song, string cancelLabel, Action<bool> onClosed)
        {
            if (song is null)
            {
                throw new ArgumentNullException(nameof(song));
            }
            if (onClosed is null)
            {
                throw new ArgumentNullException(nameof(onClosed));
            }
            var dialog = panel.gameObject.AddComponent<ChartFeedbackDialog>();
            dialog._session = new ChartFeedbackSession(song);
            dialog._onClosed = onClosed;
            dialog._panel = panel;
            dialog._restPosition = panel.anchoredPosition;
            dialog.Build(ui, cancelLabel);
            dialog.LoadAsync().Forget();
            return dialog;
        }
        /// <summary>
        /// Creates the controls of the dialog.
        /// </summary>
        /// <param name="ui">The light UI factory holding the font.</param>
        /// <param name="cancelLabel">The label of the left button.</param>
        void Build(LightUi ui, string cancelLabel)
        {
            var title = ui.CreateText(_panel, "MAJTEXT_FEEDBACK_OPEN".i18n(), 22f, LightUi.TEXT, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            LightUi.PlaceRow(title.rectTransform, PADDING, 24f, PADDING, 38f);

            // 好玩吗？ 夯 / 拉
            var question = ui.CreateText(_panel, "MAJTEXT_FEEDBACK_QUESTION".i18n(), 19f, LightUi.TEXT, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            LightUi.PlaceTopLeft(question.rectTransform, PADDING, 74f, WIDTH - PADDING * 2f - VOTE_WIDTH * 2f - 12f, ROW_HEIGHT - 8f);
            _dislikeButton = ui.CreateButton(_panel, "MAJTEXT_FEEDBACK_DISLIKE".i18n(), LightUi.PAGE, LightUi.TEXT, 19f, () => ToggleVote(ChartFeedbackSession.DISLIKE));
            LightUi.PlaceTopRight((RectTransform)_dislikeButton.transform, PADDING, 76f, VOTE_WIDTH, ROW_HEIGHT - 12f);
            _likeButton = ui.CreateButton(_panel, "MAJTEXT_FEEDBACK_LIKE".i18n(), LightUi.PAGE, LightUi.TEXT, 19f, () => ToggleVote(ChartFeedbackSession.LIKE));
            LightUi.PlaceTopRight((RectTransform)_likeButton.transform, PADDING + VOTE_WIDTH + 12f, 76f, VOTE_WIDTH, ROW_HEIGHT - 12f);

            // 发送评论：单行输入框；不显示系统自带的输入条，键盘弹出时窗口移到键盘上方
            var commentLabel = ui.CreateText(_panel, "MAJTEXT_FEEDBACK_COMMENT".i18n(), 19f, LightUi.TEXT, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            LightUi.PlaceRow(commentLabel.rectTransform, PADDING, 132f, PADDING, 36f);
            var inputPanel = LightUi.CreateImage(_panel, LightUi.PAGE, 10f);
            LightUi.PlaceRow(inputPanel.rectTransform, PADDING, 172f, PADDING, 56f);
            _input = ui.CreateInputField(inputPanel.transform, "MAJTEXT_FEEDBACK_PLACEHOLDER".i18n());
            _input.gameObject.name = "CommentField";
            _input.shouldHideMobileInput = true;
            LightUi.Stretch((RectTransform)_input.transform);

            _status = ui.CreateText(_panel, string.Empty, 17f, LightUi.MUTED, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            LightUi.PlaceRow(_status.rectTransform, PADDING, 236f, PADDING, 30f);

            var buttonWidth = (WIDTH - PADDING * 2f - 12f) * 0.5f;
            var buttonTop = HEIGHT - 24f - 56f;
            var cancel = ui.CreateButton(_panel, cancelLabel, LightUi.PAGE, LightUi.TEXT, 19f, Cancel);
            LightUi.PlaceTopLeft((RectTransform)cancel.transform, PADDING, buttonTop, buttonWidth, 56f);
            _submitButton = ui.CreateButton(_panel, "MAJTEXT_FEEDBACK_SUBMIT".i18n(), LightUi.ACCENT, Color.white, 19f, Submit);
            LightUi.PlaceTopLeft((RectTransform)_submitButton.transform, PADDING + buttonWidth + 12f, buttonTop, buttonWidth, 56f);
            _submitButton.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        }
        void Update()
        {
            FollowKeyboard();
        }
        /// <summary>
        /// Moves the panel up while the on-screen keyboard covers it during typing, and back down afterwards.
        /// </summary>
        /// <remarks>
        /// Platforms without an on-screen keyboard are not affected. The keyboard height is read every frame because it is 0 while
        /// the keyboard animates in. The panel never moves above the top of the screen.
        /// </remarks>
        void FollowKeyboard()
        {
            var target = 0f;
            var modal = _panel.parent as RectTransform;
            if (modal != null && TouchScreenKeyboard.isSupported && _input.isFocused && TouchScreenKeyboard.visible)
            {
                var keyboardHeight = TouchScreenKeyboard.area.height;
                if (keyboardHeight > 0f &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(modal, new Vector2(0f, keyboardHeight), null, out var keyboardTop))
                {
                    var halfHeight = HEIGHT * _panel.localScale.y * 0.5f;
                    var restCenter = modal.rect.center.y + _restPosition.y;
                    var needed = keyboardTop.y + KEYBOARD_GAP - (restCenter - halfHeight);
                    var highest = modal.rect.yMax - TOP_MARGIN - (restCenter + halfHeight);
                    target = Mathf.Clamp(needed, 0f, Mathf.Max(0f, highest));
                }
            }
            if (Mathf.Abs(target - _keyboardOffset) < 0.5f)
            {
                if (target == _keyboardOffset)
                {
                    return;
                }
                _keyboardOffset = target;
            }
            else
            {
                // 平滑跟随键盘，约 0.1 秒到位
                _keyboardOffset = Mathf.Lerp(_keyboardOffset, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 30f));
            }
            _panel.anchoredPosition = _restPosition + new Vector2(0f, _keyboardOffset);
        }
        /// <summary>
        /// Chooses a vote, or clears it when the chosen vote is tapped again.
        /// </summary>
        /// <param name="vote"><see cref="ChartFeedbackSession.LIKE"/> or <see cref="ChartFeedbackSession.DISLIKE"/>.</param>
        void ToggleVote(int vote)
        {
            if (_isBusy || _isClosed)
            {
                return;
            }
            _session.ToggleVote(vote);
            RefreshControls();
        }
        /// <summary>
        /// Closes the dialog without sending anything.
        /// </summary>
        void Cancel()
        {
            if (_isClosed)
            {
                return;
            }
            _input.DeactivateInputField();
            Close(false);
        }
        /// <summary>
        /// Submits the feedback, or loads the vote again when it could not be loaded.
        /// </summary>
        void Submit()
        {
            if (_isBusy || _isClosed)
            {
                return;
            }
            if (!_session.IsLoaded)
            {
                LoadAsync().Forget();
                return;
            }
            _input.DeactivateInputField();
            SubmitAsync(_input.text).Forget();
        }
        /// <summary>
        /// Calls the owner's close callback once.
        /// </summary>
        /// <param name="isSubmitted">Whether the feedback was sent.</param>
        void Close(bool isSubmitted)
        {
            _isClosed = true;
            _onClosed(isSubmitted);
        }
        /// <summary>
        /// Loads the player's vote and enables the vote buttons and the comment field when it is loaded.
        /// </summary>
        /// <returns>A task that completes when the vote was loaded or the failure is shown.</returns>
        async UniTaskVoid LoadAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();
            SetBusy("MAJTEXT_FEEDBACK_LOADING".i18n());
            var result = ChartFeedbackResult.Failed;
            try
            {
                result = await _session.LoadAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                MajDebug.LogException(nameof(ChartFeedbackDialog), e);
            }
            await UniTask.SwitchToMainThread();
            if (this == null)
            {
                return;
            }
            _isBusy = false;
            SetStatus(result == ChartFeedbackResult.Succeeded ? string.Empty : "MAJTEXT_FEEDBACK_LOAD_FAILED".i18n(), LightUi.RED);
            RefreshControls();
        }
        /// <summary>
        /// Sends the vote and the comment. On success the owner is told to close the dialog; otherwise the dialog stays open with
        /// the reason.
        /// </summary>
        /// <param name="comment">The comment typed in the dialog.</param>
        /// <returns>A task that completes when the feedback was sent or the failure is shown.</returns>
        async UniTaskVoid SubmitAsync(string comment)
        {
            var token = this.GetCancellationTokenOnDestroy();
            SetBusy("MAJTEXT_FEEDBACK_SENDING".i18n());
            var result = ChartFeedbackResult.Failed;
            try
            {
                result = await _session.SubmitAsync(comment, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                MajDebug.LogException(nameof(ChartFeedbackDialog), e);
            }
            await UniTask.SwitchToMainThread();
            if (this == null || _isClosed)
            {
                return;
            }
            _isBusy = false;
            if (result == ChartFeedbackResult.Succeeded)
            {
                Close(true);
                return;
            }
            var reason = result == ChartFeedbackResult.SessionExpired ? "MAJTEXT_LOGIN_SESSION_EXPIRED" : "MAJTEXT_FEEDBACK_FAILED";
            SetStatus(reason.i18n(), LightUi.RED);
            RefreshControls();
        }
        /// <summary>
        /// Shows a progress message and disables the dialog while a request runs.
        /// </summary>
        /// <param name="message">The progress message.</param>
        void SetBusy(string message)
        {
            _isBusy = true;
            SetStatus(message, LightUi.MUTED);
            RefreshControls();
        }
        /// <summary>
        /// Shows a line under the comment field.
        /// </summary>
        /// <param name="message">The text, or an empty string to clear the line.</param>
        /// <param name="color">The text color.</param>
        void SetStatus(string message, Color color)
        {
            _status.text = message;
            _status.color = color;
        }
        /// <summary>
        /// Shows the chosen vote and enables the controls that can be used now.
        /// </summary>
        void RefreshControls()
        {
            var isEditable = _session.IsLoaded && !_isBusy;
            var vote = _session.Vote;
            SetVoteButton(_likeButton, vote == ChartFeedbackSession.LIKE, LightUi.ACCENT, isEditable);
            SetVoteButton(_dislikeButton, vote == ChartFeedbackSession.DISLIKE, LightUi.RED, isEditable);
            _input.interactable = isEditable;
            _submitButton.interactable = !_isBusy;
        }
        /// <summary>
        /// Shows a vote button as chosen (filled with its color and white text) or not chosen.
        /// </summary>
        /// <param name="button">The vote button.</param>
        /// <param name="isChosen">Whether this vote is chosen.</param>
        /// <param name="chosenColor">The fill color while chosen.</param>
        /// <param name="isEditable">Whether the vote can be changed now.</param>
        static void SetVoteButton(Button button, bool isChosen, Color chosenColor, bool isEditable)
        {
            button.interactable = isEditable;
            if (button.targetGraphic is Image background)
            {
                background.color = isChosen ? chosenColor : LightUi.PAGE;
            }
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.color = isChosen ? Color.white : LightUi.TEXT;
            label.fontStyle = isChosen ? FontStyles.Bold : FontStyles.Normal;
        }
    }
}
