using System;

namespace MajdataPlay.Settings.OptionEnumerators;
/// <summary>
/// 动作类选项：按左右键即执行（把属性设为 <see cref="ScoreActionState.Running"/>，由属性的 setter 启动动作），
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
    bool Run()
    {
        if (Equals(Value, ScoreActionState.Running))
        {
            return false;
        }
        Value = ScoreActionState.Running;
        Refresh();
        return true;
    }
}
