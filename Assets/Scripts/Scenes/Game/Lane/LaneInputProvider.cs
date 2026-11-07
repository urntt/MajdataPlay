using MajdataPlay.IO;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
#nullable enable
namespace MajdataPlay.Scenes.Game.Lane
{
    /// <summary>
    /// A finger that can hit falling-mode slide check points in this frame.
    /// </summary>
    internal readonly struct LaneFinger
    {
        /// <summary>
        /// Identifies the finger from its press to its release; never reused. Physical buttons use negative keys.
        /// </summary>
        public readonly int Key;

        /// <summary>
        /// The horizontal world position of the finger.
        /// </summary>
        public readonly float X;

        /// <summary>
        /// Initializes a new instance of the <see cref="LaneFinger"/> struct.
        /// </summary>
        /// <param name="key">The key of the finger.</param>
        /// <param name="x">The horizontal world position.</param>
        public LaneFinger(int key, float x)
        {
            Key = key;
            X = x;
        }
    }
    /// <summary>
    /// 下落式模式的触屏输入：按屏幕位置换算为轨道（列）与高度。
    /// <para>Tap/Hold/Break/Flick 使用“列”：垂直判定开启时列上任意高度都算，关闭时需在判定线附近。
    /// 每列的判定范围是整列加左右各半列，一次按下只能打一个音符，具体给哪一列由 NoteManager 按时间就近分配。</para>
    /// <para>Slide 使用手指的横坐标，与箭头路径的位置比较；高度规则与“列”相同。</para>
    /// <para>悬空 Touch 使用“悬空区”：必须按在该传感器所在的轨道和高度附近。</para>
    /// </summary>
    internal sealed class LaneInputProvider
    {
        /// <summary>
        /// 手指速度超过该值（世界单位/秒）视为 Flick
        /// </summary>
        const float FLICK_SPEED = 10f;
        /// <summary>
        /// 速度回落到该值以下后，同一根手指才能再次触发 Flick
        /// </summary>
        const float FLICK_REARM_SPEED = 4f;
        /// <summary>
        /// Flick 在多少帧内可被音符使用，避免划动与判定跨帧错过
        /// </summary>
        const int FLICK_LATCH_FRAMES = 3;
        const int MOUSE_TOUCH_ID = int.MinValue;
        /// <summary>
        /// 扫键时越过两列分界线这么远才算进入新的一列，手指停在分界线上来回抖时不会连续触发
        /// </summary>
        static float SweepHysteresis => LaneLayout.LaneWidth * 0.15f;

        public ReadOnlySpan<bool> LaneHeld => _laneHeld;
        /// <summary>
        /// 点亮手指所在列：手指当前所在的列（带防抖），与扫键设置无关
        /// </summary>
        public ReadOnlySpan<bool> HighlightLanes => _highlightLanes;
        public ReadOnlySpan<bool> FloatSensorHeld => _floatSensorHeld;
        public ReadOnlySpan<int> FloatSensorClickedCount => _floatSensorClickedCount;
        /// <summary>
        /// 地雷 Touch 用：按在 Touch 本身上（不放大）的悬空区，以及本帧手指进入的次数
        /// </summary>
        public ReadOnlySpan<bool> MineSensorHeld => _mineSensorHeld;
        public ReadOnlySpan<int> MineSensorClickedCount => _mineSensorClickedCount;
        /// <summary>
        /// Slide 保持原样时：按标准圆形传感器划分被按住的传感器
        /// </summary>
        public ReadOnlySpan<bool> CircleSensorHeld => _circleSensorHeld;
        /// <summary>
        /// Slide 保持原样时：本帧手指进入各圆形传感器的次数，外圈的星星头用它判定
        /// </summary>
        public ReadOnlySpan<int> CircleSensorClickedCount => _circleSensorClickedCount;
        /// <summary>
        /// 本帧的按下数量，配合 <see cref="GetClick"/>、<see cref="UseClick"/> 由 NoteManager 分配给音符
        /// </summary>
        public int ClickCount => _clicks.Count;
        /// <summary>
        /// 本帧可以打 Slide 检查点的手指：轨道判定区内的手指；悬空式时为第二判定线以上的手指
        /// </summary>
        public ReadOnlySpan<LaneFinger> SlideFingers => new ReadOnlySpan<LaneFinger>(_slideFingers, 0, _slideFingerCount);

        readonly bool[] _laneHeld = new bool[LaneLayout.LANE_COUNT];
        readonly bool[] _highlightLanes = new bool[LaneLayout.LANE_COUNT];
        readonly List<LaneClick> _clicks = new(16);
        LaneFinger[] _slideFingers = new LaneFinger[16];
        int _slideFingerCount = 0;
        /// <summary>
        /// 扫键保护：正在打星星的手指（键为 <see cref="LaneFinger.Key"/>）与登记先后，越小越早
        /// </summary>
        readonly Dictionary<int, int> _guardOrders = new(8);
        readonly List<int> _expiredGuardKeys = new(8);
        /// <summary>
        /// 每列中受保护的手指：上一帧在这一列、登记最早的受保护手指的键，没有时为 0
        /// </summary>
        readonly int[] _guardOwnerKeys = new int[LaneLayout.LANE_COUNT];
        readonly int[] _guardOwnerOrders = new int[LaneLayout.LANE_COUNT];
        int _nextGuardOrder = 0;
        /// <summary>
        /// 手指的键：每次按下分配一个新的，抬起后不再使用
        /// </summary>
        int _nextFingerKey = 0;
        readonly int[] _flickLatch = new int[LaneLayout.LANE_COUNT];
        // 同一次划动标记的两列共用一个编号，被一列用掉后另一列也失效
        readonly int[] _flickId = new int[LaneLayout.LANE_COUNT];
        int _nextFlickId = 0;
        readonly bool[] _floatSensorHeld = new bool[33];
        readonly int[] _floatSensorClickedCount = new int[33];
        readonly bool[] _mineSensorHeld = new bool[33];
        readonly int[] _mineSensorClickedCount = new int[33];
        readonly bool[] _circleSensorHeld = new bool[33];
        readonly int[] _circleSensorClickedCount = new int[33];

        readonly Dictionary<int, TouchRecord> _records = new(16);
        readonly List<int> _staleTouchIds = new(16);
        readonly HashSet<int> _activeTouchIds = new();

        /// <summary>
        /// 一次按下：可以打手指所在列或相邻列的音符，但只能用一次
        /// </summary>
        internal struct LaneClick
        {
            public int Lane;
            /// <summary>
            /// 同时算作的相邻列，没有时为 0
            /// </summary>
            public int NeighborLane;
            public bool IsUsed;
            /// <summary>
            /// 扫键产生的点击（手指划进这一列，不是新按下）
            /// </summary>
            public bool IsSweep;
            /// <summary>
            /// 按下位置在判定线附近（上下 <see cref="LaneLayout.JUDGE_BAND_HALF_HEIGHT"/> 以内），地雷只认这种
            /// </summary>
            public bool IsInJudgeBand;
        }
        struct TouchRecord
        {
            public Vector2 LastPosition;
            /// <summary>
            /// 上一帧所在的悬空区（33 位）
            /// </summary>
            public ulong LastFloatSensors;
            /// <summary>
            /// 上一帧按在其上的地雷 Touch 区（33 位）
            /// </summary>
            public ulong LastMineSensors;
            public bool IsFlickArmed;
            /// <summary>
            /// 从 HUD 按钮上开始、或恢复游戏时已按住的手指，在抬起前都不参与判定
            /// </summary>
            public bool IsIgnored;
            /// <summary>
            /// 扫键用：这根手指上一帧所在的列（带防抖），不在轨道判定区时为 0
            /// </summary>
            public int SweepLane;
            /// <summary>
            /// Slide 保持原样用：上一帧所在的圆形传感器（33 位）
            /// </summary>
            public ulong LastCircleSensors;
            /// <summary>
            /// 这根手指的键，见 <see cref="LaneFinger.Key"/>
            /// </summary>
            public int Key;
        }

        /// <summary>
        /// 每帧在音符判定前调用
        /// </summary>
        /// <param name="guardKeys">
        /// 正在打星星的手指（上一帧由进行中的星星报告）。扫键与保护开启时，这些手指划过的列不扫键，
        /// 但同一列只保护登记最早的一根
        /// </param>
        public void Update(Camera? camera, Vector4 subScreenEdge, ReadOnlySpan<int> guardKeys)
        {
            Array.Clear(_laneHeld, 0, _laneHeld.Length);
            Array.Clear(_highlightLanes, 0, _highlightLanes.Length);
            _clicks.Clear();
            _slideFingerCount = 0;
            UpdateGuards(guardKeys);
            Array.Clear(_floatSensorHeld, 0, _floatSensorHeld.Length);
            Array.Clear(_floatSensorClickedCount, 0, _floatSensorClickedCount.Length);
            Array.Clear(_mineSensorHeld, 0, _mineSensorHeld.Length);
            Array.Clear(_mineSensorClickedCount, 0, _mineSensorClickedCount.Length);
            Array.Clear(_circleSensorHeld, 0, _circleSensorHeld.Length);
            Array.Clear(_circleSensorClickedCount, 0, _circleSensorClickedCount.Length);
            for (var i = 0; i < _flickLatch.Length; i++)
            {
                if (_flickLatch[i] > 0)
                {
                    _flickLatch[i]--;
                }
            }
            _activeTouchIds.Clear();
            if (camera is null)
            {
                _records.Clear();
                return;
            }
            var deltaTime = Mathf.Max(MajTimeline.UnscaledDeltaTime, 1e-4f);
            var touches = Touch.activeTouches;
            if (touches.Count > 0)
            {
                for (var i = 0; i < touches.Count; i++)
                {
                    var touch = touches[i];
                    if (!touch.valid || touch.phase is TouchPhase.Ended or TouchPhase.Canceled)
                    {
                        continue;
                    }
                    var isBegan = touch.phase == TouchPhase.Began;
                    HandlePointer(touch.touchId, touch.screenPosition, touch.radius.magnitude, isBegan, camera, subScreenEdge, deltaTime);
                }
            }
#if UNITY_STANDALONE || UNITY_EDITOR
            else if (Mouse.current is Mouse mouse && mouse.leftButton.isPressed)
            {
                HandlePointer(MOUSE_TOUCH_ID, mouse.position.value, 0f, mouse.leftButton.wasPressedThisFrame, camera, subScreenEdge, deltaTime);
            }
#endif
            _staleTouchIds.Clear();
            foreach (var id in _records.Keys)
            {
                if (!_activeTouchIds.Contains(id))
                {
                    _staleTouchIds.Add(id);
                }
            }
            foreach (var id in _staleTouchIds)
            {
                if (_records.TryGetValue(id, out var staleRecord))
                {
                    _guardOrders.Remove(staleRecord.Key);
                }
                _records.Remove(id);
            }
        }
        /// <summary>
        /// 更新扫键保护：去掉不再被星星报告的手指，登记新报告的手指，再按上一帧所在的列找出每列受保护的那根
        /// </summary>
        /// <param name="guardKeys">正在打星星的手指</param>
        void UpdateGuards(ReadOnlySpan<int> guardKeys)
        {
            _expiredGuardKeys.Clear();
            foreach (var key in _guardOrders.Keys)
            {
                if (guardKeys.IndexOf(key) < 0)
                {
                    _expiredGuardKeys.Add(key);
                }
            }
            foreach (var key in _expiredGuardKeys)
            {
                _guardOrders.Remove(key);
            }
            foreach (var key in guardKeys)
            {
                if (key >= 0 && !_guardOrders.ContainsKey(key))
                {
                    _guardOrders.Add(key, _nextGuardOrder++);
                }
            }
            Array.Clear(_guardOwnerKeys, 0, _guardOwnerKeys.Length);
            foreach (var record in _records.Values)
            {
                if (record.SweepLane == 0 || !_guardOrders.TryGetValue(record.Key, out var order))
                {
                    continue;
                }
                var index = record.SweepLane - 1;
                if (_guardOwnerKeys[index] == 0 || order < _guardOwnerOrders[index])
                {
                    _guardOwnerKeys[index] = record.Key;
                    _guardOwnerOrders[index] = order;
                }
            }
        }
        /// <summary>
        /// 这根手指此刻是否受扫键保护：正在打星星，且所到的列没有更早登记的受保护手指
        /// </summary>
        /// <param name="key">手指的键</param>
        /// <param name="lane">手指所到的列（1~8）</param>
        bool IsGuarded(int key, int lane)
        {
            if (!LaneMode.IsSweepSlideGuardEnabled || !_guardOrders.ContainsKey(key))
            {
                return false;
            }
            var owner = _guardOwnerKeys[lane - 1];
            return owner == 0 || owner == key;
        }
        public LaneClick GetClick(int index)
        {
            return _clicks[index];
        }
        /// <summary>
        /// 标记该按下已被某个音符使用
        /// </summary>
        public void UseClick(int index)
        {
            var click = _clicks[index];
            click.IsUsed = true;
            _clicks[index] = click;
        }
        /// <param name="lane">1~8</param>
        public bool IsFlicked(int lane)
        {
            return lane is >= 1 and <= LaneLayout.LANE_COUNT && _flickLatch[lane - 1] > 0;
        }
        /// <summary>
        /// 使用一次 Flick，一次划动只能判定一个音符
        /// </summary>
        /// <param name="lane">1~8</param>
        public bool TryUseFlick(int lane)
        {
            if (!IsFlicked(lane))
            {
                return false;
            }
            var id = _flickId[lane - 1];
            for (var i = 0; i < _flickLatch.Length; i++)
            {
                if (_flickId[i] == id)
                {
                    _flickLatch[i] = 0;
                }
            }
            return true;
        }
        /// <summary>
        /// 暂停恢复时调用：当前按住屏幕的手指在抬起前都不参与判定，避免被当成新的点击
        /// </summary>
        public void IgnoreActivePointers()
        {
            var touches = Touch.activeTouches;
            for (var i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (!touch.valid || touch.phase is TouchPhase.Ended or TouchPhase.Canceled)
                {
                    continue;
                }
                _records[touch.touchId] = new TouchRecord()
                {
                    LastPosition = touch.screenPosition,
                    IsIgnored = true,
                    Key = ++_nextFingerKey
                };
            }
#if UNITY_STANDALONE || UNITY_EDITOR
            if (Mouse.current is Mouse mouse && mouse.leftButton.isPressed)
            {
                _records[MOUSE_TOUCH_ID] = new TouchRecord()
                {
                    LastPosition = mouse.position.value,
                    IsIgnored = true,
                    Key = ++_nextFingerKey
                };
            }
#endif
        }
        public void Clear()
        {
            _records.Clear();
            Array.Clear(_laneHeld, 0, _laneHeld.Length);
            Array.Clear(_highlightLanes, 0, _highlightLanes.Length);
            _clicks.Clear();
            _slideFingerCount = 0;
            _guardOrders.Clear();
            Array.Clear(_guardOwnerKeys, 0, _guardOwnerKeys.Length);
            Array.Clear(_flickLatch, 0, _flickLatch.Length);
            Array.Clear(_floatSensorHeld, 0, _floatSensorHeld.Length);
            Array.Clear(_floatSensorClickedCount, 0, _floatSensorClickedCount.Length);
            Array.Clear(_mineSensorHeld, 0, _mineSensorHeld.Length);
            Array.Clear(_mineSensorClickedCount, 0, _mineSensorClickedCount.Length);
            Array.Clear(_circleSensorHeld, 0, _circleSensorHeld.Length);
            Array.Clear(_circleSensorClickedCount, 0, _circleSensorClickedCount.Length);
        }
        void AddSlideFinger(int key, float x)
        {
            if (_slideFingerCount == _slideFingers.Length)
            {
                Array.Resize(ref _slideFingers, _slideFingers.Length * 2);
            }
            _slideFingers[_slideFingerCount++] = new LaneFinger(key, x);
        }
        /// <summary>
        /// 手指能否打 Slide 检查点：悬空式时须在第二判定线以上（垂直判定关闭时不超过判定带的高度），否则与“列”的规则相同
        /// </summary>
        /// <param name="lane">手指所在的列，不在轨道上时为 0</param>
        /// <param name="y">手指的纵坐标（世界坐标）</param>
        /// <param name="isInLineArea">手指是否在“列”的判定区内</param>
        static bool IsInSlideArea(int lane, float y, bool isInLineArea)
        {
            if (!LaneMode.IsSlideFloating)
            {
                return isInLineArea;
            }
            if (lane == 0 || y < LaneLayout.FLOAT_SLIDE_LINE_Y)
            {
                return false;
            }
            return LaneMode.IsVerticalJudge || y <= LaneLayout.FLOAT_SLIDE_LINE_Y + LaneLayout.JUDGE_BAND_HALF_HEIGHT * 2f;
        }
        /// <summary>
        /// 扫键用的当前列：从 <paramref name="lastLane"/> 移到相邻列时，要越过分界线 <see cref="SweepHysteresis"/> 才算
        /// </summary>
        static int GetSweepLane(float x, int lane, int lastLane)
        {
            if (lastLane == 0 || lane == lastLane)
            {
                return lane;
            }
            var boundary = lane > lastLane ? LaneLayout.Left + lastLane * LaneLayout.LaneWidth
                                           : LaneLayout.Left + (lastLane - 1) * LaneLayout.LaneWidth;
            return Mathf.Abs(x - boundary) >= SweepHysteresis ? lane : lastLane;
        }
        /// <summary>
        /// 扫键：手指从 <paramref name="fromLane"/> 划到 <paramref name="toLane"/>，经过的每一列（不含起点列）各算一次点击，只算这一列
        /// </summary>
        void AddSweepClicks(int fromLane, int toLane)
        {
            if (fromLane == 0)
            {
                _clicks.Add(new LaneClick()
                {
                    Lane = toLane,
                    IsSweep = true
                });
                return;
            }
            var step = toLane > fromLane ? 1 : -1;
            for (var sweptLane = fromLane + step; sweptLane != toLane + step; sweptLane += step)
            {
                _clicks.Add(new LaneClick()
                {
                    Lane = sweptLane,
                    IsSweep = true
                });
            }
        }
        /// <param name="touchRadius">Touch.radius 的长度，鼠标为 0</param>
        void HandlePointer(int id, Vector2 screenPosition, float touchRadius, bool isBegan, Camera camera, Vector4 subScreenEdge, float deltaTime)
        {
            _activeTouchIds.Add(id);
            var world = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -camera.transform.position.z));
            var position = new Vector2(world.x, world.y);
            var hasRecord = _records.TryGetValue(id, out var record);
            if (!hasRecord)
            {
                record = new TouchRecord()
                {
                    LastPosition = position,
                    LastFloatSensors = 0,
                    IsFlickArmed = true,
                    Key = ++_nextFingerKey,
                    // 按在暂停键、跳过键上的手指不打音符
                    IsIgnored = LaneHud.IsPointerBlocked(screenPosition)
                };
                // 第一次出现的手指视为按下
                isBegan = true;
            }
            if (record.IsIgnored)
            {
                _records[id] = record;
                return;
            }
            // 副屏区域用于跳过乐曲等操作，不参与轨道判定
            var isInSubScreen = position.x > subScreenEdge.x && position.x < subScreenEdge.z &&
                                position.y > subScreenEdge.w && position.y < subScreenEdge.y;
            // Slide 保持原样：另按标准圆形传感器换算，只给 Slide 和外圈的星星头用；进入某个传感器即视为点击
            var circleSensors = 0UL;
            if (LaneMode.IsSlideOriginal && !isInSubScreen)
            {
                Span<bool> pointerSensors = stackalloc bool[33];
                InputManager.GetTouchPanelSensorStates(camera, screenPosition, touchRadius, pointerSensors);
                for (var i = 0; i < pointerSensors.Length; i++)
                {
                    if (!pointerSensors[i])
                    {
                        continue;
                    }
                    circleSensors |= 1UL << i;
                    _circleSensorHeld[i] = true;
                    if ((record.LastCircleSensors & (1UL << i)) == 0)
                    {
                        _circleSensorClickedCount[i]++;
                    }
                }
            }
            record.LastCircleSensors = circleSensors;
            var lane = isInSubScreen ? 0 : LaneLayout.GetLaneAt(position.x);
            var neighborLane = LaneLayout.GetNeighborLane(position.x, lane);
            var isInLineArea = lane != 0 &&
                               (LaneMode.IsVerticalJudge || Mathf.Abs(position.y - LaneLayout.JUDGE_LINE_Y) <= LaneLayout.JUDGE_BAND_HALF_HEIGHT);
            if (IsInSlideArea(lane, position.y, isInLineArea))
            {
                AddSlideFinger(record.Key, position.x);
            }
            if (isInLineArea)
            {
                _laneHeld[lane - 1] = true;
                if (neighborLane != 0)
                {
                    _laneHeld[neighborLane - 1] = true;
                }
                if (isBegan)
                {
                    _clicks.Add(new LaneClick()
                    {
                        Lane = lane,
                        NeighborLane = neighborLane,
                        IsInJudgeBand = Mathf.Abs(position.y - LaneLayout.JUDGE_LINE_Y) <= LaneLayout.JUDGE_BAND_HALF_HEIGHT
                    });
                }
            }
            // 扫键：按住的手指划进新的一列也算点击这一列；正在打星星的手指（每列最多一根）不算，免得把星星途经列上的 Tap 提前扫掉
            var sweepLane = isInLineArea ? GetSweepLane(position.x, lane, record.SweepLane) : 0;
            if (LaneMode.IsSweepEnabled && !isBegan && sweepLane != 0 && sweepLane != record.SweepLane &&
                !IsGuarded(record.Key, sweepLane))
            {
                AddSweepClicks(record.SweepLane, sweepLane);
            }
            record.SweepLane = sweepLane;
            // 点亮手指所在列：跟着手指走，与扫键设置无关
            if (sweepLane != 0)
            {
                _highlightLanes[sweepLane - 1] = true;
            }
            // Flick
            var speed = hasRecord ? (position - record.LastPosition).magnitude / deltaTime : 0f;
            if (record.IsFlickArmed)
            {
                if (isInLineArea && speed >= FLICK_SPEED)
                {
                    var flickId = ++_nextFlickId;
                    _flickLatch[lane - 1] = FLICK_LATCH_FRAMES;
                    _flickId[lane - 1] = flickId;
                    if (neighborLane != 0)
                    {
                        _flickLatch[neighborLane - 1] = FLICK_LATCH_FRAMES;
                        _flickId[neighborLane - 1] = flickId;
                    }
                    record.IsFlickArmed = false;
                }
            }
            else if (speed <= FLICK_REARM_SPEED)
            {
                record.IsFlickArmed = true;
            }
            // 悬空 Touch：进入某个悬空区即视为点击；地雷 Touch 另按 Touch 本身的范围计算
            var floatSensors = 0UL;
            var mineSensors = 0UL;
            if (!isInSubScreen)
            {
                for (var i = 0; i < _floatSensorHeld.Length; i++)
                {
                    var area = (SensorArea)i;
                    var bit = 1UL << i;
                    if (LaneLayout.IsInFloatHitArea(area, position))
                    {
                        floatSensors |= bit;
                        _floatSensorHeld[i] = true;
                        if ((record.LastFloatSensors & bit) == 0)
                        {
                            _floatSensorClickedCount[i]++;
                        }
                    }
                    if (LaneLayout.IsInFloatMineArea(area, position))
                    {
                        mineSensors |= bit;
                        _mineSensorHeld[i] = true;
                        if ((record.LastMineSensors & bit) == 0)
                        {
                            _mineSensorClickedCount[i]++;
                        }
                    }
                }
            }
            record.LastFloatSensors = floatSensors;
            record.LastMineSensors = mineSensors;
            record.LastPosition = position;
            _records[id] = record;
        }
    }
}
