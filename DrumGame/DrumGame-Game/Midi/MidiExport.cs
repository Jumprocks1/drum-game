using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Commons.Music.Midi;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Replay;
using DrumGame.Game.Channels;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Midi;

public static class MidiExport
{
    public const byte MidiDrumChannel = 9;
    public static void WriteToStream(Stream stream, int tickRate, IOrderedEnumerable<IMidiEvent> events)
    {
        var music = new MidiMusic();
        music.DeltaTimeSpec = (short)tickRate;
        var track = new MidiTrack();
        var time = 0;
        foreach (var e in events)
        {
            var delta = e.Time - time;
            track.Messages.Add(new MidiMessage(delta, e.MidiEvent()));
            time = e.Time;
        }

        track.Messages.Add(new MidiMessage(tickRate, new MidiEvent(MidiEvent.Meta, MidiMetaType.EndOfTrack, 0, new byte[0], 0, 0)));

        music.AddTrack(track);

        var writer = new SmfWriter(stream);
        writer.WriteMusic(music);
        stream.Dispose();
    }
    class NoteOff : IMidiEvent
    {
        public int Time { get; set; }
        byte noteNumber;
        public NoteOff(int time, DrumChannel channel)
        {
            Time = time;
            noteNumber = channel.MidiNote();
        }
        public MidiEvent MidiEvent()
        {
            return new MidiEvent((byte)(Commons.Music.Midi.MidiEvent.NoteOn | MidiExport.MidiDrumChannel),
                noteNumber, 0, null, 0, 0);
        }
    }

    public class NoteGroup
    {
        public readonly List<int> NoteIndices = [];
        public readonly int StartTick;
        public NoteGroup(int startTick)
        {
            StartTick = startTick;
        }
        public int Count => NoteIndices.Count;
        public void Add(int i) => NoteIndices.Add(i);
    }

    public static IEnumerable<NoteGroup> GetGroups(Beatmap beatmap)
        => GetGroups(beatmap.TickRate, beatmap.HitObjects, beatmap.MeasureChanges);
    // mostly copied from NoteGroup.cs
    // could probably change NoteGroup.cs to call this first
    // yields all groups for non-foot voice first
    public static IEnumerable<NoteGroup> GetGroups(int tickRate, List<HitObject> hits, List<MeasureChange> measures)
    {
        var voices = new bool[] { false, true };
        foreach (var foot in voices)
        {
            var currentMeasureChange = -1;
            var measureChangeOffset = 0; // in ticks
            var ticksPerMeasure = Beatmap.TickFromBeat(MeasureChange.DefaultBeats, tickRate);
            var nextMeasureChange = measures.Count > (currentMeasureChange + 1) ? measures[currentMeasureChange + 1].Time : int.MaxValue;

            NoteGroup currentGroup = null;
            var groupEnd = int.MinValue;

            for (var i = 0; i < hits.Count; i++)
            {
                var note = hits[i];
                if (note.IsFoot == foot)
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
                        if (currentGroup != null) yield return currentGroup;
                        var offsetTick = note.Time - measureChangeOffset;
                        var groupMeasure = offsetTick / ticksPerMeasure;
                        // this is the local beat in the current measure (with the start of the measure being the 0th beat exactly)
                        var measureBeat = (offsetTick - groupMeasure * ticksPerMeasure) / tickRate;
                        var measureStart = groupMeasure * ticksPerMeasure + measureChangeOffset;
                        // the part in Math.Min represents the group end relative to the start of the current measure
                        groupEnd = measureStart + Math.Min(ticksPerMeasure, (measureBeat + 1) * tickRate);
                        currentGroup = new(measureStart + measureBeat * tickRate);
                    }
                    currentGroup.Add(i);
                }
            }
            if (currentGroup != null) yield return currentGroup;
        }
    }
    public static int[] ComputeNoteEnds(Beatmap beatmap)
    {
        var hitObjects = beatmap.HitObjects;
        var tickRate = beatmap.TickRate;

        var res = new int[hitObjects.Count];

        foreach (var group in GetGroups(tickRate, hitObjects, beatmap.MeasureChanges))
        {
            var gcd = tickRate;
            foreach (var i in group.NoteIndices)
                gcd = Util.GCD(gcd, hitObjects[i].Time - group.StartTick);
            foreach (var i in group.NoteIndices)
                res[i] = hitObjects[i].Time + gcd - 1;
        }
        return res;
    }
    public static void WriteToStream(Stream stream, Beatmap beatmap)
    {
        // we put tempo changes first so when they have the same time as a note, the tempo change comes first
        var events = beatmap.TempoChanges.AsEnumerable<IMidiEvent>().Concat(beatmap.HitObjects).ToList();

        if (beatmap.MeasureChanges.Count > 0)
        {
            var measures = new List<MeasureChange>(beatmap.MeasureChanges);
            if (measures[0].Time != 0)
                measures.Insert(0, MeasureChange.Default);
            events.AddRange(measures);
        }

        var ends = ComputeNoteEnds(beatmap);
        for (var i = 0; i < beatmap.HitObjects.Count; i++)
            events.Add(new NoteOff(ends[i], beatmap.HitObjects[i].Channel));
        WriteToStream(stream, beatmap.TickRate, events.OrderBy(e => e.Time));
    }
    public static void WriteToStream(Stream stream, Beatmap beatmap, BeatmapReplay replay)
    {
        // var events = replay.Events
        //     .Select<InputChannelEvent, IMidiEvent>(e => new InputChannelEventTickTime(beatmap, e))
        //     .Where(e => e.Time >= 0)
        //     .Concat(beatmap.TempoChanges).OrderBy(e => e.Time);
        // WriteToStream(stream, beatmap.TickRate, events);
    }
}