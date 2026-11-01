using System;

namespace MajdataPlay.Settings.OptionEnumerators;
/// <summary>
/// 动作类选项：按 + 或 − 即执行（把属性设为 <see cref="ScoreActionState.Running"/>，由属性的 setter 启动动作），
/// 显示的是动作当前的状态
/// </summary>
public sealed class ScoreActionEnumerator : OptionEnumeratorBase, IOptionEnumerator
{
    protected override void InitInternal()
    {
        if (Type != typeof(ScoreActionState))
        {
            throw new InvalidOperationException("Type provided must be ScoreActionState");
        }
        var values = Enum.GetValues(typeof(ScoreActionState));
        OptionValues = new object[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            OptionValues[i] = values.GetValue(i)!;
        }
        InitValueTexts();
        Refresh();
    }
    public override bool MoveNext()
    {
        return Run();
    }
    public override bool MovePrevious()
    {
        return Run();
    }
    /// <summary>
    /// 只在 Ready 时执行：完成或失败后要等状态自动回到 Ready，按住感应区连发时不会马上又执行一次
    /// </summary>
    bool Run()
    {
        if (!Equals(Value, ScoreActionState.Ready))
        {
            return false;
        }
        Value = ScoreActionState.Running;
        Refresh();
        return true;
    }
}
