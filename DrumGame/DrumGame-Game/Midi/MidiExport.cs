using System.Collections.Generic;
using System.IO;
using System.Linq;
using Commons.Music.Midi;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Replay;
using DrumGame.Game.Channels;
using DrumGame.Game.Interfaces;

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
    static List<int> ComputeNoteEnds(List<HitObject> hitObjects, int tickRate)
    {
        var res = new List<int>();

        // this could be optimized slighty (probably 2x speed), but it's not really necessary
        for (var i = 0; i < hitObjects.Count; i++)
        {
            var e = hitObjects[i];
            // TODO for now, this only supports integer beats (doesn't work for something like 7/8 - see NoteGroup.cs for the solution)
            var nextNote = (e.Time / tickRate + 1) * tickRate;
            for (var j = i + 1; j < hitObjects.Count && hitObjects[j].Time < nextNote; j++)
            {
                if (hitObjects[j].Voice == e.Voice && hitObjects[j].Time > e.Time)
                {
                    nextNote = hitObjects[j].Time;
                    break;
                }
            }
            res.Add(nextNote - 1);
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

        var ends = ComputeNoteEnds(beatmap.HitObjects, beatmap.TickRate);
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