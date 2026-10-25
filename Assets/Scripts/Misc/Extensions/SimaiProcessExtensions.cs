using MajdataPlay.Buffers;
using MajdataPlay.Numerics;
using MajSimai;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
#nullable enable
namespace MajdataPlay.Extensions
{
    public static class SimaiProcessExtensions
    {
        public static SimaiChart Scale(this SimaiChart source,float timeScale)
        {
            var timingPoints = source.NoteTimings;
            foreach(var timingPoint in timingPoints)
            {
                timingPoint.Bpm *= timeScale;
                timingPoint.Timing /= timeScale;
                foreach(var note in timingPoint.Notes)
                {
                    note.HoldTime /= timeScale;
                    note.SlideStartTime /= timeScale;
                    note.SlideTime /= timeScale;
                }
            }
            return source;
        }
        public static SimaiChart ConvertToBreak(this SimaiChart source)
        {
            var timingPoints = source.NoteTimings;
            foreach (var timingPoint in timingPoints)
            {
                foreach (var note in timingPoint.Notes)
                {
                    note.IsBreak = true;
                    note.IsSlideBreak = true;
                }
            }
            return source;
        }
        public static SimaiChart ConvertToEx(this SimaiChart source)
        {
            var timingPoints = source.NoteTimings;
            foreach (var timingPoint in timingPoints)
            {
                foreach (var note in timingPoint.Notes)
                    note.IsEx = true;
            }
            return source;
        }
        public static SimaiChart ConvertToTouch(this SimaiChart source)
        {
            var timingPoints = source.NoteTimings;
            foreach (var timingPoint in timingPoints)
            {
                var notes = timingPoint.Notes;
                var touchNotes = notes.Where(x => x.Type is SimaiNoteType.Touch or SimaiNoteType.TouchHold);
                var newNoteList = new List<SimaiNote>();
                var noteCount = notes.Count();
                for (var i = 0; i < noteCount; i++)
                {
                    var note = notes[i];
                    switch(note.Type)
                    {
                        case SimaiNoteType.Touch:
                        case SimaiNoteType.TouchHold:
                            continue;
                        case SimaiNoteType.Slide:
                            {
                                if (note.IsSlideNoHead)
                                {
                                    newNoteList.Add(note);
                                    continue;
                                }
                                note.IsSlideNoHead = true;
                                var startKey = note.StartPosition;
                                newNoteList.Add(note);
                                newNoteList.Add(new SimaiNote()
                                {
                                    Type = SimaiNoteType.Touch,
                                    StartPosition = startKey,
                                    TouchArea = 'A',
                                    IsBreak = note.IsBreak,
                                    IsEx = note.IsEx
                                });
                            }
                            break;
                        case SimaiNoteType.Tap:
                            {
                                var startKey = note.StartPosition;
                                newNoteList.Add(new SimaiNote()
                                {
                                    Type = SimaiNoteType.Touch,
                                    StartPosition = startKey,
                                    TouchArea = 'A',
                                    IsBreak = note.IsBreak,
                                    IsEx = note.IsEx
                                });
                            }
                            break;
                        case SimaiNoteType.Hold:
                            {
                                var startKey = note.StartPosition;
                                newNoteList.Add(new SimaiNote()
                                {
                                    Type = SimaiNoteType.TouchHold,
                                    StartPosition = startKey,
                                    HoldTime = note.HoldTime,
                                    TouchArea = 'A',
                                    IsBreak = note.IsBreak,
                                    IsEx = note.IsEx
                                });
                            }
                            break;
                    }
                }
                foreach (var touch in touchNotes)
                    newNoteList.Add(touch);
                timingPoint.Notes = newNoteList.ToArray();
            }
            return source;
        }
        /// <summary>
        /// 下落式 Touch 转 Tap：Touch/TouchHold 换成所在轨道的 Tap/Hold（A/B/D/E 的第 n 区对应第 n 列）。
        /// 中心 C 区换成 StartPosition 为 4、TouchArea 仍为 'C' 的 Tap/Hold，由 NoteLoader 识别为落在中间两列之间。
        /// 同一时刻同一列已有 Tap、Hold 或星星头时丢弃转换出的音符，避免一列叠两个音符。
        /// </summary>
        internal static SimaiChart ConvertTouchToLaneTap(this SimaiChart source)
        {
            const int CENTER_BIT = 1 << 8;
            var timingPoints = source.NoteTimings;
            foreach (var timingPoint in timingPoints)
            {
                var notes = timingPoint.Notes;
                var occupied = 0;
                foreach (var note in notes)
                {
                    var isLaneNote = note.Type is SimaiNoteType.Tap or SimaiNoteType.Hold ||
                                     (note.Type == SimaiNoteType.Slide && !note.IsSlideNoHead);
                    if (isLaneNote && note.StartPosition is >= 1 and <= 8)
                    {
                        occupied |= 1 << (note.StartPosition - 1);
                    }
                }
                var newNoteList = new List<SimaiNote>(notes.Length);
                var isChanged = false;
                foreach (var note in notes)
                {
                    if (note.Type is not (SimaiNoteType.Touch or SimaiNoteType.TouchHold))
                    {
                        newNoteList.Add(note);
                        continue;
                    }
                    isChanged = true;
                    var isCenter = note.TouchArea == Scenes.Game.Lane.LaneLayout.CENTER_TOUCH_AREA;
                    var bit = isCenter ? CENTER_BIT : 1 << (note.StartPosition - 1);
                    if ((occupied & bit) != 0)
                    {
                        continue;
                    }
                    occupied |= bit;
                    note.Type = note.Type == SimaiNoteType.TouchHold ? SimaiNoteType.Hold : SimaiNoteType.Tap;
                    if (isCenter)
                    {
                        note.StartPosition = Scenes.Game.Lane.LaneLayout.CENTER_LANE;
                    }
                    else
                    {
                        note.TouchArea = ' ';
                    }
                    note.IsHanabi = false;
                    newNoteList.Add(note);
                }
                if (isChanged)
                {
                    timingPoint.Notes = newNoteList.ToArray();
                }
            }
            return source;
        }
        public static SimaiChart Clamp(this SimaiChart source,Range<long> noteIndexRange)
        {
            using PooledList<SimaiTimingPoint> newTimingList = new();
            var currentIndex = 0;
            for (var i = 0; i < source.NoteTimings.Length; i++)
            {
                var noteTiming = source.NoteTimings[i];
                List<SimaiNote> newNoteList = new();
                for (var j = 0; j < noteTiming.Notes.Length; j++)
                {
                    var note = noteTiming.Notes[j];
                    switch (note.Type)
                    {
                        case SimaiNoteType.Tap:
                        case SimaiNoteType.Hold:
                        case SimaiNoteType.Touch:
                        case SimaiNoteType.TouchHold:
                            if (noteIndexRange.InRange(currentIndex))
                            {
                                newNoteList.Add(note);
                            }
                            break;
                        case SimaiNoteType.Slide:
                            if (note.IsSlideNoHead)
                            {
                                if (noteIndexRange.InRange(currentIndex))
                                {
                                    newNoteList.Add(note);
                                }
                            }
                            else
                            {
                                if (noteIndexRange.InRange(currentIndex + 1))
                                {
                                    newNoteList.Add(note);
                                    currentIndex++;
                                }
                                else if (noteIndexRange.InRange(currentIndex))
                                {
                                    newNoteList.Add(new()
                                    {
                                        Type = SimaiNoteType.Tap,
                                        IsForceStar = true,
                                        IsFakeRotate = true,
                                        IsBreak = note.IsBreak,
                                        IsEx = note.IsEx,
                                        RawContent = $"{note.StartPosition}",
                                        StartPosition = note.StartPosition,
                                    });
                                }
                            }
                            break;
                    }
                    currentIndex++;
                }
                if (newNoteList.Count != 0) 
                {
                    var newTimingPoint = new SimaiTimingPoint(noteTiming.Timing,
                                                              newNoteList.ToArray(),
                                                              noteTiming.RawContent,
                                                              noteTiming.RawTextPositionX,
                                                              noteTiming.RawTextPositionY,
                                                              noteTiming.Bpm, 
                                                              noteTiming.HSpeed);
                    newTimingList.Add(newTimingPoint);
                }
            }
            using (var buffer = Pool<SimaiTimingPoint>.Rent(newTimingList.Count))
            {
                newTimingList.CopyTo(buffer);
                return new SimaiChart(source.Level, source.Designer, source.Fumen, buffer.AsSpan(0, newTimingList.Count), null);
            }
            //source.NoteTimings = newTimingList.ToArray();
        }
        public static SimaiChart Clamp(this SimaiChart source, Range<double> timestampRange)
        {
            using PooledList<SimaiTimingPoint> newTimingList = new();
            foreach(var noteTiming in source.NoteTimings)
            {
                if(timestampRange.InRange(noteTiming.Timing))
                {
                    newTimingList.Add(noteTiming);
                }
            }
            //source.NoteTimings = newTimingList.ToArray();
            using (var buffer = Pool<SimaiTimingPoint>.Rent(newTimingList.Count, true))
            {
                newTimingList.CopyTo(buffer);
                return new SimaiChart(source.Level, source.Designer, source.Fumen, buffer.AsSpan(0, newTimingList.Count), null);
            }
        }
        public static SimaiChart AddOffset(this SimaiChart source, float timingOffsetMSec)
        {
            var noteTimingCount = source.NoteTimings.Length;
            var commaTimingCount = source.CommaTimings.Length;

            Parallel.For(0, noteTimingCount, i =>
            {
                ref readonly var tp = ref source.NoteTimings[i];
                tp.Timing += timingOffsetMSec;
                for (var k = 0; k < tp.Notes.Length; k++)
                {
                    ref var note = ref tp.Notes[k];
                    switch(note.Type)
                    {
                        case SimaiNoteType.Slide:
                            note.SlideStartTime += timingOffsetMSec;
                            break;
                    }
                }
            });
            Parallel.For(0, commaTimingCount, i =>
            {
                ref readonly var tp = ref source.CommaTimings[i];
                tp.Timing += timingOffsetMSec;
            });

            return source;
        }
    }
}