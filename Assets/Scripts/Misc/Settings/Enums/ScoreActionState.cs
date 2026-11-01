namespace MajdataPlay.Settings;

/// <summary>
/// 设置里“分享”“导出备份”“导入备份”等动作的状态；在该行按左右键即执行
/// </summary>
public enum ScoreActionState
{
    Ready,
    Running,
    Done,
    Failed,
}
