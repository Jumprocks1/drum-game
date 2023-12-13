using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Utils;
using osu.Framework.Extensions.EnumExtensions;

namespace DrumGame.Game.Notation;

// Represents a set of notes that are in the same voice and the exact same time
public class Flag
{
    public TimedNote BottomNote;
    public bool Accented;
    public float FlagLeft;
    public List<TimedNote> Notes = new();
    public double EffectiveDuration;
    public double Time;
    public Flag(TimedNote note)
    {
        Notes.Add(note);
        Time = note.Time;
        BottomNote = note;
        if (note.Modifiers.HasFlagFast(NoteModifiers.Accented)) Accented = true;
    }
    public void AddNote(TimedNote note, bool down)
    {
        if (note.Modifiers.HasFlagFast(NoteModifiers.Accented)) Accented = true;
        if (down ? BottomNote.Note.Position > note.Note.Position
             : BottomNote.Note.Position < note.Note.Position) BottomNote = note;
        Notes.Add(note);
    }
}
// A note group is a collection of flags that are all under the same beam
// Ex: adjacent eighth notes in the same voice
// Different voices (feet vs hands) are not in the same note group
// Note groups should not persist across measure boundaries
public class NoteGroup
{
    public readonly bool Down;
    public readonly List<Flag> Flags;
    public readonly int HighestNote;
    public readonly int Beat;
    public readonly double GroupEnd;
    public NoteGroup(int groupBeat, List<Flag> flags, bool down, int highestNote, double groupEnd)
    {
        Beat = groupBeat;
        Down = down;
        HighestNote = down ? int.MinValue : int.MaxValue;
        Flags = flags;
        HighestNote = highestNote;
        GroupEnd = groupEnd;
    }

    // this does not respect voices or measure boundaries
    // expects hits to start at beat 0
    public static NoteGroup Create(int tickRate, List<HitObject> hits, bool foot = false, double groupEnd = 1)
    {
        var currentGroup = new List<Flag>();
        var highestNote = int.MinValue; // highest (or lowest for feet) note position in group
        Flag currentFlag = null;
        var flagTime = int.MinValue; // ticks
        foreach (var note in hits)
        {
            var tn = new TimedNote(tickRate, note);
            if (note.Time == flagTime)
            {
                currentFlag.AddNote(tn, foot);
                if (foot ? tn.Note.Position > highestNote : tn.Note.Position < highestNote)
                    highestNote = tn.Note.Position;
            }
            else
            {
                if (highestNote == int.MinValue || (foot ? tn.Note.Position > highestNote : tn.Note.Position < highestNote))
                    highestNote = tn.Note.Position;

                currentGroup.Add(currentFlag = new Flag(tn));
                flagTime = note.Time;
            }
        }
        return new NoteGroup(0, currentGroup, foot, highestNote, groupEnd);
    }

    private static readonly bool[] Voices = new[] { false, true };

    public static IEnumerable<NoteGroup> GetGroupsInRange(Beatmap beatmap, int beatStart, int beatEnd)
        => GetGroups(beatmap.TickRate, beatmap.HitObjects, beatmap.MeasureChanges, beatStart, beatEnd);
    // ~1ms for through the fire and flames with no filter
    // the filter makes it >100 times faster
    public static IEnumerable<NoteGroup> GetGroups(int tickRate, List<HitObject> hits, List<MeasureChange> measures, int beatStart, int beatEnd)
    {
        // note that the split beats are not necessarily on integers
        // there can even be multiple groups with the same groupBeat

        // These are close but not perfect
        // since these are not perfect, we have to keep the filters in the grouping code
        // for a 100% integer measure map, these would be easy to calculate
        // if there are measures like 3.5 or 3.25, it's not so easy
        var tickStart = (beatStart - 1) * tickRate;
        var tickEnd = (beatEnd + 1) * tickRate;
        var indexStart = Math.Max(0, hits.BinarySearchFirst(tickStart));
        var indexEnd = Math.Min(hits.Count, hits.BinarySearchThrough(tickEnd)); // exclusive

        foreach (var foot in Voices)
        {
            var currentMeasureChange = -1;
            var measureChangeOffset = 0; // in ticks
            var ticksPerMeasure = Beatmap.TickFromBeat(MeasureChange.DefaultBeats, tickRate);
            var nextMeasureChange = measures.Count > (currentMeasureChange + 1) ? measures[currentMeasureChange + 1].Time : int.MaxValue;

            List<Flag> currentGroup = null;
            var highestNote = 0; // highest (or lowest for feet) note position in group
                                 // initial value not used since flagTime never matches
            Flag currentFlag = null;
            var flagTime = int.MinValue; // ticks
            var groupBeat = int.MinValue; // used to determine container that group will go in, not a big deal
                                          // can repeat between groups
            var groupEnd = int.MinValue;

            for (var i = indexStart; i < indexEnd; i++)
            {
                var note = hits[i];
                if (note.IsFoot == foot)
                {
                    var tn = new TimedNote(tickRate, note);
                    if (note.Time == flagTime)
                    {
                        currentFlag.AddNote(tn, foot);
                        if (foot ? tn.Note.Position > highestNote : tn.Note.Position < highestNote)
                            highestNote = tn.Note.Position;
                    }
                    else
                    {
                        while (note.Time >= nextMeasureChange)
                        {
                            currentMeasureChange++;
                            measureChangeOffset = measures[currentMeasureChange].Time;
                            ticksPerMeasure = Beatmap.TickFromBeat(measures[currentMeasureChange].Beats, tickRate);
                            nextMeasureChange = measures.Count > (currentMeasureChange + 1) ? measures[currentMeasureChange + 1].Time : int.MaxValue;
                        }

                        if (note.Time >= groupEnd)
                        {
                            if (currentGroup != null)
                            {
                                if (groupBeat >= beatStart && groupBeat < beatEnd)
                                    yield return new NoteGroup(groupBeat, currentGroup, foot, highestNote, (double)groupEnd / tickRate);
                            }
                            currentGroup = new();
                            var offsetTick = note.Time - measureChangeOffset;
                            var groupMeasure = offsetTick / ticksPerMeasure;
                            // this is the local beat in the current measure (with the start of the measure being the 0th beat exactly)
                            var measureBeat = (offsetTick - groupMeasure * ticksPerMeasure) / tickRate;
                            // the part in Math.Min represents the group end relative to the start of the current measure
                            groupEnd = groupMeasure * ticksPerMeasure + Math.Min(ticksPerMeasure,
                                (measureBeat + 1) * tickRate) + measureChangeOffset;
                            // groupBeat is the integer part of the beat that the group starts in
                            // note that this is NOT the integer part of the current flag,
                            ///   since the flag could jump over to the next integer beat
                            groupBeat = (measureBeat * tickRate
                                + groupMeasure * ticksPerMeasure + measureChangeOffset) / tickRate;
                            highestNote = tn.Note.Position;
                        }
                        else
                        {
                            if (foot ? tn.Note.Position > highestNote : tn.Note.Position < highestNote)
                                highestNote = tn.Note.Position;
                        }

                        currentGroup.Add(currentFlag = new Flag(tn));
                        flagTime = note.Time;
                    }
                }
            }
            // close pending group
            if (currentGroup != null)
                if (groupBeat >= beatStart && groupBeat < beatEnd)
                    yield return new NoteGroup(groupBeat, currentGroup, foot, highestNote, (double)groupEnd / tickRate);
        }
    }
}

