namespace MajdataPlay.Settings;

/// <summary>
/// 分享“最好的若干个”与“全部”时的排序：难度、达成率、DX 分三个关键字的先后顺序，都从大到小
/// </summary>
public enum ScoreShareSortOption
{
    LevelAchievementDx,
    LevelDxAchievement,
    AchievementLevelDx,
    AchievementDxLevel,
    DxLevelAchievement,
    DxAchievementLevel,
}
