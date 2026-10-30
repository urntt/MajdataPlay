using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine.Scripting;
using Newtonsoft.Json;
using MajdataPlay.Net;
using MajdataPlay.Settings.OptionEnumerators;
using MajdataPlay.Diagnostics;
using System.Runtime.Serialization;
#nullable enable
namespace MajdataPlay.Settings
{
    public class GameSetting
    {
        
        public GameOptions Game { get; init; } = new();
        
        public JudgeOptions Judge { get; init; } = new();
        
        public DisplayOptions Display { get; init; } = new();

        // 属性声明顺序即设置分类顺序：显示 → 渲染 → 音量。
        private RenderingOptions _rendering = new();
        private bool _hasRenderingSettings;
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public RenderingOptions Rendering
        {
            get => _rendering;
            init
            {
                _rendering = value ?? new();
                _hasRenderingSettings = value is not null;
            }
        }
        
        public SoundOptions Audio { get; init; } = new();

        private ModOptions _mod = new();
        private ModOptions? _savedMod;
        [JsonIgnore]
        public ModOptions Mod
        {
            get => _mod;
            init => _mod = value;
        }
        /// <summary>
        /// 调试设置“保存 Mod 设置”开启时，Mod 设置随设置文件保存，读取后恢复；关闭时不写入，每次启动都是默认值
        /// </summary>
        [Preserve]
        [JsonProperty(nameof(Mod), NullValueHandling = NullValueHandling.Ignore, ObjectCreationHandling = ObjectCreationHandling.Replace)]
        private ModOptions? SavedMod
        {
            get => Debug.SaveModSettings ? _mod : null;
            set => _savedMod = value;
        }

        private FallingOptions _falling = new();
        private bool _hasFallingSettings;
        /// <summary>
        /// 下落式的全部设置（含游玩模式），随设置文件保存
        /// </summary>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public FallingOptions Falling
        {
            get => _falling;
            init
            {
                _falling = value ?? new();
                _hasFallingSettings = value is not null;
            }
        }
        
        public DebugOptions Debug { get; init; } = new();
        [HideInSettingUI]
        public OnlineOptions Online { get; init; } = new();
        [HideInSettingUI]
        public IOOptions IO { get; init; } = new();

        [OnDeserialized]
        private void MigrateRenderingSettings(StreamingContext context)
        {
            // 旧版把画质与比例保存在 Display；只在没有新分类时迁移，不能覆盖新配置。
            if (!_hasRenderingSettings)
                _rendering = Display.ReadLegacyRenderingSettings();
            _rendering.MigrateFramePacing(Display.LegacyFPSLimit, Display.LegacyVSync);
            // 旧版的下落式设置分散在 Game 与 Mod（Mod 仅在保存 Mod 设置时存在）；只在没有新分类时迁移
            if (!_hasFallingSettings)
            {
                Game.LegacyFalling?.ForEach(apply => apply(_falling));
                if (Debug.SaveModSettings)
                    _savedMod?.LegacyFalling?.ForEach(apply => apply(_falling));
            }
            // 读取时 Debug 可能晚于 Mod 出现，所以先暂存，全部读完再按开关决定是否恢复
            if (Debug.SaveModSettings && _savedMod is not null)
                _mod = _savedMod;
            _savedMod = null;
        }
        /// <summary>
        /// 是否启用了 Mod；下落式也视为 Mod：成绩不上传，只保存到本地的下落式分区
        /// </summary>
        public bool IsAnyModActive()
        {
            return IsAnyModActiveExceptPlayStyle() || Falling.PlayStyle != PlayStyleOption.Standard;
        }
        /// <summary>
        /// 除游玩模式外是否还启用了其他 Mod，启用时不保存成绩。
        /// 下落式中改变谱面的选项（见 <see cref="FallingOptions.IsChartModActive"/>）也算在内；只调手感的下落式选项不算
        /// </summary>
        public bool IsAnyModActiveExceptPlayStyle()
        {
            return Mod.IsAnyModActiveExceptPlayStyle() ||
                   (Falling.PlayStyle == PlayStyleOption.Falling && Falling.IsChartModActive());
        }
    }
    
    public class GameOptions
    {
#if UNITY_ANDROID || UNITY_IOS
        public const GameplaySubScreenClickBehaviorOption DEFAULT_GameplaySubScreenClickBehavior = GameplaySubScreenClickBehaviorOption.TrackSkip_1_Sec_Delay;
#else
        public const GameplaySubScreenClickBehaviorOption DEFAULT_GameplaySubScreenClickBehavior = GameplaySubScreenClickBehaviorOption.TrackSkip;
#endif

        [Step("0.25")]
        [Range(HasMax = false, HasMin = false)]
        public float TapSpeed { get; set; } = 7.5f;
        
        [Step("0.25")]
        [Range(HasMax = false, HasMin = false)]
        public float TouchSpeed { get; set; } = 7.5f;

        [OptionEnumerator(typeof(GameOffsetEnumerator))]
        public float SlideFadeInOffset { get; set; } = 0f;

        // 下落式设置已移到 GameSetting.Falling：以下只接收旧 settings.json 的键，读取后迁移，不再保存
        [JsonIgnore]
        internal List<System.Action<FallingOptions>>? LegacyFalling { get; private set; }
        [Preserve, JsonProperty("FallingSpeed")]
        private float ReadLegacyFallingSpeed { set => (LegacyFalling ??= new()).Add(o => o.FallingSpeed = value); }
        [Preserve, JsonProperty("FallingLaneDividers")]
        private bool ReadLegacyFallingLaneDividers { set => (LegacyFalling ??= new()).Add(o => o.FallingLaneDividers = value); }
        [Preserve, JsonProperty("FallingSweep")]
        private bool ReadLegacyFallingSweep { set => (LegacyFalling ??= new()).Add(o => o.FallingSweep = value); }
        [Preserve, JsonProperty("FallingInfoFollowDisplay")]
        private bool ReadLegacyFallingInfoFollowDisplay { set => (LegacyFalling ??= new()).Add(o => o.FallingInfoFollowDisplay = value); }
        [Preserve, JsonProperty("FallingDifficultyColor")]
        private bool ReadLegacyFallingDifficultyColor { set => (LegacyFalling ??= new()).Add(o => o.FallingDifficultyColor = value); }
        [Preserve, JsonProperty("FallingScoreColor")]
        private bool ReadLegacyFallingScoreColor { set => (LegacyFalling ??= new()).Add(o => o.FallingScoreColor = value); }
        [Preserve, JsonProperty("FallingJudgeLineColor")]
        private FallingJudgeLineColorOption ReadLegacyFallingJudgeLineColor { set => (LegacyFalling ??= new()).Add(o => o.FallingJudgeLineColor = value); }
        
        [Step("0.05")]
        [Range("0", "1" ,HasMax = true, HasMin = true)]
        public float BackgroundDim { get; set; } = 0.8f;
        
        public bool StarRotation { get; set; } = true;
        
        public BGInfoOption BGInfo { get; set; } = BGInfoOption.Combo;
        public BGInfoOption SecondaryBGInfo { get; set; } = BGInfoOption.None;
        public BGInfoOption SubScreenBGInfo { get; set; } = BGInfoOption.Achievement;
        
        public TopInfoDisplayOption TopInfo { get; set; } = TopInfoDisplayOption.None;
        public bool EnableJudgeTimingGauge { get; set; } = false;

        public bool TrackSkip { get; set; } = true;

        public EnforceGameFailureCondition EnforceGameFailure { get; set; } = EnforceGameFailureCondition.Disabled;
        
        public bool FastRetry { get; set; } = true;

        public bool FastPractice { get; set; } = false;

        public GameplaySubScreenClickBehaviorOption GameplaySubScreenClickBehavior { get; set; } = DEFAULT_GameplaySubScreenClickBehavior;
        
        public MirrorOption Mirror { get; set; } = MirrorOption.Off;
        
        [Step("1")]
        [Range("-7", "7", HasMax = true, HasMin = true)]
        public int Rotation { get; set; } = 0;
        public bool SlideSkipping { get; set; } = true;

        public RandomModeOption Random { get; set; } = RandomModeOption.Disabled;
#if UNITY_ANDROID || UNITY_IOS
        
        public bool ButtonRingForTouch { get; set; } = true;
#endif

#if UNITY_STANDALONE
        
        public RecordModeOption RecordMode { get; set; } = RecordModeOption.Disable;
#endif
        [Step("1")]
        [Range("1", "5", HasMax = true, HasMin = true)]
        public int LeadInTime { get; set; } = 1;

        public bool ManualStartGame { get; set; } = false;
    }
    
    public class JudgeOptions
    {
        
        [OptionEnumerator(typeof(GameOffsetEnumerator))]
        public float AudioOffset { get; set; } = 0f;
        
        [OptionEnumerator(typeof(GameOffsetEnumerator))]
        public float JudgeOffset { get; set; } = 0f;
        
        [OptionEnumerator(typeof(GameOffsetEnumerator))]
        public float AnswerOffset { get; set; } = 0f;
        
        [OptionEnumerator(typeof(GameOffsetEnumerator))]
        public float TouchPanelOffset { get; set; } = 0f;
        
        public JudgeModeOption Mode { get; set; } = JudgeModeOption.Modern;
    }
    
    public class DisplayOptions
    {
        [OptionEnumerator(typeof(LanguageEnumerator))]
        public string Language { get; set; } = "";
        
        [OptionEnumerator(typeof(SkinEnumerator))]
        public string Skin { get; set; } = "default";
        
        public bool DisplayCriticalPerfect { get; set; } = false;
        
        public bool DisplayBreakScore { get; set; } = true;

        
        public JudgeDisplayOption FastLateType { get; set; } = JudgeDisplayOption.Disable;
        
        public JudgeDisplayOption NoteJudgeType { get; set; } = JudgeDisplayOption.All;
        
        public JudgeDisplayOption TouchJudgeType { get; set; } = JudgeDisplayOption.All;
        
        public JudgeDisplayOption SlideJudgeType { get; set; } = JudgeDisplayOption.All;
        
        public JudgeDisplayOption BreakJudgeType { get; set; } = JudgeDisplayOption.All;
        
        public JudgeDisplayOption BreakFastLateType { get; set; } = JudgeDisplayOption.Disable;
        
        public JudgeModeOption SlideSortOrder { get; set; } = JudgeModeOption.Modern;
        /// <summary>
        /// Such like Tap、Star、Hold and Break
        /// </summary>
        
        [Step("0.05")]
        [Range("0", "1", HasMax = true, HasMin = true)]
        public float OuterJudgeDistance { get; set; } = 1f;
        /// <summary>
        /// Such like Touch and TouchHold
        /// </summary>
        
        [Step("0.05")]
        [Range("0", "1", HasMax = true, HasMin = true)]
        public float InnerJudgeDistance { get; set; } = 1f;
        
        public bool DisplayHoldHeadJudgeResult { get; set; } = false;
        
        [Step("0.01")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        public float TapScale { get; set; } = 1f;
        
        [Step("0.01")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        public float HoldScale { get; set; } = 1f;
        
        [Step("0.01")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        public float TouchScale { get; set; } = 1f;
        
        [Step("0.01")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        public float SlideScale { get; set; } = 1f;
        
        public TouchFeedbackLevel TouchFeedback { get; set; } = TouchFeedbackLevel.Outer_Only;
#if UNITY_STANDALONE
        [HideInSettingUI]
        public string Resolution { get; set; } = "1080x1920";
#endif
        
#if UNITY_ANDROID || UNITY_IOS
        public bool MainScreenTransform { get; set; } = true;
#else
        public bool MainScreenTransform { get; set; } = false;
#endif
        
        [Step("0.01")]
        [Range("0.05", "1.5", HasMax = true, HasMin = true)]
        public float MainScreenScale { get; set; } = 1f;
        
        [Step("0.01")]
        [Range("-1", "1", HasMax = true, HasMin = true)]
        public float MainScreenOffset { get; set; } = 1f;
        [HideInSettingUI]
        public float MainScreenCachedScreenCenterY { get; set; } = 960f;
        
        [Step("0.01")]
        [Range("-5", "5", HasMax = true, HasMin = true)]
        public float SubDisplayOffset { get; set; } = 0f;
        
        [Step("0.01")]
        public float SubDisplayScale { get; set; } = 1f;
        public GameplayScreenRotationAngleOption GameplayScreenRotationAngle { get; set; } = GameplayScreenRotationAngleOption.Zero;
        public GameplayOrientationOption GameplayOrientation { get; set; } = GameplayOrientationOption.Portrait;

        // 仅接收旧 settings.json 的键，不作为现行设置暴露，也不重新保存。
        // nullable 区分“旧字段确实存在”和“没有渲染设置”，空配置不能套用旧画质默认值。
        private RenderQualityOption? _legacyRenderQuality;
        private int? _legacyRenderScale;

        [Preserve]
        [JsonProperty("RenderQuality")]
        private RenderQualityOption? LegacyRenderQuality { set => _legacyRenderQuality = value; }

        [Preserve]
        [JsonProperty("RenderScale")]
        private int? LegacyRenderScale { set => _legacyRenderScale = value; }

        internal RenderingOptions ReadLegacyRenderingSettings()
            => RenderingOptions.FromLegacy(_legacyRenderQuality, _legacyRenderScale);
#if UNITY_STANDALONE
        [HideInSettingUI]
        public bool Topmost { get; set; } = false;
#endif
        
        internal int? LegacyFPSLimit { get; private set; }
        internal bool? LegacyVSync { get; private set; }
        [Preserve, JsonProperty("FPSLimit")]
        private int? ReadLegacyFPSLimit { set => LegacyFPSLimit = value; }
        [Preserve, JsonProperty("VSync")]
        private bool? ReadLegacyVSync { set => LegacyVSync = value; }

        public bool SkipVideoDownload { get; set; } = false;
    }
    
    public class SoundOptions
    {
#if UNITY_IOS || UNITY_ANDROID
        readonly static SoundBackendOption DEFAULT_SOUND_BACKEND = SoundBackendOption.BassSimple;
#else
        readonly static SoundBackendOption DEFAULT_SOUND_BACKEND = SoundBackendOption.Wasapi;
#endif
        
        public bool ForceMono { get; set; } = false;
        
        public SFXVolume Volume { get; set; } = new();
#if UNITY_STANDALONE
#if UNITY_STANDALONE_WIN
        public WasapiOptions Wasapi { get; set; } = new();
        
        public AsioOptions Asio { get; set; } = new();
#endif
        public ChannelOptions Channel { get; set; } = new();
#endif
        public BassAudioOptions Bass { get; set; } = new();

        
        public SoundBackendOption Backend { get; set; } = DEFAULT_SOUND_BACKEND;
    }
    
    public class SFXVolume
    {
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        [OptionEnumerator(typeof(AudioVolumeEnumerator))]
        public float Global { get; set; } = 0.3f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        [OptionEnumerator(typeof(AudioVolumeEnumerator))]
        public float BGM { get; set; } = 1f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        [OptionEnumerator(typeof(AudioVolumeEnumerator))]
        public float Track { get; set; } = 1f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        [OptionEnumerator(typeof(AudioVolumeEnumerator))]
        public float Answer { get; set; } = 0.8f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        [OptionEnumerator(typeof(AudioVolumeEnumerator))]
        public float Tap { get; set; } = 0.3f;

        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        [OptionEnumerator(typeof(AudioVolumeEnumerator))]
        public float Ex { get; set; } = 0.3f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        [OptionEnumerator(typeof(AudioVolumeEnumerator))]
        public float Break { get; set; } = 0.3f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        [OptionEnumerator(typeof(AudioVolumeEnumerator))]
        public float Slide { get; set; } = 0.3f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        [OptionEnumerator(typeof(AudioVolumeEnumerator))]
        public float Touch { get; set; } = 0.3f;

        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        [OptionEnumerator(typeof(AudioVolumeEnumerator))]
        public float Hanabi { get; set; } = 0.3f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        [OptionEnumerator(typeof(AudioVolumeEnumerator))]
        public float Voice { get; set; } = 1f;
    }
    
    public class ModOptions
    {
        
        [Step("0.05")]
        [Range("0", null, HasMax = false, HasMin = true)]
        public float PlaybackSpeed { get; set; } = 1f;
        
        public AutoplayModeOption AutoPlay { get; set; } = AutoplayModeOption.Disable;
        
        public JudgeStyleOption JudgeStyle { get; set; } = JudgeStyleOption.DEFAULT;
        
        public bool SubdivideSlideJudgeGrade { get; set; } = false;
        
        public bool AllBreak { get; set; } = false;
        
        public bool AllEx { get; set; } = false;
        
        public bool AllTouch { get; set; } = false;
        
        public bool SlideNoHead { get; set; } = false;
        
        public bool SlideNoTrack { get; set; } = false;
#if !(UNITY_ANDROID || UNITY_IOS)
        
        public bool ButtonRingForTouch { get; set; } = false;
#endif
        
        [OptionEnumerator(typeof(NoteMaskEnumerator))]
        public string NoteMask { get; set; } = "Disable";

        // 游玩模式与下落式 Mod 已移到 GameSetting.Falling：以下只接收开启“保存 Mod 设置”时旧 settings.json 的键，读取后迁移，不再保存
        [JsonIgnore]
        internal List<System.Action<FallingOptions>>? LegacyFalling { get; private set; }
        [Preserve, JsonProperty("PlayStyle")]
        private PlayStyleOption ReadLegacyPlayStyle { set => (LegacyFalling ??= new()).Add(o => o.PlayStyle = value); }
        [Preserve, JsonProperty("FallingVerticalJudge")]
        private bool ReadLegacyFallingVerticalJudge { set => (LegacyFalling ??= new()).Add(o => o.FallingVerticalJudge = value); }
        [Preserve, JsonProperty("FallingBreakToFlick")]
        private bool ReadLegacyFallingBreakToFlick { set => (LegacyFalling ??= new()).Add(o => o.FallingBreakToFlick = value); }
        [Preserve, JsonProperty("FallingTouchToTap")]
        private bool ReadLegacyFallingTouchToTap { set => (LegacyFalling ??= new()).Add(o => o.FallingTouchToTap = value); }
        [Preserve, JsonProperty("FallingSlideOriginal")]
        private bool ReadLegacyFallingSlideOriginal { set => (LegacyFalling ??= new()).Add(o => o.FallingSlideOriginal = value); }

        /// <summary>
        /// 除游玩模式外是否还启用了其他 Mod。含游玩模式与下落式选项的判断见 <see cref="GameSetting.IsAnyModActive"/>、<see cref="GameSetting.IsAnyModActiveExceptPlayStyle"/>
        /// </summary>
        public bool IsAnyModActiveExceptPlayStyle()
        {
            return !(PlaybackSpeed == 1f &&
                !AllBreak && !AllEx && !AllTouch && AutoPlay == AutoplayModeOption.Disable && JudgeStyle == JudgeStyleOption.DEFAULT);
        }

    }
    
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
        [Range("1", null, HasMax = false, HasMin = true)]
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
    
    public class OnlineOptions
    {
        
#if UNITY_IOS
        [JsonIgnore]
#endif
        public bool Enable { get; set; } = false;
#if UNITY_STANDALONE
        public bool UseProxy { get; init; } = true;
        public string Proxy { get; init; } = string.Empty;
#endif
        
        public ApiEndpoint[] ApiEndpoints { get; set; } = new ApiEndpoint[]
        {
#if !UNITY_IOS
            new ApiEndpoint()
            {
                Name = "MajdataNET",
                Url = new("https://majdata.net/api3/api/"),
                Username = "YourUsername",
                Password = "YourPassword"
            }
#endif
        };
    }
    
    public class DebugOptions
    {
        
        public bool DisplaySensor { get; set; } = false;

        private int _sensorOpacity = 20;
        [Step("10")]
        [Range("10", "50", HasMax = true, HasMin = true)]
        public int SensorOpacity
        {
            get => _sensorOpacity;
            // 菜单、外部 JSON 和直接赋值统一为 10/20/30/40/50%，旧配置缺项默认 20%。
            set => _sensorOpacity = value <= 10 ? 10 : value >= 50 ? 50 : ((value + 5) / 10) * 10;
        }

#if UNITY_ANDROID
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        public float TouchSimulationRadius { get; set; } = 0.5f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        public float TouchAAreaExtraRadius { get; set; } = 0f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        public float TouchBAreaExtraRadius { get; set; } = 0f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        public float TouchCAreaExtraRadius { get; set; } = 0.25f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        public float TouchDAreaExtraRadius { get; set; } = 0.2f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        public float TouchEAreaExtraRadius { get; set; } = 0.10f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        public float TouchRadiusAdjust { get; set; } = 0f;
#else
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        public float TouchSimulationRadius { get; set; } = 0.5f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        public float TouchAAreaExtraRadius { get; set; } = 0f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        public float TouchBAreaExtraRadius { get; set; } = 0f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        public float TouchCAreaExtraRadius { get; set; } = 0.25f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        public float TouchDAreaExtraRadius { get; set; } = 0.2f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        public float TouchEAreaExtraRadius { get; set; } = 0.10f;
        
        [Step("0.05")]
        [Range("0", "2", HasMax = true, HasMin = true)]
        public float TouchRadiusAdjust { get; set; } = 0f;
#endif
        
        public bool DisplayRuntimeInfo { get; set; } = true;
#if UNITY_STANDALONE
        [HideInSettingUI]
        public bool FullScreen { get; set; } = true;
#endif
        [HideInSettingUI]
        public int MenuOptionIterationSpeed { get; set; } = 45;
        
        [Range("0", null, HasMax = false, HasMin = true)]
        [OptionEnumerator(typeof(GameOffsetEnumerator))]
        public float DisplayOffset { get; set; } = 0f;
        
        [Step("0.001")]
        public float NoteAppearRate { get; set; } = 0.265f;
        
        public OffsetUnitOption OffsetUnit { get; set; } = OffsetUnitOption.Frame;
#if UNITY_STANDALONE
        [HideInSettingUI]
        public bool HideCursorInGame { get; set; } = true;
#endif
        [HideInSettingUI]
#if UNITY_IOS
        [JsonIgnore]
#endif
        public bool NoteFolding { get; set; } = true;
        
        public DJAutoPolicyOption DJAutoPolicy { get; set; } = DJAutoPolicyOption.Strict;

        /// <summary>
        /// 开启后 Mod 设置随设置文件保存，重启游戏不会重置
        /// </summary>
        public bool SaveModSettings { get; set; } = false;
        [HideInSettingUI]
        public int MaxQueuedFrames { get; set; } = 2;
#if UNITY_IOS || UNITY_ANDROID
        [HideInSettingUI]
        public int TapPoolCapacity { get; set; } = 48;
        [HideInSettingUI]
        public int HoldPoolCapacity { get; set; } = 48;
        [HideInSettingUI]
        public int TouchPoolCapacity { get; set; } = 64;
        [HideInSettingUI]
        public int TouchHoldPoolCapacity { get; set; } = 64;
        [HideInSettingUI]
        public int EachLinePoolCapacity { get; set; } = 24;
#else
        [HideInSettingUI]
        public int TapPoolCapacity { get; set; } = 96;
        [HideInSettingUI]
        public int HoldPoolCapacity { get; set; } = 96;
        [HideInSettingUI]
        public int TouchPoolCapacity { get; set; } = 64;
        [HideInSettingUI]
        public int TouchHoldPoolCapacity { get; set; } = 64;
        [HideInSettingUI]
        public int EachLinePoolCapacity { get; set; } = 48;
#endif
        
        [HideInSettingUI]
#if UNITY_IOS
        [JsonIgnore]
#endif
        public LogLevel DebugLevel { get; set; } = LogLevel.Info;
    }
    
    public class IOOptions
    {
#if UNITY_STANDALONE
        
        public DeviceManufacturerOption? Manufacturer { get; set; } = null;
#endif
        
        public InputDeviceOptions InputDevice { get; set; } = new();
#if UNITY_STANDALONE
        
        public OutputDeviceOptions OutputDevice { get; set; } = new();
#endif
    }
    
    public class InputDeviceOptions
    {
#if UNITY_STANDALONE
        
        public int Player { get; set; } = 1;
        
        public ButtonRingOptions ButtonRing { get; set; } = new();
        
        public TouchPanelOptions TouchPanel { get; set; } = new();
#else
        public MobileExternalButtonRingOption ExternalButtonRing { get; init; } = MobileExternalButtonRingOption.None;
#endif
    }
#if UNITY_STANDALONE
    
    public class OutputDeviceOptions
    {
        
        public LedOptions Led { get; set; } = new();
    }
    
    public class LedOptions
    {
        
        public bool Enable { get; init; } = true;
        
        public float Brightness { get; set; } = 1.0f;
        
        public int RefreshRateMs { get; set; } = 16;
        
        public bool Throttler { get; set; } = true;
        
        public SerialPortOptions SerialPortOptions { get; set; } = new();
        
        public HidOptions HidOptions { get; set; } = new();
    }
    
    public class ButtonRingOptions
    {
        public bool Enable { get; init; } = true;

        public ButtonRingDeviceOption? Type { get; set; } = null;
        
        public bool Debounce { get; set; } = false;
        
        public int PollingRateMs { get; set; } = 0;
        
        public int DebounceThresholdMs { get; set; } = 0;
        
        public HidOptions HidOptions { get; set; } = new();
    }
    
    public class TouchPanelOptions
    {
        public bool Enable { get; init; } = true;

        public bool Debounce { get; set; } = false;
        
        public TouchPanelSensitivityConfig Sensitivities { get; set; } = default;
        
        public int PollingRateMs { get; set; } = 0;
        
        public int DebounceThresholdMs { get; set; } = 0;
        
        public SerialPortOptions SerialPortOptions { get; set; } = new();
        
        public UsbOptions UsbOptions { get; set; } = new();
        
        public CapacitiveTouchPanelOptions CapacitivePanelOptions { get; set; } = new();
    }

    
    public class HidOptions
    {
        
        public string? DeviceName { get; set; } = null;
        
        public int? ProductId { get; set; } = null;
        
        public int? VendorId { get; set; } = null;
        
        public bool Exclusice { get; set; } = false;
        
        public HidOpenPriority OpenPriority { get; set; } = HidOpenPriority.VeryHigh;
    }
    
    public class UsbOptions
    {
        
        public string? DeviceName { get; set; } = null;
        
        public int? ProductId { get; set; } = null;
        
        public int? VendorId { get; set; } = null;
        
        public bool Exclusice { get; set; } = false;
    }
    
    public class CapacitiveTouchPanelOptions
    {
        
        public int TouchRadius { get; set; } = 30;
        public CapacitiveTouchPanelRadiusOffsetConfig RadiusOffset { get; set; } = new()
        {
            A = 0,
            B = 20,
            C = 0,
            D = 0,
            E = 25
        };
    }
    
    public class SerialPortOptions
    {
        
#if UNITY_STANDALONE_WIN
        public int? Port { get; set; } = null;
#else
        public string? Port { get; set; } = null;
#endif
        
        public int? BaudRate { get; set; } = null;
    }
    
    public struct TouchPanelSensitivityConfig
    {
        
        public short A { get; set; }
        
        public short B { get; set; }
        
        public short C { get; set; }
        
        public short D { get; set; }
        
        public short E { get; set; }
    }
    public struct CapacitiveTouchPanelRadiusOffsetConfig
    {

        public int A { get; set; }

        public int B { get; set; }

        public int C { get; set; }

        public int D { get; set; }

        public int E { get; set; }
    }

    public class ChannelOptions
    {
        // Front (LF / RF)
        // Rear (LR / RR)
        // Side (LS / RS) (rear center)
        // CenterAndLFE (LFE / Center)
        public float FrontVolume { get; set; } = 1f;
        public float CenterAndLFEVolume { get; set; } = 1f;
        public float SideVolume { get; set; } = 1f;
        public float RearVolume { get; set; } = 1f;

    }
    public class AsioOptions
    {
        public int DeviceIndex { get; set; } = 0;
        public int SampleRate { get; set; } = 44100;
    }
    public class WasapiOptions
    {
        public bool Exclusive { get; set; } = true;
        public bool RawMode { get; set; } = true;
        public bool AsyncMode { get; set; } = true;
        public float BufferSize { get; set; } = 0.02f;
        public float Period { get; set; } = 0.005f;
    }
#endif
    public class BassAudioOptions
    {
        const int DEFAULT_BufferLengthMs = 1000;
        const int DEFAULT_UpdatePeriodMs = 200;
#if UNITY_ANDROID || UNITY_IOS
        const int DEFAULT_DeviceBufferLengthMs = 32;
        const int DEFAULT_DeviceUpdatePeriodMs = 8;
#else
        const int DEFAULT_DeviceBufferLengthMs = 64;
        const int DEFAULT_DeviceUpdatePeriodMs = 16;
#endif
#if UNITY_ANDROID // Android Only (AAudio)
        public bool EnableAAudio { get; set; } = true;
#endif
        public int BufferLengthMs { get; set; } = DEFAULT_BufferLengthMs;
        public int UpdatePeriodMs { get; set; } = DEFAULT_UpdatePeriodMs;
        public int DeviceBufferLengthMs { get; set; } = DEFAULT_DeviceBufferLengthMs;
        public int DeviceUpdatePeriodMs { get; set; } = DEFAULT_DeviceUpdatePeriodMs;
    }
}
