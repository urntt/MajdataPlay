using Cysharp.Threading.Tasks;
using MajdataPlay.Diagnostics;
using MajdataPlay.Editor;
using MajdataPlay.IO;
using MajdataPlay.Numerics;
using MajdataPlay.Scenes.Game.Buffers;
using MajdataPlay.Scenes.Game.Lane;
using MajdataPlay.Settings;
using MajdataPlay.UnsafeKit;
using MajdataPlay.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using Unity.IL2CPP.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Profiling;

#nullable enable
namespace MajdataPlay.Scenes.Game.Notes.Controllers
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    internal class NoteManager : MonoBehaviour
    {

        TapUpdater _tapUpdater;
        HoldUpdater _holdUpdater;
        SlideUpdater _slideUpdater;
        TouchUpdater _touchUpdater;
        TouchHoldUpdater _touchHoldUpdater;
        EachLineUpdater _eachLineUpdater;
        SlideOKUpdater _slideOKUpdater;

        readonly int[] _noteCurrentIndex = new int[8];
        readonly int[] _touchCurrentIndex = new int[33];

        [ReadOnlyField]
        [SerializeField]
        double _preUpdateElapsedMs = 0;
        [ReadOnlyField]
        [SerializeField]
        double _updateElapsedMs = 0;
        [ReadOnlyField]
        [SerializeField]
        double _fixedUpdateElapsedMs = 0;
        [ReadOnlyField]
        [SerializeField]
        double _lateUpdateElapsedMs = 0;

        //DJAuto
        readonly SwitchStatus[] _btnStatusInNextFrame = new SwitchStatus[8];
        readonly SwitchStatus[] _btnStatusInThisFrame = new SwitchStatus[8];
        readonly SwitchStatus[] _btnStatusInPreviousFrame = new SwitchStatus[8];
        //DJAuto
        readonly SwitchStatus[] _sensorStatusInNextFrame = new SwitchStatus[33];
        readonly SwitchStatus[] _sensorStatusInThisFrame = new SwitchStatus[33];
        readonly SwitchStatus[] _sensorStatusInPreviousFrame = new SwitchStatus[33];
        
        readonly bool[] _isBtnClickedInThisFrame = new bool[8];
        readonly bool[] _isSensorClickedInThisFrame = new bool[33];

#if UNITY_ANDROID || UNITY_IOS
        readonly int[] _btnClickedCountInThisFrame = new int[8];
        readonly int[] _btnUsedCountInThisFrame = new int[8];

        readonly int[] _sensorClickedCountInThisFrame = new int[33];
        readonly int[] _sensorUsedCountInThisFrame = new int[33];

        static int _defaultButtonStatusUsage = 0;
        static int _defaultSensorStatusUsage = 0;
#else
        readonly bool[] _isBtnUsedInThisFrame = new bool[8];
        readonly bool[] _isSensorUsedInThisFrame = new bool[33];
        static bool _isUseButtonRingForTouch = false;
#endif

        // 下落式模式
        bool _isLaneMode = false;
        readonly LaneInputProvider _laneInput = new();
        readonly bool[] _isPhysicalBtnOnInThisFrame = new bool[8];
        readonly bool[] _isPhysicalBtnClickedInThisFrame = new bool[8];
        readonly bool[] _isPhysicalSensorOnInThisFrame = new bool[33];
        // Slide 保持原样：外圈星星头可用的圆形传感器点击（触摸进入加实体传感器按下）及本帧已用次数
        readonly int[] _laneCircleClickedCount = new int[33];
        readonly int[] _laneCircleUsedCount = new int[33];
        // 每列音符的判定时刻（按队列序号），用于把轨道点击分给离判定时刻最近的音符
        readonly Dictionary<long, float>[] _laneNoteTimings = CreateLaneNoteTimings();
        // 本帧拖动 Slide 用的手指横坐标：轨道触摸加上实体按键按住列的中心
        float[] _laneDragXs = new float[16];
        int _laneDragCount = 0;
        // 扫键：本帧各条进行中星星的当前位置，下一帧交给轨道输入，跟着星星的手指不扫键
        readonly List<float> _pendingLaneSlideXs = new(8);
        float[] _laneSlideXs = new float[8];
        int _laneSlideCount = 0;
        /// <summary>
        /// 两列的音符离判定时刻差不多时（在此范围内），轨道点击优先给手指所在的列
        /// </summary>
        const float LANE_CLICK_PRIMARY_BIAS_SEC = 0.05f;
        /// <summary>
        /// 与 Tap 的 Good 判定区间相同，超出的音符不参与分配
        /// </summary>
        const float LANE_CLICK_CANDIDATE_RANGE_SEC = 0.15f;

        const string SENSOR_IS_NULL = "Sensor index requested by Note is null";
        const string SENSOR_OUT_OF_RANGE = "Sensor index requested by Note is out of range";
        const string BUTTON_IS_NULL = "Button index requested by Note is null";
        const string BUTTON_OUT_OF_RANGE = "Button index requested by Note is out of range";
        readonly bool USERSETTING_IS_AUTOPLAY = (MajEnv.Settings?.Mod.AutoPlay ?? AutoplayModeOption.Disable) != AutoplayModeOption.Disable;

        void Awake()
        {
            Majdata<NoteManager>.Instance = this;

            Array.Fill(_btnStatusInNextFrame, SwitchStatus.Off);
            Array.Fill(_btnStatusInThisFrame, SwitchStatus.Off);
            Array.Fill(_btnStatusInPreviousFrame, SwitchStatus.Off);
            Array.Fill(_sensorStatusInNextFrame, SwitchStatus.Off);
            Array.Fill(_sensorStatusInThisFrame, SwitchStatus.Off);
            Array.Fill(_sensorStatusInPreviousFrame, SwitchStatus.Off);
            Array.Fill(_isBtnClickedInThisFrame, false);
            Array.Fill(_isSensorClickedInThisFrame, false);
#if UNITY_ANDROID || UNITY_IOS
            Array.Fill(_btnUsedCountInThisFrame, 0);
            Array.Fill(_sensorUsedCountInThisFrame, 0);
#else
            Array.Fill(_isBtnUsedInThisFrame, false);
            Array.Fill(_isSensorUsedInThisFrame, false);
#endif
            Array.Fill(_noteCurrentIndex, 0);
            Array.Fill(_touchCurrentIndex, 0);
            //InputManager.BindAnyArea(OnAnyAreaTrigger);
        }
        void Start()
        {
            _tapUpdater = Majdata<TapUpdater>.Instance!;
            _holdUpdater = Majdata<HoldUpdater>.Instance!;
            _slideUpdater = Majdata<SlideUpdater>.Instance!;
            _touchUpdater = Majdata<TouchUpdater>.Instance!;
            _touchHoldUpdater = Majdata<TouchHoldUpdater>.Instance!;
            _eachLineUpdater = Majdata<EachLineUpdater>.Instance!;
            _slideOKUpdater = Majdata<SlideOKUpdater>.Instance!;
#if !(UNITY_ANDROID || UNITY_IOS)
            _isUseButtonRingForTouch = Majdata<INoteController>.Instance?.ModInfo.ButtonRingForTouch ?? false;
#endif
            _isLaneMode = LaneMode.IsEnabled;
        }
        void OnDestroy()
        {
            Majdata<NoteManager>.Free();
        }

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnPreUpdate()
        {
            using (UnityProfiler.Create("NoteManager.OnPreUpdate"))
            {
                for (var i = 0; i < 8; i++)
                {
#if UNITY_ANDROID || UNITY_IOS
                    _btnUsedCountInThisFrame[i] = 0;
                    _btnClickedCountInThisFrame[i] = 0;
#else
                _isBtnUsedInThisFrame[i] = false;
#endif
                    _isBtnClickedInThisFrame[i] = false;
                }
                for (var i = 0; i < 33; i++)
                {
#if UNITY_ANDROID || UNITY_IOS
                    _sensorUsedCountInThisFrame[i] = 0;
                    _sensorClickedCountInThisFrame[i] = 0;
#else
                _isSensorUsedInThisFrame[i] = false;
#endif
                    _isSensorClickedInThisFrame[i] = false;
                }

                if (USERSETTING_IS_AUTOPLAY)
                {
                    AutoplayIOUpdate();
                }
                else
                {
                    GameIOUpdate();
                }
                _tapUpdater.OnPreUpdate();
                _holdUpdater.OnPreUpdate();
                _slideUpdater.OnPreUpdate();
                _touchUpdater.OnPreUpdate();
                _touchHoldUpdater.OnPreUpdate();
                _eachLineUpdater.OnPreUpdate();
#if UNITY_EDITOR || DEBUG
                _preUpdateElapsedMs = 0;
                _preUpdateElapsedMs += _tapUpdater.PreUpdateElapsedMs;
                _preUpdateElapsedMs += _holdUpdater.PreUpdateElapsedMs;
                _preUpdateElapsedMs += _slideUpdater.PreUpdateElapsedMs;
                _preUpdateElapsedMs += _touchUpdater.PreUpdateElapsedMs;
                _preUpdateElapsedMs += _touchHoldUpdater.PreUpdateElapsedMs;
                _preUpdateElapsedMs += _eachLineUpdater.PreUpdateElapsedMs;
#endif
            }
        }
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnUpdate()
        {
            using (UnityProfiler.Create("NoteManager.OnUpdate"))
            {
                _tapUpdater.OnUpdate();
                _holdUpdater.OnUpdate();
                _slideUpdater.OnUpdate();
                _touchUpdater.OnUpdate();
                _touchHoldUpdater.OnUpdate();
                _eachLineUpdater.OnUpdate();
                _slideOKUpdater.OnUpdate();
#if UNITY_EDITOR || DEBUG
                _updateElapsedMs = 0;
                _updateElapsedMs += _tapUpdater.UpdateElapsedMs;
                _updateElapsedMs += _holdUpdater.UpdateElapsedMs;
                _updateElapsedMs += _slideUpdater.UpdateElapsedMs;
                _updateElapsedMs += _touchUpdater.UpdateElapsedMs;
                _updateElapsedMs += _touchHoldUpdater.UpdateElapsedMs;
                _updateElapsedMs += _eachLineUpdater.UpdateElapsedMs;
                _updateElapsedMs += _slideOKUpdater.UpdateElapsedMs;
#endif
            }
        }
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnLateUpdate()
        {
            using (UnityProfiler.Create("NoteManager.OnLateUpdate"))
            {
                _tapUpdater.OnLateUpdate();
                _holdUpdater.OnLateUpdate();
                _slideUpdater.OnLateUpdate();
                _touchUpdater.OnLateUpdate();
                _touchHoldUpdater.OnLateUpdate();
                _eachLineUpdater.OnLateUpdate();
                _slideOKUpdater.OnLateUpdate();
#if UNITY_EDITOR || DEBUG
                _lateUpdateElapsedMs = 0;
                _lateUpdateElapsedMs += _tapUpdater.LateUpdateElapsedMs;
                _lateUpdateElapsedMs += _holdUpdater.LateUpdateElapsedMs;
                _lateUpdateElapsedMs += _slideUpdater.LateUpdateElapsedMs;
                _lateUpdateElapsedMs += _touchUpdater.LateUpdateElapsedMs;
                _lateUpdateElapsedMs += _touchHoldUpdater.LateUpdateElapsedMs;
                _lateUpdateElapsedMs += _eachLineUpdater.LateUpdateElapsedMs;
                _lateUpdateElapsedMs += _slideOKUpdater.LateUpdateElapsedMs;
#endif
            }
        }
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnFixedUpdate()
        {
            using (UnityProfiler.Create("NoteManager.OnFixedUpdate"))
            {
                _tapUpdater.OnFixedUpdate();
                _holdUpdater.OnFixedUpdate();
                _slideUpdater.OnFixedUpdate();
                _touchUpdater.OnFixedUpdate();
                _touchHoldUpdater.OnFixedUpdate();
                _eachLineUpdater.OnFixedUpdate();
#if UNITY_EDITOR || DEBUG
                _fixedUpdateElapsedMs = 0;
                _fixedUpdateElapsedMs += _tapUpdater.FixedUpdateElapsedMs;
                _fixedUpdateElapsedMs += _holdUpdater.FixedUpdateElapsedMs;
                _fixedUpdateElapsedMs += _slideUpdater.FixedUpdateElapsedMs;
                _fixedUpdateElapsedMs += _touchUpdater.FixedUpdateElapsedMs;
                _fixedUpdateElapsedMs += _touchHoldUpdater.FixedUpdateElapsedMs;
                _fixedUpdateElapsedMs += _eachLineUpdater.FixedUpdateElapsedMs;
#endif
            }
        }
        public async UniTask InitAsync()
        {
            _tapUpdater.Init();
            _holdUpdater.Init();
            _slideUpdater.Init();
            _touchUpdater.Init();
            _touchHoldUpdater.Init();
            _eachLineUpdater.Init();
            _slideOKUpdater.Init();
        }
        internal void Clear()
        {
            //_tapUpdater.Clear();
            //_holdUpdater.Clear();
            //_slideUpdater.Clear();
            //_touchUpdater.Clear();
            //_touchHoldUpdater.Clear();
            //_eachLineUpdater.Clear();

            Array.Fill(_btnStatusInNextFrame, SwitchStatus.Off);
            Array.Fill(_btnStatusInThisFrame, SwitchStatus.Off);
            Array.Fill(_btnStatusInPreviousFrame, SwitchStatus.Off);
            Array.Fill(_sensorStatusInNextFrame, SwitchStatus.Off);
            Array.Fill(_sensorStatusInThisFrame, SwitchStatus.Off);
            Array.Fill(_sensorStatusInPreviousFrame, SwitchStatus.Off);
#if UNITY_ANDROID || UNITY_IOS
            Array.Fill(_btnUsedCountInThisFrame, 0);
            Array.Fill(_sensorUsedCountInThisFrame, 0);
#else
            Array.Fill(_isBtnUsedInThisFrame, false);
            Array.Fill(_isSensorUsedInThisFrame, false);
#endif
            Array.Fill(_noteCurrentIndex, 0);
            Array.Fill(_touchCurrentIndex, 0);
            _laneInput.Clear();
            foreach (var timings in _laneNoteTimings)
            {
                timings.Clear();
            }
            _laneDragCount = 0;
            _pendingLaneSlideXs.Clear();
            _laneSlideCount = 0;
        }
        public void ResetCounter()
        {
            for (var i = 0; i < 8; i++)
            {
                _noteCurrentIndex[i] = 0;
            }
            for (var i = 0; i < 33; i++)
            {
                _touchCurrentIndex[i] = 0;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void GameIOUpdate()
        {
            if (_isLaneMode)
            {
                LaneIOUpdate();
                return;
            }
            var previousButtonStatus = InputManager.ButtonStatusInPreviousFrame;
            var currentButtonStatus = InputManager.ButtonStatusInThisFrame;

            var previousSensorStatus = InputManager.SensorStatusInPreviousFrame;
            var currentSensorStatus = InputManager.SensorStatusInThisFrame;

#if UNITY_ANDROID || UNITY_IOS
            var btnClickedCount = InputManager.ButtonClickedCountInThisFrame;
            var sensorClickedCount = InputManager.SensorClickedCountInThisFrame;
            for (var i = 0; i < 8; i++)
            {
                _btnStatusInPreviousFrame[i] = previousButtonStatus[i];
                _btnStatusInThisFrame[i] = currentButtonStatus[i];

                _isBtnClickedInThisFrame[i] = btnClickedCount[i] != 0;
                _btnClickedCountInThisFrame[i] = btnClickedCount[i];
            }
            for (var i = 0; i < 33; i++)
            {
                _sensorStatusInPreviousFrame[i] = previousSensorStatus[i];
                _sensorStatusInThisFrame[i] = currentSensorStatus[i];

                _isSensorClickedInThisFrame[i] = sensorClickedCount[i] != 0;
                _sensorClickedCountInThisFrame[i] = sensorClickedCount[i];
            }
#else
            for (var i = 0; i < 8; i++)
            {
                _btnStatusInPreviousFrame[i] = previousButtonStatus[i];
                _btnStatusInThisFrame[i] = currentButtonStatus[i];
                _isBtnClickedInThisFrame[i] = _btnStatusInPreviousFrame[i] == SwitchStatus.Off &&
                                              _btnStatusInThisFrame[i] == SwitchStatus.On;
            }
            for (var i = 0; i < 33; i++)
            {
                _sensorStatusInPreviousFrame[i] = previousSensorStatus[i];
                _sensorStatusInThisFrame[i] = currentSensorStatus[i];
                if (_isUseButtonRingForTouch && i < 8)
                {
                    _sensorStatusInThisFrame[i] |= _btnStatusInThisFrame[i];
                }
                _isSensorClickedInThisFrame[i] = _sensorStatusInPreviousFrame[i] == SwitchStatus.Off &&
                                                 _sensorStatusInThisFrame[i] == SwitchStatus.On;
            }
#endif
        }
        /// <summary>
        /// 下落式模式：实体按键（键盘、按键环、手柄）与轨道触摸合并为按键状态，悬空 Touch 区合并为传感器状态。
        /// 轨道触摸的点击不计入按键点击次数，由 <see cref="TryUseLaneClick"/> 另行分配，
        /// 因为一次按下可以打所在列或相邻列，但只能用一次。
        /// 屏幕触摸到圆形传感器的映射已由 InputManager.SuppressTouchPanelSimulation 关闭。
        /// </summary>
        void LaneIOUpdate()
        {
            // 上一帧星星报告的位置用于本帧的扫键判断，晚一帧可以忽略
            if (_laneSlideXs.Length < _pendingLaneSlideXs.Count)
            {
                _laneSlideXs = new float[_pendingLaneSlideXs.Count * 2];
            }
            _pendingLaneSlideXs.CopyTo(_laneSlideXs);
            _laneSlideCount = _pendingLaneSlideXs.Count;
            _pendingLaneSlideXs.Clear();
            _laneInput.Update(SceneSwitcher.MainCamera, InputManager.SubScreenEdge, new ReadOnlySpan<float>(_laneSlideXs, 0, _laneSlideCount));

            var previousButtonStatus = InputManager.ButtonStatusInPreviousFrame;
            var currentButtonStatus = InputManager.ButtonStatusInThisFrame;
            var previousSensorStatus = InputManager.SensorStatusInPreviousFrame;
            var currentSensorStatus = InputManager.SensorStatusInThisFrame;
            var laneHeld = _laneInput.LaneHeld;
            var floatSensorHeld = _laneInput.FloatSensorHeld;
            var floatSensorClickedCount = _laneInput.FloatSensorClickedCount;
#if UNITY_ANDROID || UNITY_IOS
            var btnClickedCount = InputManager.ButtonClickedCountInThisFrame;
            var sensorClickedCount = InputManager.SensorClickedCountInThisFrame;
#endif
            for (var i = 0; i < 8; i++)
            {
                var isPhysicalOn = currentButtonStatus[i] == SwitchStatus.On;
#if UNITY_ANDROID || UNITY_IOS
                var physicalClickedCount = btnClickedCount[i];
#else
                var physicalClickedCount = previousButtonStatus[i] == SwitchStatus.Off && isPhysicalOn ? 1 : 0;
#endif
                _isPhysicalBtnOnInThisFrame[i] = isPhysicalOn;
                _isPhysicalBtnClickedInThisFrame[i] = physicalClickedCount != 0;

                _btnStatusInPreviousFrame[i] = _btnStatusInThisFrame[i];
                _btnStatusInThisFrame[i] = isPhysicalOn || laneHeld[i] ? SwitchStatus.On : SwitchStatus.Off;
                _isBtnClickedInThisFrame[i] = physicalClickedCount != 0;
#if UNITY_ANDROID || UNITY_IOS
                _btnClickedCountInThisFrame[i] = physicalClickedCount;
#endif
            }
            UpdateLaneDragXs();
            for (var i = 0; i < 33; i++)
            {
                var isPhysicalOn = currentSensorStatus[i] == SwitchStatus.On;
#if UNITY_ANDROID || UNITY_IOS
                var physicalClickedCount = sensorClickedCount[i];
#else
                var physicalClickedCount = previousSensorStatus[i] == SwitchStatus.Off && isPhysicalOn ? 1 : 0;
                if (_isUseButtonRingForTouch && i < 8)
                {
                    isPhysicalOn |= _isPhysicalBtnOnInThisFrame[i];
                    physicalClickedCount += _isPhysicalBtnClickedInThisFrame[i] ? 1 : 0;
                }
#endif
                var clickedCount = physicalClickedCount + floatSensorClickedCount[i];

                _sensorStatusInPreviousFrame[i] = _sensorStatusInThisFrame[i];
                _isPhysicalSensorOnInThisFrame[i] = isPhysicalOn;
                _laneCircleClickedCount[i] = physicalClickedCount + _laneInput.CircleSensorClickedCount[i];
                _laneCircleUsedCount[i] = 0;
                _sensorStatusInThisFrame[i] = isPhysicalOn || floatSensorHeld[i] ? SwitchStatus.On : SwitchStatus.Off;
                _isSensorClickedInThisFrame[i] = clickedCount != 0;
#if UNITY_ANDROID || UNITY_IOS
                _sensorClickedCountInThisFrame[i] = clickedCount;
#endif
            }
        }
        /// <summary>
        /// 下落式暂停恢复时调用：当前按住屏幕的手指在抬起前都不参与判定
        /// </summary>
        internal void IgnoreActiveLanePointers()
        {
            _laneInput.IgnoreActivePointers();
        }
        /// <summary>
        /// 下落式 Slide：本帧手指的横坐标（世界坐标）。实体按键按住某列时视为手指在该列中心
        /// </summary>
        public ReadOnlySpan<float> LaneDragXs => new ReadOnlySpan<float>(_laneDragXs, 0, _laneDragCount);
        void UpdateLaneDragXs()
        {
            var touchXs = _laneInput.DragXs;
            var capacity = touchXs.Length + LaneLayout.LANE_COUNT;
            if (_laneDragXs.Length < capacity)
            {
                _laneDragXs = new float[capacity * 2];
            }
            touchXs.CopyTo(_laneDragXs);
            _laneDragCount = touchXs.Length;
            for (var lane = 1; lane <= LaneLayout.LANE_COUNT; lane++)
            {
                if (_isPhysicalBtnOnInThisFrame[lane - 1])
                {
                    _laneDragXs[_laneDragCount++] = LaneLayout.GetLaneX(lane);
                }
            }
        }
        /// <summary>
        /// 下落式扫键：进行中的星星报告当前位置，在它附近的手指划过的列不算扫键
        /// </summary>
        public void AddLaneSlideX(float x)
        {
            _pendingLaneSlideXs.Add(x);
        }
        static Dictionary<long, float>[] CreateLaneNoteTimings()
        {
            var timings = new Dictionary<long, float>[LaneLayout.LANE_COUNT];
            for (var i = 0; i < timings.Length; i++)
            {
                timings[i] = new Dictionary<long, float>();
            }
            return timings;
        }
        /// <summary>
        /// 下落式：登记可以用轨道点击判定的 Tap/Hold 的判定时刻，用于把点击分给离判定时刻最近的音符
        /// </summary>
        public void RegisterLaneNote(TapQueueInfo queueInfo, float judgeTiming)
        {
            var keyIndex = queueInfo.KeyIndex - 1;
            if (!keyIndex.InRange(0, LaneLayout.LANE_COUNT - 1))
            {
                return;
            }
            _laneNoteTimings[keyIndex][queueInfo.Index] = judgeTiming;
        }
        /// <summary>
        /// 该列当前待判定的音符离 <paramref name="now"/> 的时间差；没有或不在判定区间内时返回 false
        /// </summary>
        /// <param name="lane">1~8</param>
        bool TryGetLaneCandidateDiff(int lane, float now, out float diff)
        {
            diff = 0f;
            if (lane is < 1 or > LaneLayout.LANE_COUNT)
            {
                return false;
            }
            if (!_laneNoteTimings[lane - 1].TryGetValue(_noteCurrentIndex[lane - 1], out var timing))
            {
                return false;
            }
            diff = MathF.Abs(now - timing);
            return diff <= LANE_CLICK_CANDIDATE_RANGE_SEC;
        }
        /// <param name="excludeIndex">不计入的按下</param>
        bool HasOtherUnusedLaneClick(int lane, int excludeIndex)
        {
            for (var i = 0; i < _laneInput.ClickCount; i++)
            {
                if (i == excludeIndex)
                {
                    continue;
                }
                var click = _laneInput.GetClick(i);
                if (!click.IsUsed && (click.Lane == lane || click.NeighborLane == lane))
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 下落式：为 <paramref name="lane"/> 上判定时刻为 <paramref name="judgeTiming"/> 的音符使用一次轨道点击。
        /// <para>一次按下可以打手指所在列或相邻列，给两列中离判定时刻更近的音符；差不多时优先给手指所在列。
        /// 另一列另有按下可用时不替它保留。同一列有多个按下可选时，优先用另一列不需要的那个。</para>
        /// </summary>
        /// <param name="lane">1~8</param>
        public bool TryUseLaneClick(int lane, float judgeTiming, float now)
        {
            if (!_isLaneMode)
            {
                return false;
            }
            var myDiff = MathF.Abs(now - judgeTiming);
            var bestIndex = -1;
            var bestRank = int.MaxValue;
            for (var i = 0; i < _laneInput.ClickCount; i++)
            {
                var click = _laneInput.GetClick(i);
                if (click.IsUsed)
                {
                    continue;
                }
                var isPrimary = click.Lane == lane;
                if (!isPrimary && click.NeighborLane != lane)
                {
                    continue;
                }
                var otherLane = isPrimary ? click.NeighborLane : click.Lane;
                var isOtherWanting = TryGetLaneCandidateDiff(otherLane, now, out var otherDiff);
                if (isOtherWanting)
                {
                    // 另一列是手指所在列时，它在差不多的时间内就优先；否则另一列要明显更近才让给它
                    var isOtherBetter = isPrimary ? otherDiff + LANE_CLICK_PRIMARY_BIAS_SEC < myDiff
                                                  : otherDiff <= myDiff + LANE_CLICK_PRIMARY_BIAS_SEC;
                    if (isOtherBetter && !HasOtherUnusedLaneClick(otherLane, i))
                    {
                        continue;
                    }
                }
                var rank = (isPrimary ? 0 : 2) + (isOtherWanting ? 1 : 0);
                if (rank < bestRank)
                {
                    bestRank = rank;
                    bestIndex = i;
                    if (rank == 0)
                    {
                        break;
                    }
                }
            }
            if (bestIndex == -1)
            {
                return false;
            }
            _laneInput.UseClick(bestIndex);
            return true;
        }
        /// <summary>
        /// 下落式 Slide 保持原样：按标准圆形传感器划分的按住状态（实体传感器也算），与标准游玩的判定相同
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SwitchStatus GetLaneCircleSensorStatusInThisFrame(SensorArea area)
        {
            var index = (int)area;
            if (index is < 0 or >= 33)
            {
                return SwitchStatus.Off;
            }
            return _isPhysicalSensorOnInThisFrame[index] || _laneInput.CircleSensorHeld[index] ? SwitchStatus.On : SwitchStatus.Off;
        }
        /// <summary>
        /// 下落式 Slide 保持原样：外圈的星星头使用一次圆形传感器点击（手指进入该传感器，或实体传感器按下）
        /// </summary>
        public bool TryUseLaneCircleSensorClick(SensorArea area)
        {
            var index = (int)area;
            if (index is < 0 or >= 33 || _laneCircleUsedCount[index] >= _laneCircleClickedCount[index])
            {
                return false;
            }
            _laneCircleUsedCount[index]++;
            return true;
        }
        /// <summary>
        /// 下落式 Slide（Drag）：判定区对应的任一轨道被按住即视为 On，不需要点击。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SwitchStatus GetLaneDragStatusInThisFrame(SensorArea area)
        {
            for (var lane = 1; lane <= LaneLayout.LANE_COUNT; lane++)
            {
                if (!LaneLayout.IsSensorInLane(area, lane))
                {
                    continue;
                }
                if (_btnStatusInThisFrame[lane - 1] == SwitchStatus.On)
                {
                    return SwitchStatus.On;
                }
            }
            return SwitchStatus.Off;
        }
        /// <summary>
        /// 下落式 Flick：触屏划动，或实体按键按下（键盘无法划动）
        /// </summary>
        /// <param name="lane">1~8</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsLaneFlickedInThisFrame(int lane)
        {
            if (lane is < 1 or > LaneLayout.LANE_COUNT)
            {
                return false;
            }
            return _laneInput.IsFlicked(lane) || _isPhysicalBtnClickedInThisFrame[lane - 1];
        }
        /// <param name="lane">1~8</param>
        public bool TryUseLaneFlick(int lane)
        {
            if (lane is < 1 or > LaneLayout.LANE_COUNT)
            {
                return false;
            }
            if (_laneInput.TryUseFlick(lane))
            {
                return true;
            }
            var zone = (ButtonZone)(lane - 1);
            return _isPhysicalBtnClickedInThisFrame[lane - 1] && TryUseButtonClickEvent(zone);
        }
        void AutoplayIOUpdate()
        {
            for (var i = 0; i < 8; i++)
            {
                var btnState = _btnStatusInNextFrame[i];
                
                _btnStatusInPreviousFrame[i] = _btnStatusInThisFrame[i];
                _btnStatusInThisFrame[i] = btnState;
                _btnStatusInNextFrame[i] = SwitchStatus.Off;
                
#if UNITY_ANDROID || UNITY_IOS
                var isClicked = _btnStatusInPreviousFrame[i] == SwitchStatus.Off &&
                                _btnStatusInThisFrame[i] == SwitchStatus.On;
                _isBtnClickedInThisFrame[i] |= isClicked;
                _btnClickedCountInThisFrame[i] += Convert.ToInt32(_isBtnClickedInThisFrame[i]);
#else
                _isBtnClickedInThisFrame[i] = _btnStatusInPreviousFrame[i] == SwitchStatus.Off &&
                                              _btnStatusInThisFrame[i] == SwitchStatus.On;
#endif
            }
            for (var i = 0; i < 33; i++)
            {
                var senState = _sensorStatusInNextFrame[i];
#if !(UNITY_ANDROID || UNITY_IOS)
                if (_isUseButtonRingForTouch && i < 8)
                {
                    senState |= _btnStatusInThisFrame[i];
                }
#endif
                _sensorStatusInPreviousFrame[i] = _sensorStatusInThisFrame[i];
                _sensorStatusInThisFrame[i] = senState;
                _sensorStatusInNextFrame[i] = SwitchStatus.Off;

#if UNITY_ANDROID || UNITY_IOS
                var isClicked = _sensorStatusInPreviousFrame[i] == SwitchStatus.Off &&
                                _sensorStatusInThisFrame[i] == SwitchStatus.On;
                _isSensorClickedInThisFrame[i] |= isClicked;
                _sensorClickedCountInThisFrame[i] += Convert.ToInt32(isClicked);
#else
                _isSensorClickedInThisFrame[i] = _sensorStatusInPreviousFrame[i] == SwitchStatus.Off &&
                                                 _sensorStatusInThisFrame[i] == SwitchStatus.On;
#endif
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCurrentNoteJudgeable(in TapQueueInfo queueInfo)
        {
            var keyIndex = queueInfo.KeyIndex - 1;
            if (!keyIndex.InRange(0, 7))
            {
                return false;
            }

            var currentIndex = _noteCurrentIndex[keyIndex];
            var index = queueInfo.Index;

            return index <= currentIndex;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCurrentNoteJudgeable(in TouchQueueInfo queueInfo)
        {
            var sensorPos = queueInfo.SensorPos;
            if (sensorPos > SensorArea.E8 || sensorPos < SensorArea.A1)
            {
                return false;
            }
            var pos = (int)sensorPos;
            var index = queueInfo.Index;
            var currentIndex = _touchCurrentIndex[pos];

            return index <= currentIndex;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void NextNote(in TapQueueInfo queueInfo)
        {
            var keyIndex = queueInfo.KeyIndex - 1;
            if (!keyIndex.InRange(0, 7))
            {
                return;
            }
            ref var currentIndex = ref _noteCurrentIndex[keyIndex];

            currentIndex++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void NextTouch(in TouchQueueInfo queueInfo)
        {
            var sensorPos = queueInfo.SensorPos;
            if (sensorPos > SensorArea.E8 || sensorPos < SensorArea.A1)
            {
                return;
            }
            var pos = (int)sensorPos;
            ref var currentIndex = ref _touchCurrentIndex[pos];

            currentIndex++;
        }
#if UNITY_ANDROID || UNITY_IOS
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryUseButtonClickEvent(ButtonZone? zone)
        {
            if (zone is null)
            {
                MajDebug.LogWarning(BUTTON_IS_NULL);
                return default;
            }
            else if (zone < ButtonZone.A1 || zone > ButtonZone.A8)
            {
                MajDebug.LogWarning(BUTTON_OUT_OF_RANGE);
                return default;
            }
            ref var btnUsedCount = ref _btnUsedCountInThisFrame[(int)zone];
            var btnClickedCount = _btnClickedCountInThisFrame[(int)zone];

            if (btnUsedCount >= btnClickedCount)
            {
                return false;
            }
            btnUsedCount++;
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryUseSensorClickEvent(SensorArea? area)
        {
            if (area is null)
            {
                MajDebug.LogWarning(SENSOR_IS_NULL);
                return false;
            }
            else if (area < SensorArea.A1 || area > SensorArea.E8)
            {
                MajDebug.LogWarning(SENSOR_OUT_OF_RANGE);
                return false;
            }
            ref var sensorUsedCount = ref _sensorUsedCountInThisFrame[(int)area];
            var sensorClickedCount = _sensorClickedCountInThisFrame[(int)area];

            if (sensorUsedCount >= sensorClickedCount)
            {
                return false;
            }
            sensorUsedCount++;
            return true;
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryUseButtonClickEvent(ButtonZone? zone)
        {
            if (zone is null)
            {
                MajDebug.LogWarning(BUTTON_IS_NULL);
                return default;
            }
            else if (zone < ButtonZone.A1 || zone > ButtonZone.A8)
            {
                MajDebug.LogWarning(BUTTON_OUT_OF_RANGE);
                return default;
            }
            ref var isUsed = ref _isBtnUsedInThisFrame[(int)zone];
            if (isUsed)
            {
                return false;
            }
            isUsed = true;
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryUseSensorClickEvent(SensorArea? area)
        {
            if (area is null)
            {
                MajDebug.LogWarning(SENSOR_IS_NULL);
                return false;
            }
            else if (area < SensorArea.A1 || area > SensorArea.E8)
            {
                MajDebug.LogWarning(SENSOR_OUT_OF_RANGE);
                return false;
            }
            ref var isUsed = ref _isSensorUsedInThisFrame[(int)area];
            if (isUsed)
            {
                return false;
            }
            isUsed = true;
            return true;
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckSensorStatusInThisFrame(SensorArea? area, SwitchStatus targetState)
        {
            if (area is null)
            {
                MajDebug.LogWarning(SENSOR_IS_NULL);
                return default;
            }
            else if (area < SensorArea.A1 || area > SensorArea.E8)
            {
                MajDebug.LogWarning(SENSOR_OUT_OF_RANGE);
                return default;
            }

            return _sensorStatusInThisFrame[(int)area] == targetState;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckButtonStatusInThisFrame(ButtonZone? zone, SwitchStatus targetState)
        {
            if (zone is null)
            {
                MajDebug.LogWarning(BUTTON_IS_NULL);
                return default;
            }
            else if (zone < ButtonZone.A1 || zone > ButtonZone.A8)
            {
                MajDebug.LogWarning(BUTTON_OUT_OF_RANGE);
                return default;
            }

            return _btnStatusInThisFrame[(int)zone] == targetState;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckButtonStatusInPreviousFrame(ButtonZone? zone, SwitchStatus targetState)
        {
            if (zone is null)
            {
                MajDebug.LogWarning(BUTTON_IS_NULL);
                return default;
            }
            else if (zone < ButtonZone.A1 || zone > ButtonZone.A8)
            {
                MajDebug.LogWarning(BUTTON_OUT_OF_RANGE);
                return default;
            }

            return _btnStatusInPreviousFrame[(int)zone] == targetState;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckSensorStatusInPreviousFrame(SensorArea? area, SwitchStatus targetState)
        {
            if (area is null)
            {
                MajDebug.LogWarning(SENSOR_IS_NULL);
                return default;
            }
            else if (area < SensorArea.A1 || area > SensorArea.E8)
            {
                MajDebug.LogWarning(SENSOR_OUT_OF_RANGE);
                return default;
            }

            return _sensorStatusInPreviousFrame[(int)area] == targetState;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SwitchStatus GetButtonStatusInThisFrame(ButtonZone? zone)
        {
            if (zone is null)
            {
                MajDebug.LogWarning(BUTTON_IS_NULL);
                return default;
            }
            else if (zone < ButtonZone.A1 || zone > ButtonZone.A8)
            {
                MajDebug.LogWarning(BUTTON_OUT_OF_RANGE);
                return default;
            }

            return _btnStatusInThisFrame[(int)zone];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SwitchStatus GetButtonStatusInPreviousFrame(ButtonZone? zone)
        {
            if (zone is null)
            {
                MajDebug.LogWarning(BUTTON_IS_NULL);
                return default;
            }
            else if (zone < ButtonZone.A1 || zone > ButtonZone.A8)
            {
                MajDebug.LogWarning(BUTTON_OUT_OF_RANGE);
                return default;
            }

            return _btnStatusInPreviousFrame[(int)zone];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SwitchStatus GetSensorStatusInThisFrame(SensorArea? area)
        {
            if (area is null)
            {
                MajDebug.LogWarning(SENSOR_IS_NULL);
                return default;
            }
            else if (area < SensorArea.A1 || area > SensorArea.E8)
            {
                MajDebug.LogWarning(SENSOR_OUT_OF_RANGE);
                return default;
            }

            return _sensorStatusInThisFrame[(int)area];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SwitchStatus GetSensorStatusInPreviousFrame(SensorArea? area)
        {
            if (area is null)
            {
                MajDebug.LogWarning(SENSOR_IS_NULL);
                return default;
            }
            else if (area < SensorArea.A1 || area > SensorArea.E8)
            {
                MajDebug.LogWarning(SENSOR_OUT_OF_RANGE);
                return default;
            }

            return _sensorStatusInPreviousFrame[(int)area];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsButtonClickedInThisFrame(ButtonZone? zone)
        {
            if (zone is null)
            {
                MajDebug.LogWarning(BUTTON_IS_NULL);
                return default;
            }
            else if (zone < ButtonZone.A1 || zone > ButtonZone.A8)
            {
                MajDebug.LogWarning(BUTTON_OUT_OF_RANGE);
                return default;
            }
            var i = (int)zone;

            return _isBtnClickedInThisFrame[i];
            //return _btnStatusInPreviousFrame[i] == SwitchStatus.Off &&
            //       _btnStatusInThisFrame[i] == SwitchStatus.On;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSensorClickedInThisFrame(SensorArea? area)
        {
            if(area is null)
            {
                MajDebug.LogWarning(SENSOR_IS_NULL);
                return default;
            }
            else if (area < SensorArea.A1 || area > SensorArea.E8)
            {
                MajDebug.LogWarning(SENSOR_OUT_OF_RANGE);
                return default;
            }
            var i = (int)area;

            return _isSensorClickedInThisFrame[i];
            //return _sensorStatusInPreviousFrame[i] == SwitchStatus.Off &&
            //       _sensorStatusInThisFrame[i] == SwitchStatus.On;
        }

        public bool SimulateSensorPress(SensorArea? area)
        {
            if (area is null)
            {
                MajDebug.LogWarning(SENSOR_IS_NULL);
                return default;
            }
            else if (area < SensorArea.A1 || area > SensorArea.E8)
            {
                MajDebug.LogWarning(SENSOR_OUT_OF_RANGE);
                return default;
            }
            var i = (int)area;
            var raw = _sensorStatusInNextFrame[i];
            _sensorStatusInNextFrame[i] = SwitchStatus.On;

            return raw == SwitchStatus.Off;
        }
        public bool SimulateButtonPress(ButtonZone? zone)
        {
            if (zone is null)
            {
                MajDebug.LogWarning(BUTTON_IS_NULL);
                return default;
            }
            else if (zone < ButtonZone.A1 || zone > ButtonZone.A8)
            {
                MajDebug.LogWarning(BUTTON_OUT_OF_RANGE);
                return default;
            }

            var i = (int)zone;
            var raw = _btnStatusInNextFrame[i];
            _btnStatusInNextFrame[i] = SwitchStatus.On;

            return raw == SwitchStatus.Off;
        }
        public bool SimulateSensorClick(SensorArea? area)
        {
            if (area is null)
            {
                MajDebug.LogWarning(SENSOR_IS_NULL);
                return default;
            }
            else if (area < SensorArea.A1 || area > SensorArea.E8)
            {
                MajDebug.LogWarning(SENSOR_OUT_OF_RANGE);
                return default;
            }

            var i = (int)area;
            var raw = _sensorStatusInThisFrame[i] == SwitchStatus.Off &&
                      _sensorStatusInNextFrame[i] == SwitchStatus.Off;
            _sensorStatusInNextFrame[i] = SwitchStatus.On;

            return raw;
        }
        public bool SimulateButtonClick(ButtonZone? zone)
        {
            if (zone is null)
            {
                MajDebug.LogWarning(BUTTON_IS_NULL);
                return default;
            }
            else if (zone < ButtonZone.A1 || zone > ButtonZone.A8)
            {
                MajDebug.LogWarning(BUTTON_OUT_OF_RANGE);
                return default;
            }

            var i = (int)zone;
            var raw = _btnStatusInThisFrame[i] == SwitchStatus.Off &&
                      _btnStatusInNextFrame[i] == SwitchStatus.Off;
            _btnStatusInNextFrame[i] = SwitchStatus.On;

            return raw;
        }
        
    }
}