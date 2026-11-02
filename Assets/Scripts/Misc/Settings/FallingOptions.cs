#nullable enable
namespace MajdataPlay.Settings
{
    /// <summary>
    /// 下落式的全部设置，属性声明顺序即设置界面中的顺序。属性名沿用旧版，语言文件的键不变
    /// </summary>
    public class FallingOptions
    {
        /// <summary>
        /// 标准或下落式。下落式成绩单独保存在本地，不上传
        /// </summary>
        public PlayStyleOption PlayStyle { get; set; } = PlayStyleOption.Standard;
        /// <summary>
        /// 代替 TapSpeed 决定音符下落快慢，数值含义与 TapSpeed 相同
        /// </summary>
        [Step("0.25")]
        [Range("1", null)]
        public float FallingSpeed { get; set; } = 7.5f;
        /// <summary>
        /// 开启时 Tap/Hold/Break/Flick/Slide 在正确的列上任意高度都算，关闭时需按在判定线附近。悬空 Touch 另见 <see cref="FallingTouchVerticalJudge"/>
        /// </summary>
        public bool FallingVerticalJudge { get; set; } = true;
        /// <summary>
        /// 悬空 Touch 在所在列（左右各多一列）的任意高度都能按；与 <see cref="FallingVerticalJudge"/> 互不影响，地雷 Touch 不受影响
        /// </summary>
        public bool FallingTouchVerticalJudge { get; set; } = false;
        /// <summary>
        /// 扫键：按住的手指划进某一列也算点击这一列，像机台上手划过按键也能打到；关闭时只有新按下才算
        /// </summary>
        public bool FallingSweep { get; set; } = false;

        public bool FallingBreakToFlick { get; set; } = false;
        /// <summary>
        /// 星星尾转 Flick：整条箭头换成一个红色 Flick，在箭头开始移动的时刻落在起点所在的列；<see cref="FallingSlideOriginal"/> 开启时无效
        /// </summary>
        public bool FallingSlideTailToFlick { get; set; } = false;

        public bool FallingTouchToTap { get; set; } = false;
        /// <summary>
        /// 星星保持标准游玩的样式与判定。星星头回到圆形外圈并按外圈判定，Slide 悬空显示在画面中央
        /// </summary>
        public bool FallingSlideOriginal { get; set; } = false;

        public bool FallingLaneDividers { get; set; } = true;
        /// <summary>
        /// 点亮手指所在列（跟着手指走），只在显示轨道分割线时生效
        /// </summary>
        public bool FallingLaneHighlight { get; set; } = false;
        /// <summary>
        /// 信息显示是否跟随“连击/第二信息/副屏信息”等显示设置；不跟随时右上显示分数与达成率、顶部中间显示连击
        /// </summary>
        public bool FallingInfoFollowDisplay { get; set; } = false;

        public bool FallingDifficultyColor { get; set; } = true;

        public bool FallingScoreColor { get; set; } = false;

        public FallingJudgeLineColorOption FallingJudgeLineColor { get; set; } = FallingJudgeLineColorOption.White;
        /// <summary>
        /// 是否开启了改变谱面的选项（Touch 转 Tap、星星尾转 Flick、Slide 保持原样）：与其他 Mod 一样，开启时不保存成绩。
        /// 垂直判定、扫键、Break 转 Flick 等只调手感的选项不算
        /// </summary>
        public bool IsChartModActive()
        {
            return FallingTouchToTap || FallingSlideTailToFlick || FallingSlideOriginal;
        }
    }
}
