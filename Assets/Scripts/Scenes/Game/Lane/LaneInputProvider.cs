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

        public ReadOnlySpan<bool> LaneHeld => _laneHeld;
        public ReadOnlySpan<bool> FloatSensorHeld => _floatSensorHeld;
        public ReadOnlySpan<int> FloatSensorClickedCount => _floatSensorClickedCount;
        /// <summary>
        /// 本帧的按下数量，配合 <see cref="GetClick"/>、<see cref="UseClick"/> 由 NoteManager 分配给音符
        /// </summary>
        public int ClickCount => _clicks.Count;
        /// <summary>
        /// 本帧在轨道判定区内的手指横坐标（世界坐标）
        /// </summary>
        public ReadOnlySpan<float> DragXs => new ReadOnlySpan<float>(_dragXs, 0, _dragCount);

        readonly bool[] _laneHeld = new bool[LaneLayout.LANE_COUNT];
        readonly List<LaneClick> _clicks = new(16);
        float[] _dragXs = new float[16];
        int _dragCount = 0;
        readonly int[] _flickLatch = new int[LaneLayout.LANE_COUNT];
        // 同一次划动标记的两列共用一个编号，被一列用掉后另一列也失效
        readonly int[] _flickId = new int[LaneLayout.LANE_COUNT];
        int _nextFlickId = 0;
        readonly bool[] _floatSensorHeld = new bool[33];
        readonly int[] _floatSensorClickedCount = new int[33];

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
        }
        struct TouchRecord
        {
            public Vector2 LastPosition;
            /// <summary>
            /// 上一帧所在的悬空区（33 位）
            /// </summary>
            public ulong LastFloatSensors;
            public bool IsFlickArmed;
            /// <summary>
            /// 从 HUD 按钮上开始、或恢复游戏时已按住的手指，在抬起前都不参与判定
            /// </summary>
            public bool IsIgnored;
        }

        /// <summary>
        /// 每帧在音符判定前调用
        /// </summary>
        public void Update(Camera? camera, Vector4 subScreenEdge)
        {
            Array.Clear(_laneHeld, 0, _laneHeld.Length);
            _clicks.Clear();
            _dragCount = 0;
            Array.Clear(_floatSensorHeld, 0, _floatSensorHeld.Length);
            Array.Clear(_floatSensorClickedCount, 0, _floatSensorClickedCount.Length);
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
                    HandlePointer(touch.touchId, touch.screenPosition, isBegan, camera, subScreenEdge, deltaTime);
                }
            }
#if UNITY_STANDALONE || UNITY_EDITOR
            else if (Mouse.current is Mouse mouse && mouse.leftButton.isPressed)
            {
                HandlePointer(MOUSE_TOUCH_ID, mouse.position.value, mouse.leftButton.wasPressedThisFrame, camera, subScreenEdge, deltaTime);
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
                _records.Remove(id);
            }
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
                    IsIgnored = true
                };
            }
#if UNITY_STANDALONE || UNITY_EDITOR
            if (Mouse.current is Mouse mouse && mouse.leftButton.isPressed)
            {
                _records[MOUSE_TOUCH_ID] = new TouchRecord()
                {
                    LastPosition = mouse.position.value,
                    IsIgnored = true
                };
            }
#endif
        }
        public void Clear()
        {
            _records.Clear();
            Array.Clear(_laneHeld, 0, _laneHeld.Length);
            _clicks.Clear();
            _dragCount = 0;
            Array.Clear(_flickLatch, 0, _flickLatch.Length);
            Array.Clear(_floatSensorHeld, 0, _floatSensorHeld.Length);
            Array.Clear(_floatSensorClickedCount, 0, _floatSensorClickedCount.Length);
        }
        void AddDragX(float x)
        {
            if (_dragCount == _dragXs.Length)
            {
                Array.Resize(ref _dragXs, _dragXs.Length * 2);
            }
            _dragXs[_dragCount++] = x;
        }
        void HandlePointer(int id, Vector2 screenPosition, bool isBegan, Camera camera, Vector4 subScreenEdge, float deltaTime)
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
            var lane = isInSubScreen ? 0 : LaneLayout.GetLaneAt(position.x);
            var neighborLane = LaneLayout.GetNeighborLane(position.x, lane);
            var isInLineArea = lane != 0 &&
                               (LaneMode.IsVerticalJudge || Mathf.Abs(position.y - LaneLayout.JUDGE_LINE_Y) <= LaneLayout.JUDGE_BAND_HALF_HEIGHT);
            if (isInLineArea)
            {
                AddDragX(position.x);
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
                        NeighborLane = neighborLane
                    });
                }
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
            // 悬空 Touch：进入某个悬空区即视为点击
            var floatSensors = 0UL;
            if (!isInSubScreen)
            {
                for (var i = 0; i < _floatSensorHeld.Length; i++)
                {
                    var area = (SensorArea)i;
                    if (!LaneLayout.IsInFloatHitArea(area, position))
                    {
                        continue;
                    }
                    floatSensors |= 1UL << i;
                    _floatSensorHeld[i] = true;
                    if ((record.LastFloatSensors & (1UL << i)) == 0)
                    {
                        _floatSensorClickedCount[i]++;
                    }
                }
            }
            record.LastFloatSensors = floatSensors;
            record.LastPosition = position;
            _records[id] = record;
        }
    }
}
