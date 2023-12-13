using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Channels;
using DrumGame.Game.IO.Midi;

namespace DrumGame.Game.Beatmaps.Loaders;

public static class MidiLoader
{
    public static Dictionary<int, DrumChannel> BRSNoteMapping = new Dictionary<int, DrumChannel>
        {
            // 14 = choke
            {33, DrumChannel.Snare}, // ghost note
            {39, DrumChannel.Snare},
            {62, DrumChannel.OpenHiHat},
            {64, DrumChannel.OpenHiHat},
            {61, DrumChannel.ClosedHiHat},
            {42, DrumChannel.HiHatPedal}
        };
    public static Beatmap LoadMidi(Beatmap map, string path)
    {
        using var midiStream = File.OpenRead(path);
        using var reader = new MidiReader(midiStream);
        var midi = reader.ReadFile();

        var tickRate = midi.Header.quarterNote;
        var hitObjects = new List<HitObject>();
        map.MeasureChanges = new List<MeasureChange>();

        var hasChannel9 = midi.Tracks.Any(e => e.events.Any(e => e is MidiTrack.MidiEvent me && me.channel == 9));

        var maxT = 0;
        foreach (var track in midi.Tracks)
        {
            var t = 0;
            foreach (var ev in track.events)
            {
                t += ev.delta;
                if (ev is MidiTrack.MidiEvent me)
                {
                    if (me.type == 9) // note on event
                    {
                        if (me.parameter2 > 0)
                        {
                            var channel = DrumChannel.None;
                            if (me.channel != 9 && hasChannel9) continue;
                            if (me.channel == 0)
                            {
                                BRSNoteMapping.TryGetValue(me.parameter1, out channel);
                            }
                            if (channel == DrumChannel.None)
                            {
                                channel = ChannelMapping.ImportMidiMapping(me.parameter1);
                            }
                            if (channel != DrumChannel.None)
                            {
                                var modifiers = NoteModifiers.None;
                                if (me.parameter2 >= 120)
                                    modifiers = NoteModifiers.Accented;
                                else if (me.parameter2 <= 40)
                                    modifiers = NoteModifiers.Ghost;
                                hitObjects.Add(new HitObject(t, new HitObjectData(channel, modifiers)));
                            }
                            else
                            {
                                Console.WriteLine($"Unknown MIDI note: {me.parameter1} at {(double)t / tickRate} {me.parameter2}");
                            }
                        }
                    }
                }
                else if (ev is MidiTrack.TempoEvent te)
                {
                    map.UpdateChangePoint<TempoChange>(t, t => t.WithTempo(te.MicrosecondsPerQuarterNote));
                }
                else if (ev is MidiTrack.TimingEvent ts)
                {
                    var bpMeasure = (double)(ts.Numerator * 4) / ts.Denominator;
                    map.UpdateChangePoint<MeasureChange>(t, t => t.WithBeats(bpMeasure));
                }
            }
            maxT = Math.Max(maxT, t);
        }
        map.TickRate = tickRate;
        map.QuarterNotes = Math.Ceiling((double)maxT / tickRate);
        // We use OrderBy instead of List.Sort since OrderBy is stable sort
        map.HitObjects = hitObjects.OrderBy(e => e.Time).ToList();
        map.TempoChanges = map.TempoChanges.OrderBy(e => e.Time).ToList();
        map.MeasureChanges = map.MeasureChanges.OrderBy(e => e.Time).ToList();
        map.RemoveExtras<TempoChange>();
        map.RemoveExtras<MeasureChange>();
        map.RemoveDuplicates();
        return map;
    }
}

