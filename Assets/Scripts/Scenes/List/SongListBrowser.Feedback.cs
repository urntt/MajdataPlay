#nullable enable
using Cysharp.Threading.Tasks;
using MajdataPlay.Net;

namespace MajdataPlay.Scenes.List
{
    internal sealed partial class SongListBrowser
    {
        /// <summary>
        /// Opens the feedback dialog of the chart shown on the detail page, see <see cref="ChartFeedbackDialog"/>.
        /// </summary>
        /// <remarks>
        /// It only opens for charts that accept feedback (<see cref="ChartFeedbackSession.IsAvailable"/>).
        /// </remarks>
        void ShowFeedbackDialog()
        {
            if (_detailSong is not OnlineSongDetail song || _isDetailClosing || !ChartFeedbackSession.IsAvailable(song))
            {
                return;
            }
            var panel = CreateModalPanel(ChartFeedbackDialog.WIDTH, ChartFeedbackDialog.HEIGHT);
            ChartFeedbackDialog.Show(new LightUi(_font), panel, song, "MAJTEXT_LIST_CANCEL".i18n(), isSubmitted => OnFeedbackDialogClosed(song, isSubmitted));
        }
        /// <summary>
        /// Closes the feedback dialog; after the feedback was sent, a toast confirms it and the like and comment counts of the
        /// detail page are loaded again.
        /// </summary>
        /// <param name="song">The chart of the dialog.</param>
        /// <param name="isSubmitted">Whether the feedback was sent.</param>
        void OnFeedbackDialogClosed(OnlineSongDetail song, bool isSubmitted)
        {
            HideModal();
            if (!isSubmitted)
            {
                return;
            }
            ShowToast("MAJTEXT_LIST_FEEDBACK_DONE".i18n());
            RefreshDetailInteract(song, this.GetCancellationTokenOnDestroy());
        }
    }
}
