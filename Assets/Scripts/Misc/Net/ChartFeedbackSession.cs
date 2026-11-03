#nullable enable
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MajdataPlay.Diagnostics;

namespace MajdataPlay.Net
{
    /// <summary>
    /// The outcome of loading or submitting chart feedback.
    /// </summary>
    internal enum ChartFeedbackResult
    {
        /// <summary>
        /// The request succeeded.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The request failed or the server could not be reached.
        /// </summary>
        Failed,

        /// <summary>
        /// The server rejected the login of the account; the player has to log in again.
        /// </summary>
        SessionExpired,
    }

    /// <summary>
    /// Loads the player's vote on an online chart and submits a new vote and a comment, without any UI.
    /// </summary>
    /// <remarks>
    /// Used by the feedback dialog of the song list detail page. It follows the rules of the result screen's
    /// <c>SongFeedbackPanel</c>: a vote is changed with the like, dislike and delete requests of <see cref="Online"/>; when the
    /// outcome of a vote request is unknown, the server's vote is loaded again before the next one; a comment is never sent again
    /// automatically. The methods may continue on a worker thread, so callers switch back to the main thread before touching
    /// Unity objects.
    /// </remarks>
    internal sealed class ChartFeedbackSession
    {
        /// <summary>
        /// The only server that accepts chart feedback.
        /// </summary>
        private const string FEEDBACK_HOST = "majdata.net";

        /// <summary>
        /// The vote that means "like".
        /// </summary>
        public const int LIKE = 1;

        /// <summary>
        /// The vote that means "dislike".
        /// </summary>
        public const int DISLIKE = -1;

        /// <summary>
        /// The vote the server holds as far as this session knows.
        /// </summary>
        private int _submittedVote;

        /// <summary>
        /// Whether the outcome of the last vote request is unknown, so the server's vote has to be loaded again first.
        /// </summary>
        private bool _isVoteUncertain;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChartFeedbackSession"/> class.
        /// </summary>
        /// <param name="song">The chart to give feedback on.</param>
        /// <exception cref="ArgumentNullException"><paramref name="song"/> is null.</exception>
        public ChartFeedbackSession(OnlineSongDetail song)
        {
            Song = song ?? throw new ArgumentNullException(nameof(song));
        }

        /// <summary>
        /// Gets the chart this session gives feedback on.
        /// </summary>
        public OnlineSongDetail Song { get; }

        /// <summary>
        /// Gets a value indicating whether the player's vote was loaded, which is required before voting or submitting.
        /// </summary>
        public bool IsLoaded { get; private set; }

        /// <summary>
        /// Gets the vote the player chose: <see cref="LIKE"/>, <see cref="DISLIKE"/>, or 0 for none.
        /// </summary>
        public int Vote { get; private set; }

        /// <summary>
        /// Determines whether the player can give feedback on a chart: an online chart from majdata.net, on a server that requires
        /// logging in, with a logged-in account.
        /// </summary>
        /// <param name="song">The chart.</param>
        /// <returns><see langword="true"/> when feedback can be given; otherwise <see langword="false"/>.</returns>
        public static bool IsAvailable(ISongDetail? song)
        {
            if (song is not OnlineSongDetail onlineSong)
            {
                return false;
            }
            var server = onlineSong.ServerInfo;
            return string.Equals(server.Url.Host, FEEDBACK_HOST, StringComparison.OrdinalIgnoreCase)
                && server.RuntimeConfig.IsLoggedIn
                && server.RuntimeConfig.AuthMethod != NetAuthMethodOption.None;
        }

        /// <summary>
        /// Chooses a vote, or clears it when the same vote is chosen again. Does nothing before the vote is loaded.
        /// </summary>
        /// <param name="vote"><see cref="LIKE"/> or <see cref="DISLIKE"/>; other values are ignored.</param>
        public void ToggleVote(int vote)
        {
            if (!IsLoaded || (vote != LIKE && vote != DISLIKE))
            {
                return;
            }
            Vote = Vote == vote ? 0 : vote;
        }

        /// <summary>
        /// Loads the player's current vote from the server, bypassing the cache.
        /// </summary>
        /// <param name="token">Cancels the request.</param>
        /// <returns><see cref="ChartFeedbackResult.Succeeded"/> when the vote was loaded; otherwise <see cref="ChartFeedbackResult.Failed"/>.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="token"/> was canceled.</exception>
        public async UniTask<ChartFeedbackResult> LoadAsync(CancellationToken token)
        {
            var serverVote = await TryLoadServerVoteAsync(token);
            if (serverVote is not int vote)
            {
                return ChartFeedbackResult.Failed;
            }
            Vote = vote;
            _submittedVote = vote;
            _isVoteUncertain = false;
            IsLoaded = true;
            return ChartFeedbackResult.Succeeded;
        }

        /// <summary>
        /// Sends the chosen vote when it differs from the server's, then the comment when it is not empty.
        /// </summary>
        /// <param name="comment">The comment to post; leading and trailing white space is removed, and an empty comment is not sent.</param>
        /// <param name="token">Cancels the requests.</param>
        /// <returns>
        /// <see cref="ChartFeedbackResult.Succeeded"/> when everything was sent; <see cref="ChartFeedbackResult.SessionExpired"/> when
        /// the server rejected the login; otherwise <see cref="ChartFeedbackResult.Failed"/>. Nothing is sent before the vote is loaded.
        /// </returns>
        /// <exception cref="OperationCanceledException"><paramref name="token"/> was canceled.</exception>
        public async UniTask<ChartFeedbackResult> SubmitAsync(string comment, CancellationToken token)
        {
            if (!IsLoaded)
            {
                return ChartFeedbackResult.Failed;
            }
            var vote = Vote;
            var trimmedComment = comment.Trim();
            if (_isVoteUncertain)
            {
                // A failed response does not prove that the server rejected the previous vote
                var serverVote = await TryLoadServerVoteAsync(token);
                if (serverVote is not int current)
                {
                    return ChartFeedbackResult.Failed;
                }
                _submittedVote = current;
                _isVoteUncertain = false;
            }
            if (vote != _submittedVote)
            {
                _isVoteUncertain = true;
                var voteResult = await SendAsync(GetVoteRequest(vote), token);
                if (voteResult != ChartFeedbackResult.Succeeded)
                {
                    return voteResult;
                }
                _submittedVote = vote;
                _isVoteUncertain = false;
            }
            if (trimmedComment.Length != 0)
            {
                var commentResult = await SendAsync(Online.PostCommentAsync(Song, trimmedComment, token), token);
                if (commentResult != ChartFeedbackResult.Succeeded)
                {
                    return commentResult;
                }
            }
            return ChartFeedbackResult.Succeeded;

            ValueTask<EndpointResponse> GetVoteRequest(int target)
            {
                return target switch
                {
                    LIKE => Online.PostLikeAsync(Song, token),
                    DISLIKE => Online.PostDislikeAsync(Song, token),
                    _ => _submittedVote == LIKE ? Online.DeleteLikeAsync(Song, token) : Online.DeleteDislikeAsync(Song, token),
                };
            }
        }

        /// <summary>
        /// Loads the vote the server holds for the player.
        /// </summary>
        /// <param name="token">Cancels the request.</param>
        /// <returns>The server's vote, or <see langword="null"/> when it could not be loaded.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="token"/> was canceled.</exception>
        private async UniTask<int?> TryLoadServerVoteAsync(CancellationToken token)
        {
            try
            {
                var interaction = await Online.GetChartInteractAsync(Song, token, forceRefresh: true);
                if (interaction is not MajNetSongInteract current)
                {
                    return null;
                }
                return current.IsLiked ? LIKE : current.IsDisLiked ? DISLIKE : 0;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                MajDebug.LogWarning(nameof(ChartFeedbackSession), $"Failed to load the vote on chart {Song.Id}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Waits for a feedback request and turns its response into a result.
        /// </summary>
        /// <param name="request">The request that was started.</param>
        /// <param name="token">The token the request was started with.</param>
        /// <returns>The result of the request.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="token"/> was canceled.</exception>
        private async UniTask<ChartFeedbackResult> SendAsync(ValueTask<EndpointResponse> request, CancellationToken token)
        {
            try
            {
                var response = await request;
                if (response.IsSuccessfully)
                {
                    return ChartFeedbackResult.Succeeded;
                }
                return response.StatusCode == HttpStatusCode.Unauthorized ? ChartFeedbackResult.SessionExpired : ChartFeedbackResult.Failed;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                MajDebug.LogWarning(nameof(ChartFeedbackSession), $"Failed to send feedback on chart {Song.Id}: {e.Message}");
                return ChartFeedbackResult.Failed;
            }
        }
    }
}
