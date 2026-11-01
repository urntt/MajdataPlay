using System;

namespace MajdataPlay.Settings.OptionEnumerators;
/// <summary>
/// 备份文件：列出备份文件夹里的文件（最新的在前）；没有文件时只有“无”。
/// 当前值不在列表里时（例如刚导出了新文件）重新扫描文件夹
/// </summary>
public sealed class ScoreBackupFileEnumerator : OptionEnumeratorBase, IOptionEnumerator
{
    public const string NONE = "None";

    protected override void InitInternal()
    {
        if (Type != typeof(string))
        {
            throw new InvalidOperationException("Type provided must be String");
        }
        Scan();
        if (!SelectCurrent())
        {
            ValueIndex = 0;
            Value = OptionValues[0];
        }
    }
    public override void Refresh()
    {
        if (!SelectCurrent())
        {
            Scan();
            SelectCurrent();
        }
    }
    void Scan()
    {
        var files = ScoreExchange.ListBackupFiles();
        OptionValues = files.Length == 0 ? new object[] { NONE } : Array.ConvertAll(files, x => (object)x);
        InitValueTexts();
    }
    bool SelectCurrent()
    {
        var value = Value as string;
        for (var i = 0; i < OptionValues.Length; i++)
        {
            if (Equals(OptionValues[i], value))
            {
                ValueIndex = i;
                return true;
            }
        }
        return false;
    }
}
