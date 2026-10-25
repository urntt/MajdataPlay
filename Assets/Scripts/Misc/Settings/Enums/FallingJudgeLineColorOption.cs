namespace MajdataPlay.Settings;

/// <summary>
/// 下落式判定线颜色
/// </summary>
public enum FallingJudgeLineColorOption
{
    /// <summary>
    /// 始终白色
    /// </summary>
    White,
    /// <summary>
    /// 保持 AP 时金色，出现 Great 变紫，出现 Good 变绿，出现 Miss 变白
    /// </summary>
    Maimai,
    /// <summary>
    /// 达成率（101% 往下掉）不低于 100.5% 时金色，不低于 100% 时蓝色，否则白色
    /// </summary>
    Phigros,
}
