#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MajdataPlay.Diagnostics;
using MajdataPlay.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MajdataPlay.Scenes.List
{
    internal sealed partial class SongListBrowser
    {
        /// <summary>
        /// The height of the feedback dialog.
        /// </summary>
        const float FEEDBACK_DIALOG_HEIGHT = 362f;
        /// <summary>
        /// The width of the like and dislike buttons in the feedback dialog.
        /// </summary>
        const float FEEDBACK_VOTE_WIDTH = 110f;
        /// <summary>
        /// The color of the dislike button while it is chosen.
        /// </summary>
        static readonly Color DISLIKE_COLOR = Hex(0xD64545);

        /// <summary>
        /// The feedback of the open feedback dialog, or <see langword="null"/> while the dialog is closed.
        /// </summary>
        ChartFeedbackSession? _feedbackSession;
        /// <summary>
        /// Cancels the requests of the feedback dialog when the browser is destroyed. Re-laying out the detail page (for example
        /// after a rotation) does not cancel them; results for a dialog that was closed meanwhile are ignored.
        /// </summary>
        CancellationToken _feedbackToken;
        /// <summary>
        /// Whether the feedback dialog is loading the vote or sending the feedback.
        /// </summary>
        bool _isFeedbackBusy;
        Button _feedbackLikeButton = null!;
        Button _feedbackDislikeButton = null!;
        TMP_InputField _feedbackInput = null!;
        TextMeshProUGUI _feedbackStatus = null!;
        Button _feedbackSubmitButton = null!;

        /// <summary>
        /// Opens the feedback dialog of the chart shown on the detail page and loads the player's vote.
        /// </summary>
        /// <remarks>
        /// The dialog has "Fun?" with the like and dislike buttons, a comment field, a status line, then Cancel and Submit.
        /// It only opens for charts that accept feedback (<see cref="ChartFeedbackSession.IsAvailable"/>).
        /// </remarks>
        void ShowFeedbackDialog()
        {
            if (_detailSong is not OnlineSongDetail song || _isDetailClosing || !ChartFeedbackSession.IsAvailable(song))
            {
                return;
            }
            var panel = CreateModalPanel(SHARE_DIALOG_WIDTH, FEEDBACK_DIALOG_HEIGHT);
            var session = new ChartFeedbackSession(song);
            _feedbackSession = session;
            _feedbackToken = this.GetCancellationTokenOnDestroy();
            _isFeedbackBusy = false;

            var title = CreateText(panel, "MAJTEXT_FEEDBACK_OPEN".i18n(), 22f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            PlaceRow(title.rectTransform, 28f, 24f, 28f, 38f);

            // 好玩吗？ 夯 / 拉
            var question = CreateText(panel, "MAJTEXT_FEEDBACK_QUESTION".i18n(), 19f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            PlaceTopLeft(question.rectTransform, 28f, 74f, SHARE_DIALOG_WIDTH - 28f * 2f - FEEDBACK_VOTE_WIDTH * 2f - 12f, SHARE_ROW_HEIGHT - 8f);
            _feedbackDislikeButton = CreateButton(panel, "MAJTEXT_FEEDBACK_DISLIKE".i18n(), PAGE_COLOR, TEXT_COLOR, 19f, () => ToggleFeedbackVote(ChartFeedbackSession.DISLIKE));
            PlaceTopRight((RectTransform)_feedbackDislikeButton.transform, 28f, 76f, FEEDBACK_VOTE_WIDTH, SHARE_ROW_HEIGHT - 12f);
            _feedbackLikeButton = CreateButton(panel, "MAJTEXT_FEEDBACK_LIKE".i18n(), PAGE_COLOR, TEXT_COLOR, 19f, () => ToggleFeedbackVote(ChartFeedbackSession.LIKE));
            PlaceTopRight((RectTransform)_feedbackLikeButton.transform, 28f + FEEDBACK_VOTE_WIDTH + 12f, 76f, FEEDBACK_VOTE_WIDTH, SHARE_ROW_HEIGHT - 12f);

            // 发送评论：与单行输入框
            var commentLabel = CreateText(panel, "MAJTEXT_FEEDBACK_COMMENT".i18n(), 19f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            PlaceRow(commentLabel.rectTransform, 28f, 132f, 28f, 36f);
            var inputPanel = CreatePanel(panel, PAGE_COLOR, 10f);
            PlaceRow(inputPanel.rectTransform, 28f, 172f, 28f, 56f);
            _feedbackInput = CreateInputField(inputPanel.transform, "MAJTEXT_FEEDBACK_PLACEHOLDER".i18n());
            _feedbackInput.gameObject.name = "CommentField";
            _feedbackInput.richText = false;
            Stretch((RectTransform)_feedbackInput.transform);

            _feedbackStatus = CreateText(panel, string.Empty, 17f, MUTED_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            PlaceRow(_feedbackStatus.rectTransform, 28f, 236f, 28f, 30f);

            var buttonWidth = (SHARE_DIALOG_WIDTH - 28f * 2f - 12f) * 0.5f;
            var buttonTop = FEEDBACK_DIALOG_HEIGHT - 24f - 56f;
            var cancel = CreateButton(panel, "MAJTEXT_LIST_CANCEL".i18n(), PAGE_COLOR, TEXT_COLOR, 19f, HideModal);
            PlaceTopLeft((RectTransform)cancel.transform, 28f, buttonTop, buttonWidth, 56f);
            _feedbackSubmitButton = CreateButton(panel, "MAJTEXT_FEEDBACK_SUBMIT".i18n(), ACCENT_COLOR, Color.white, 19f, SubmitFeedback);
            PlaceTopLeft((RectTransform)_feedbackSubmitButton.transform, 28f + buttonWidth + 12f, buttonTop, buttonWidth, 56f);
            _feedbackSubmitButton.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

            LoadFeedbackAsync(session, _feedbackToken).Forget();
        }
        /// <summary>
        /// Chooses a vote in the feedback dialog, or clears it when the chosen vote is tapped again.
        /// </summary>
        /// <param name="vote"><see cref="ChartFeedbackSession.LIKE"/> or <see cref="ChartFeedbackSession.DISLIKE"/>.</param>
        void ToggleFeedbackVote(int vote)
        {
            var session = _feedbackSession;
            if (session is null || _isFeedbackBusy)
            {
                return;
            }
            session.ToggleVote(vote);
            RefreshFeedbackControls();
        }
        /// <summary>
        /// Submits the feedback, or loads the vote again when it could not be loaded.
        /// </summary>
        void SubmitFeedback()
        {
            var session = _feedbackSession;
            if (session is null || _isFeedbackBusy)
            {
                return;
            }
            if (!session.IsLoaded)
            {
                LoadFeedbackAsync(session, _feedbackToken).Forget();
                return;
            }
            _feedbackInput.DeactivateInputField();
            SubmitFeedbackAsync(session, _feedbackInput.text, _feedbackToken).Forget();
        }
        /// <summary>
        /// Loads the player's vote and enables the vote buttons and the comment field when it is loaded.
        /// </summary>
        /// <param name="session">The feedback of the dialog.</param>
        /// <param name="token">Cancels the request when the browser is destroyed.</param>
        async UniTaskVoid LoadFeedbackAsync(ChartFeedbackSession session, CancellationToken token)
        {
            SetFeedbackBusy("MAJTEXT_FEEDBACK_LOADING".i18n());
            var result = ChartFeedbackResult.Failed;
            try
            {
                result = await session.LoadAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                MajDebug.LogException(nameof(SongListBrowser), e);
            }
            await UniTask.SwitchToMainThread();
            if (!IsFeedbackDialogOf(session))
            {
                return;
            }
            _isFeedbackBusy = false;
            SetFeedbackStatus(result == ChartFeedbackResult.Succeeded ? string.Empty : "MAJTEXT_FEEDBACK_LOAD_FAILED".i18n(), DISLIKE_COLOR);
            RefreshFeedbackControls();
        }
        /// <summary>
        /// Sends the vote and the comment. On success the dialog closes, a toast confirms it, and the like and comment counts of the
        /// detail page are loaded again; otherwise the dialog stays open with the reason.
        /// </summary>
        /// <param name="session">The feedback of the dialog.</param>
        /// <param name="comment">The comment typed in the dialog.</param>
        /// <param name="token">Cancels the requests when the browser is destroyed.</param>
        async UniTaskVoid SubmitFeedbackAsync(ChartFeedbackSession session, string comment, CancellationToken token)
        {
            SetFeedbackBusy("MAJTEXT_FEEDBACK_SENDING".i18n());
            var result = ChartFeedbackResult.Failed;
            try
            {
                result = await session.SubmitAsync(comment, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                MajDebug.LogException(nameof(SongListBrowser), e);
            }
            await UniTask.SwitchToMainThread();
            if (!IsFeedbackDialogOf(session))
            {
                return;
            }
            _isFeedbackBusy = false;
            if (result == ChartFeedbackResult.Succeeded)
            {
                HideModal();
                ShowToast("MAJTEXT_LIST_FEEDBACK_DONE".i18n());
                RefreshDetailInteract(session.Song, token);
                return;
            }
            var reason = result == ChartFeedbackResult.SessionExpired ? "MAJTEXT_LOGIN_SESSION_EXPIRED" : "MAJTEXT_FEEDBACK_FAILED";
            SetFeedbackStatus(reason.i18n(), DISLIKE_COLOR);
            RefreshFeedbackControls();
        }
        /// <summary>
        /// Determines whether the feedback dialog of a session is still open in a living browser.
        /// </summary>
        /// <param name="session">The feedback of the dialog.</param>
        /// <returns><see langword="true"/> when the dialog of <paramref name="session"/> is open; otherwise <see langword="false"/>.</returns>
        bool IsFeedbackDialogOf(ChartFeedbackSession session)
        {
            return this != null && ReferenceEquals(_feedbackSession, session);
        }
        /// <summary>
        /// Shows a progress message and disables the dialog while a request runs.
        /// </summary>
        /// <param name="message">The progress message.</param>
        void SetFeedbackBusy(string message)
        {
            _isFeedbackBusy = true;
            SetFeedbackStatus(message, MUTED_COLOR);
            RefreshFeedbackControls();
        }
        /// <summary>
        /// Shows a line under the comment field.
        /// </summary>
        /// <param name="message">The text, or an empty string to clear the line.</param>
        /// <param name="color">The text color.</param>
        void SetFeedbackStatus(string message, Color color)
        {
            _feedbackStatus.text = message;
            _feedbackStatus.color = color;
        }
        /// <summary>
        /// Shows the chosen vote and enables the controls that can be used now.
        /// </summary>
        void RefreshFeedbackControls()
        {
            var session = _feedbackSession;
            var isEditable = session is not null && session.IsLoaded && !_isFeedbackBusy;
            var vote = session?.Vote ?? 0;
            SetVoteButton(_feedbackLikeButton, vote == ChartFeedbackSession.LIKE, ACCENT_COLOR, isEditable);
            SetVoteButton(_feedbackDislikeButton, vote == ChartFeedbackSession.DISLIKE, DISLIKE_COLOR, isEditable);
            _feedbackInput.interactable = isEditable;
            _feedbackSubmitButton.interactable = !_isFeedbackBusy;
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
                background.color = isChosen ? chosenColor : PAGE_COLOR;
            }
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.color = isChosen ? Color.white : TEXT_COLOR;
            label.fontStyle = isChosen ? FontStyles.Bold : FontStyles.Normal;
        }
    }
}
