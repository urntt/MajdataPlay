namespace MajdataPlay.Settings;

public enum ScoreShareScopeOption
{
    /// <summary>
    /// 进入设置前在选歌界面选中的谱面
    /// </summary>
    CurrentChart,
    /// <summary>
    /// 按难度、达成率、DX 分排序后的前若干个
    /// </summary>
    Best,
    All,
    /// <summary>
    /// 在列表式选歌里勾选的谱面（ScoreOptions.ShareSelection）
    /// </summary>
    Custom,
}
