using MajdataPlay.Buffers;
using MajdataPlay.Diagnostics;
using MajdataPlay.Game.Notes;
using MajdataPlay.IO;
using MajdataPlay.Numerics;
using MajdataPlay.Scenes.Game.Buffers;
using MajdataPlay.Scenes.Game.Lane;
using MajdataPlay.Scenes.Game.Notes.Controllers;
using MajdataPlay.Scenes.Game.Utils;
using MajdataPlay.Settings;
using MajdataPlay.Utils;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Profiling;
#nullable enable
namespace MajdataPlay.Scenes.Game.Notes.Behaviours
{
    internal sealed class TapDrop : NoteDrop, IDistanceProvider, INoteQueueMember<TapQueueInfo>, IPoolableNote<TapPoolingInfo, TapQueueInfo>, IMajComponent
    {
        public TapQueueInfo QueueInfo { get; set; } = TapQueueInfo.Default;
        public float RotateSpeed { get; set; } = 0f;
        public bool IsDouble { get; set; } = false;
        public bool IsStar { get; set; } = false;
        public float Distance { get; set; } = -100;

        [SerializeField]
        GameObject _tapLinePrefab;

        EachLineBinding? _eachLineBinding;

        Transform _tapLineTransform;
        GameObject _tapLineObject;
        GameObject _exObject;


        SpriteRenderer _thisRenderer;
        SpriteRenderer _exRenderer;
        SpriteRenderer _tapLineRenderer;
        NotePoolManager _notePoolManager;

        bool _isStarRotation = false;
        // 下落式模式
        bool _isLaneMode = false;
        /// <summary>
        /// 下落式开启“Slide 保持原样”时，星星头回到圆形模式的外圈，按标准圆形传感器判定
        /// </summary>
        bool _isOnRing = false;
        /// <summary>
        /// 按下落式的轨道摆放与判定（不在外圈的下落式音符）
        /// </summary>
        bool IsLaneLayout => _isLaneMode && !_isOnRing;
        /// <summary>
        /// 下落式 Break 转 Flick：外观为红色 Tap，需要划动才判定
        /// </summary>
        bool _isFlick = false;
        Color _defaultColor = Color.white;

        ButtonZone? _buttonPos;
        // 下落式：中心 C 区 Touch 转成的 Tap 落在第 4、5 列之间，第 5 列也能判定
        ButtonZone? _altButtonPos;
        bool _isLaneCenter = false;

        Vector3 _innerPos = NoteHelper.GetTapPosition(1, 1.225f);
        Vector3 _outerPos = NoteHelper.GetTapPosition(1, 4.8f);

        float _noteAppearRate = 0.265f;
        //readonly float _touchPanelOffset = MajEnv.UserSetting?.Judge.TouchPanelOffset ?? 0;

        const int TAP_SPRITE_SORT_ORDER = 1;
        const int TAP_EX_SORT_ORDER = 0;

        protected override void Awake()
        {
            base.Awake();
            _noteAppearRate = MajEnv.Settings.Debug.NoteAppearRate;
            _isStarRotation = Settings.Game.StarRotation;
            _isLaneMode = LaneMode.IsEnabled;
            _notePoolManager = FindAnyObjectByType<NotePoolManager>();
            _thisRenderer = GetComponent<SpriteRenderer>();
            _defaultColor = _thisRenderer.color;

            _exObject = Transform.GetChild(0).gameObject;
            _exRenderer = _exObject.GetComponent<SpriteRenderer>();

            _tapLineObject = Instantiate(_tapLinePrefab, NoteManager.gameObject.transform.GetChild(7));
            _tapLineObject.SetActive(true);
            _tapLineRenderer = _tapLineObject.GetComponent<SpriteRenderer>();
            _tapLineTransform = _tapLineObject.transform;

            Transform.localScale = new Vector3(0, 0);

            SetActiveWithRenderer(false);
        }
        public void Init(TapPoolingInfo poolingInfo)
        {
            if (State >= NoteStatus.Inited && State < NoteStatus.End)
            {
                return;
            }
            _eachLineBinding = poolingInfo.EachLineBinding;
            if (_eachLineBinding is not null)
            {
                _eachLineBinding.Bind(this);
            }
            StartPos = poolingInfo.StartPos;
            Timing = poolingInfo.Timing;
            JudgeTiming = Timing;
            SortOrder = poolingInfo.NoteSortOrder;
            Speed = poolingInfo.Speed;
            IsEach = poolingInfo.IsEach;
            IsBreak = poolingInfo.IsBreak;
            IsEX = poolingInfo.IsEX;
            IsMine = poolingInfo.IsMine;
            QueueInfo = poolingInfo.QueueInfo;
            IsStar = poolingInfo.IsStar;
            _isOnRing = _isLaneMode && LaneMode.IsSlideOriginal && IsStar;
            IsDouble = poolingInfo.IsDouble;
            RotateSpeed = poolingInfo.RotateSpeed;
            IsJudged = false;
            Distance = -100;
            _innerPos = NoteHelper.GetTapPosition(StartPos, 1.225f);
            _outerPos = NoteHelper.GetTapPosition(StartPos, 4.8f);
            SensorPos = (SensorArea)(StartPos - 1);
            _buttonPos = SensorPos.ToButtonZone();
            _isLaneCenter = _isLaneMode && poolingInfo.IsLaneCenter;
            _altButtonPos = _isLaneCenter ? ((SensorArea)(LaneLayout.CENTER_ALT_LANE - 1)).ToButtonZone() : null;
            _isFlick = IsLaneLayout && LaneMode.IsBreakToFlick && IsBreak && !IsMine;
            if (IsMine)
            {
                JudgableRange = new(JudgeTimingWithOffset - (TAP_JUDGE_SEG_3RD_PERFECT_MSEC / 1000), JudgeTimingWithOffset + (TAP_JUDGE_SEG_3RD_PERFECT_MSEC / 1000), ContainsType.Closed);
            }
            else
            {
                JudgableRange = new(JudgeTimingWithOffset - (TAP_JUDGE_GOOD_AREA_MSEC / 1000), JudgeTimingWithOffset + (TAP_JUDGE_GOOD_AREA_MSEC / 1000), ContainsType.Closed);
            }
            if (IsLaneLayout && !IsMine && !_isFlick)
            {
                NoteManager.RegisterLaneNote(QueueInfo, JudgeTimingWithOffset);
            }

            Transform.rotation = IsLaneLayout ? LaneLayout.NoteRotation : Quaternion.Euler(0, 0, -22.5f + (-45f * (StartPos - 1)));
            Transform.localScale = new Vector3(0, 0);

            _tapLineObject.transform.rotation = Quaternion.Euler(0, 0, -22.5f + (-45f * (StartPos - 1)));
            _thisRenderer.sortingOrder = SortOrder - TAP_SPRITE_SORT_ORDER;
            _exRenderer.sortingOrder = SortOrder - TAP_EX_SORT_ORDER;

            LoadSkin();
            SetActiveWithoutRenderer(true);

            State = NoteStatus.Inited;
        }
        private void End()
        {
            if (IsEnded)
            {
                return;
            }
            State = NoteStatus.End;
            
            if (_eachLineBinding is not null)
            {
                _eachLineBinding.Unbind(this);
                _eachLineBinding = null;
            }
            SetActive(false);
            var result = new NoteJudgeResult()
            {
                Grade = JudgeResult,
                IsBreak = IsBreak,
                IsMine = IsMine,
                IsEX = IsEX,
                Diff = JudgeDiff
            };
            PlayJudgeSFX(result);
            EffectManager.PlayTapJudgeResult(StartPos, result, _isOnRing);
            //MajDebug.LogDebug($"Note index: {QueueInfo.Index}");
            NoteManager.NextNote(QueueInfo);
            ObjectCounter.ReportResult(this, result);
            _notePoolManager.Collect(this);
        }
        protected override void PlaySFX()
        {
            PlayJudgeSFX(new NoteJudgeResult()
            {
                Grade = JudgeResult,
                IsBreak = IsBreak,
                IsEX = IsEX,
                IsMine = IsMine,
                Diff = JudgeDiff
            });
        }
        protected override void PlayJudgeSFX(in NoteJudgeResult judgeResult)
        {
            AudioEffMana.PlayTapSound(judgeResult);
        }
        [OnPreUpdate]
        internal void OnPreUpdate()
        {
            using (UnityProfiler.Create("TapDrop.OnPreUpdate"))
            {
                TooLateCheck();
                Check();
                MineCheck();
                Autoplay();
            }
        }
        [OnUpdate]
        internal void OnUpdate()
        {
            using (UnityProfiler.Create("TapDrop.OnUpdate"))
            {
                var timing = GetTimeSpanToArriveTiming();
                var distance = timing * Speed + 4.8f;
                var scaleRate = _noteAppearRate;
                var destScale = distance * scaleRate + (1 - scaleRate * 1.225f);

                switch (State)
                {
                    case NoteStatus.Inited:
                        if (destScale >= 0f)
                        {
                            Transform.position = IsLaneLayout ? GetLanePosition(1.225f) : _innerPos;
                            _tapLineTransform.localScale = new Vector3(1.225f / 4.8f, 1.225f / 4.8f, 1f);
                            SetNoteRendererActive(true);
                            SetExRendererActive(true);
                            State = NoteStatus.Scaling;
                            goto case NoteStatus.Scaling;
                        }
                        return;
                    case NoteStatus.Scaling:
                        {
                            if (destScale > 0.3f)
                            {
                                SetTapLineActive(true);
                            }
                            if (distance < 1.225f)
                            {
                                Distance = distance;
                                Transform.localScale = new Vector3(destScale, destScale) * USERSETTING_TAP_SCALE;
                            }
                            else
                            {
                                Transform.localScale = new Vector3(1f, 1f) * USERSETTING_TAP_SCALE;
                                State = NoteStatus.Running;
                                goto case NoteStatus.Running;
                            }
                        }
                        break;
                    case NoteStatus.Running:
                        {
                            Distance = distance;
                            Transform.position = IsLaneLayout ? GetLanePosition(distance) : _outerPos * (distance / 4.8f);
                            var lineScale = Mathf.Abs(distance / 4.8f);
                            _tapLineTransform.localScale = new Vector3(lineScale, lineScale, 1f);
                        }
                        break;
                    default:
                        return;
                }
                if (IsStar)
                {
                    if (NoteController.IsStart && _isStarRotation)
                    {
                        Transform.Rotate(0f, 0f, RotateSpeed * MajTimeline.DeltaTime);
                    }
                }
            }
        }
        protected override void Autoplay()
        {
            if (IsMine)
            {
                return;
            }
            switch(AutoplayMode)
            {
                case AutoplayModeOption.Enable:
                    base.Autoplay();
                    if(IsJudged)
                    {
                        End();
                    }
                    break;
                case AutoplayModeOption.DJAuto_TouchPanel_First:
                case AutoplayModeOption.DJAuto_ButtonRing_First:
                    DJAutoplay();
                    break;
            }
        }
        private void DJAutoplay()
        {
            if (IsJudged || !IsAutoplay)
            {
                return;
            }
            else if (!NoteManager.IsCurrentNoteJudgeable(QueueInfo))
            {
                return;
            }
            else if (GetTimeSpanToArriveTiming() < (-FRAME_LENGTH_SEC * 2 + FRAME_LENGTH_SEC / 2))
            {
                return;
            }
            var isBtnFirst = AutoplayMode == AutoplayModeOption.DJAuto_ButtonRing_First;

            if (isBtnFirst)
            {
                _ = NoteManager.SimulateButtonClick(_buttonPos) ||
                    (USERSETTING_DJAUTO_POLICY == DJAutoPolicyOption.Permissive && NoteManager.SimulateSensorClick(SensorPos));
            }
            else
            {
                _ = NoteManager.SimulateSensorClick(SensorPos) ||
                    (USERSETTING_DJAUTO_POLICY == DJAutoPolicyOption.Permissive && NoteManager.SimulateButtonClick(_buttonPos));
            }
        }
        
        private void TooLateCheck()
        {
            // Too late check
            if (IsJudged || IsEnded || AutoplayMode == AutoplayModeOption.Enable)
            {
                return;
            }

            var timing = GetTimeSpanToJudgeTiming();
            var isTooLate = timing > TAP_JUDGE_GOOD_AREA_MSEC / 1000;

            if (isTooLate)
            {
                //MajDebug.LogWarning("Note too late");
                JudgeResult = JudgeGrade.Miss;
                IsJudged = true;
                End();
            }
        }
        /// <summary>
        /// 下落式音符位置；中心音符落在第 4、5 列之间
        /// </summary>
        Vector3 GetLanePosition(float distance)
        {
            var position = LaneLayout.GetNotePosition(StartPos, distance);
            if (_isLaneCenter)
            {
                position.x = LaneLayout.GetSensorX(SensorArea.C);
            }
            return position;
        }
        bool TryUseLaneFlick()
        {
            if (NoteManager.IsLaneFlickedInThisFrame(StartPos) && NoteManager.TryUseLaneFlick(StartPos))
            {
                return true;
            }
            return _isLaneCenter &&
                   NoteManager.IsLaneFlickedInThisFrame(LaneLayout.CENTER_ALT_LANE) &&
                   NoteManager.TryUseLaneFlick(LaneLayout.CENTER_ALT_LANE);
        }
        /// <summary>
        /// 下落式：使用一次轨道点击（手指在本列或相邻列的半边），中心音符也可以用第 5 列
        /// </summary>
        bool TryUseLaneClick()
        {
            var now = ThisFrameSec;
            return NoteManager.TryUseLaneClick(StartPos, JudgeTimingWithOffset, now) ||
                   (_isLaneCenter && NoteManager.TryUseLaneClick(LaneLayout.CENTER_ALT_LANE, JudgeTimingWithOffset, now));
        }
        private void Check()
        {
            if (IsEnded || !IsInited)
            {
                return;
            }
            else if (!JudgableRange.InRange(ThisFrameSec) || !NoteManager.IsCurrentNoteJudgeable(QueueInfo))
            {
                return;
            }

            if (_isFlick)
            {
                if (!TryUseLaneFlick())
                {
                    return;
                }
                Judge(ThisFrameSec);
            }
            else if (NoteManager.IsButtonClickedInThisFrame(_buttonPos) && NoteManager.TryUseButtonClickEvent(_buttonPos))
            {
                Judge(ThisFrameSec);
            }
            else if (_altButtonPos is ButtonZone altButtonPos && NoteManager.IsButtonClickedInThisFrame(altButtonPos) && NoteManager.TryUseButtonClickEvent(altButtonPos))
            {
                Judge(ThisFrameSec);
            }
            else if (IsLaneLayout && TryUseLaneClick())
            {
                Judge(ThisFrameSec);
            }
            else if (_isOnRing && NoteManager.TryUseLaneCircleSensorClick(SensorPos))
            {
                // 外圈的星星头：手指按进外圈对应的 A 区（或按键），与标准游玩相同
                Judge(ThisFrameSec - USERSETTING_TOUCHPANEL_OFFSET_SEC);
            }
            else if (!_isLaneMode && NoteManager.IsSensorClickedInThisFrame(SensorPos) && NoteManager.TryUseSensorClickEvent(SensorPos))
            {
                // 下落式中 Tap 只响应轨道，悬空 Touch 区不触发 Tap
                Judge(ThisFrameSec - USERSETTING_TOUCHPANEL_OFFSET_SEC);
            }
            else
            {
                return;
            }

            if (IsJudged)
            {
                if(IsMine)
                {
                    if(JudgeResult >= JudgeGrade.Perfect)
                    {
                        JudgeResult = JudgeGrade.TooFast;
                    }
                    else
                    {
                        JudgeResult = JudgeGrade.Miss;
                    }
                }
                //MajDebug.LogError("Note is judged");
                End();
            }
        }
        private void MineCheck()
        {
            if (!IsMine || IsEnded || !IsInited || IsJudged)
            {
                return;
            }
            if (GetTimeSpanToJudgeTiming() > TAP_JUDGE_SEG_3RD_PERFECT_MSEC / 1000)
            {
                IsJudged = true;
                JudgeResult = JudgeGrade.Perfect;
                End();
            }
        }
        protected override void LoadSkin()
        {
            if (IsStar)
            {
                LoadStarSkin();
            }
            else
            {
                LoadTapSkin();
            }
            if (_isFlick)
            {
                LoadFlickSkin();
            }
            else
            {
                _thisRenderer.color = _defaultColor;
            }
        }
        /// <summary>
        /// Break 转 Flick：复用普通 Tap/星星贴图并染成红色
        /// </summary>
        private void LoadFlickSkin()
        {
            if (IsStar)
            {
                var skin = MajInstances.SkinManager.GetStarSkin();
                _thisRenderer.sprite = IsDouble ? skin.Double : skin.Normal;
            }
            else
            {
                var skin = MajInstances.SkinManager.GetTapSkin();
                _thisRenderer.sprite = skin.Normal;
            }
            _thisRenderer.sharedMaterial = DefaultMaterial;
            _thisRenderer.color = LaneVisuals.FlickColor;
        }
        public override void SetActive(bool state)
        {
            if (Active == state)
            {
                return;
            }
            SetActiveWithRenderer(state);
        }
        private void SetActiveWithoutRenderer(bool state)
        {
            base.SetActive(state);
        }
        private void SetActiveWithRenderer(bool state)
        {
            base.SetActive(state);
            SetNoteRendererActive(state);
            SetExRendererActive(state);
            SetTapLineActive(state);
        }
        private void SetNoteRendererActive(bool state)
        {
            _thisRenderer.enabled = state;
        }
        private void SetExRendererActive(bool state)
        {
            if(state)
            {
                _exRenderer.enabled = IsEX;
            }
            else
            {
                _exRenderer.enabled = false;
            }
        }
        private void SetTapLineActive(bool state)
        {
            // 下落式不显示圆形引导线；外圈的星星头与标准游玩相同
            _tapLineRenderer.enabled = state && !IsLaneLayout;
        }
        private void LoadTapSkin()
        {
            var skin = MajInstances.SkinManager.GetTapSkin();

            if (IsMine)
            {
                _thisRenderer.sprite = skin.Mine;
                _thisRenderer.sharedMaterial = DefaultMaterial;
                _exRenderer.sprite = skin.Ex;
                _exRenderer.color = skin.ExEffects[0];
                _tapLineRenderer.sprite = skin.GuideLines[0];
                if (IsEach)
                {
                    _tapLineRenderer.sprite = skin.GuideLines[1];
                }
                if (IsBreak)
                {
                    _thisRenderer.sprite = skin.BreakMine;
                    _thisRenderer.sharedMaterial = BreakMaterial;
                    _tapLineRenderer.sprite = skin.GuideLines[2];
                    _exRenderer.color = skin.ExEffects[2];
                }
            }
            else
            {
                _thisRenderer.sprite = skin.Normal;
                _thisRenderer.sharedMaterial = DefaultMaterial;
                _exRenderer.sprite = skin.Ex;
                _exRenderer.color = skin.ExEffects[0];
                _tapLineRenderer.sprite = skin.GuideLines[0];
                if (IsEach)
                {
                    _thisRenderer.sprite = skin.Each;
                    _tapLineRenderer.sprite = skin.GuideLines[1];
                    _exRenderer.color = skin.ExEffects[1];
                }
                if (IsBreak)
                {
                    _thisRenderer.sprite = skin.Break;
                    _thisRenderer.sharedMaterial = BreakMaterial;
                    _tapLineRenderer.sprite = skin.GuideLines[2];
                    _exRenderer.color = skin.ExEffects[2];
                }
            }
        }
        private void LoadStarSkin()
        {
            var skin = MajInstances.SkinManager.GetStarSkin();
            _thisRenderer.sharedMaterial = DefaultMaterial;
            _exRenderer.color = skin.ExEffects[0];
            _tapLineRenderer.sprite = skin.GuideLines[0];

            if (IsMine)
            {
                if (IsDouble)
                {
                    _thisRenderer.sprite = skin.DoubleMine;
                    _exRenderer.sprite = skin.ExDouble;
                    if (IsBreak)
                    {
                        _thisRenderer.sprite = skin.BreakDoubleMine;
                        _thisRenderer.sharedMaterial = BreakMaterial;
                        _tapLineRenderer.sprite = skin.GuideLines[2];
                        _exRenderer.color = skin.ExEffects[2];
                    }
                }
                else
                {
                    _thisRenderer.sprite = skin.Mine;
                    _exRenderer.sprite = skin.Ex;

                    if (IsBreak)
                    {
                        _thisRenderer.sprite = skin.BreakMine;
                        _thisRenderer.sharedMaterial = BreakMaterial;
                        _tapLineRenderer.sprite = skin.GuideLines[2];
                        _exRenderer.color = skin.ExEffects[2];
                    }
                }
            }
            else
            {
                if (IsDouble)
                {
                    _thisRenderer.sprite = skin.Double;
                    _exRenderer.sprite = skin.ExDouble;

                    if (IsEach)
                    {
                        _thisRenderer.sprite = skin.EachDouble;
                        _tapLineRenderer.sprite = skin.GuideLines[1];
                        _exRenderer.color = skin.ExEffects[1];
                    }
                    if (IsBreak)
                    {
                        _thisRenderer.sprite = skin.BreakDouble;
                        _thisRenderer.sharedMaterial = BreakMaterial;
                        _tapLineRenderer.sprite = skin.GuideLines[2];
                        _exRenderer.color = skin.ExEffects[2];
                    }
                }
                else
                {
                    _thisRenderer.sprite = skin.Normal;
                    _exRenderer.sprite = skin.Ex;

                    if (IsEach)
                    {
                        _thisRenderer.sprite = skin.Each;
                        _tapLineRenderer.sprite = skin.GuideLines[1];
                        _exRenderer.color = skin.ExEffects[1];
                    }
                    if (IsBreak)
                    {
                        _thisRenderer.sprite = skin.Break;
                        _thisRenderer.sharedMaterial = BreakMaterial;
                        _tapLineRenderer.sprite = skin.GuideLines[2];
                        _exRenderer.color = skin.ExEffects[2];
                    }
                }
            }
        }
    }
}
