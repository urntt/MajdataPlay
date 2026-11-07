#nullable enable
using MajdataPlay.Scenes.Game.Notes;

namespace MajdataPlay.Scenes.Game.Lane
{
    /// <summary>
    /// Grades a falling-mode slide from its check points, without any Unity dependency so it can be tested on its own.
    /// </summary>
    /// <remarks>
    /// Two gradings are computed and the better one is used:
    /// <list type="bullet">
    /// <item><description>
    /// By position (<see cref="GetPositionGrade"/>): all points hit is Critical Perfect; first and last hit is Perfect; first hit with
    /// another hit is Great; any hit is Good; none is Miss.
    /// </description></item>
    /// <item><description>
    /// By ratio (<see cref="GetRatioGrade"/>): with x points and y hits, y ≥ max(1, ⌊x⌋) is Critical Perfect, y ≥ max(1, ⌊0.75x⌋) is
    /// Perfect, y ≥ max(1, ⌊0.5x⌋) is Great, y ≥ max(1, ⌊0.25x⌋) is Good, otherwise Miss.
    /// </description></item>
    /// </list>
    /// Slides have no fast or late, so only the late side of <see cref="JudgeGrade"/> (and <see cref="JudgeGrade.Perfect"/>) is used;
    /// on that side a larger value is a better grade.
    /// </remarks>
    internal static class LaneSlideGrading
    {
        /// <summary>
        /// Gets the grade of a slide: the better of the position grade and the ratio grade.
        /// </summary>
        /// <param name="hitCount">The number of check points hit (y).</param>
        /// <param name="totalCount">The number of check points, including the first and the last (x).</param>
        /// <param name="isFirstHit">Whether the first check point was hit.</param>
        /// <param name="isLastHit">Whether the last check point was hit.</param>
        /// <returns>The grade; a slide without check points is Critical Perfect.</returns>
        public static JudgeGrade GetGrade(int hitCount, int totalCount, bool isFirstHit, bool isLastHit)
        {
            var byPosition = GetPositionGrade(hitCount, totalCount, isFirstHit, isLastHit);
            var byRatio = GetRatioGrade(hitCount, totalCount);
            return byRatio > byPosition ? byRatio : byPosition;
        }

        /// <summary>
        /// Gets the grade from which check points were hit (g1).
        /// </summary>
        /// <param name="hitCount">The number of check points hit.</param>
        /// <param name="totalCount">The number of check points.</param>
        /// <param name="isFirstHit">Whether the first check point was hit.</param>
        /// <param name="isLastHit">Whether the last check point was hit.</param>
        /// <returns>The grade; a slide without check points is Critical Perfect.</returns>
        public static JudgeGrade GetPositionGrade(int hitCount, int totalCount, bool isFirstHit, bool isLastHit)
        {
            if (totalCount <= 0 || hitCount >= totalCount)
            {
                return JudgeGrade.Perfect;
            }
            else if (isFirstHit && isLastHit)
            {
                return JudgeGrade.LatePerfect2nd;
            }
            else if (isFirstHit && hitCount >= 2)
            {
                return JudgeGrade.LateGreat;
            }
            else if (hitCount > 0)
            {
                return JudgeGrade.LateGood;
            }
            return JudgeGrade.Miss;
        }

        /// <summary>
        /// Gets the grade from the share of check points hit (g2).
        /// </summary>
        /// <param name="hitCount">The number of check points hit (y).</param>
        /// <param name="totalCount">The number of check points (x).</param>
        /// <returns>The grade; a slide without check points is Critical Perfect.</returns>
        public static JudgeGrade GetRatioGrade(int hitCount, int totalCount)
        {
            if (totalCount <= 0)
            {
                return JudgeGrade.Perfect;
            }
            if (hitCount <= 0)
            {
                return JudgeGrade.Miss;
            }
            if (hitCount >= GetThreshold(totalCount, 4))
            {
                return JudgeGrade.Perfect;
            }
            else if (hitCount >= GetThreshold(totalCount, 3))
            {
                return JudgeGrade.LatePerfect2nd;
            }
            else if (hitCount >= GetThreshold(totalCount, 2))
            {
                return JudgeGrade.LateGreat;
            }
            else if (hitCount >= GetThreshold(totalCount, 1))
            {
                return JudgeGrade.LateGood;
            }
            return JudgeGrade.Miss;
        }

        /// <summary>
        /// Gets max(1, ⌊x × quarters / 4⌋), computed with integers so no rounding error moves a threshold.
        /// </summary>
        /// <param name="totalCount">The number of check points (x).</param>
        /// <param name="quarters">The share in quarters: 4 for 100%, 3 for 75%, 2 for 50%, 1 for 25%.</param>
        /// <returns>The minimum number of hits.</returns>
        private static int GetThreshold(int totalCount, int quarters)
        {
            var threshold = totalCount * quarters / 4;
            return threshold < 1 ? 1 : threshold;
        }
    }
}
