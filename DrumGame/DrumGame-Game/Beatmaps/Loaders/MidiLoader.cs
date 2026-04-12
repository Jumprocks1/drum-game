using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Formats;
using DrumGame.Game.Channels;
using DrumGame.Game.IO.Midi;
using DrumGame.Game.Utils;
using osu.Framework.Logging;

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
            {65, DrumChannel.HiHatPedal},
            {42, DrumChannel.HiHatPedal},
            {67, DrumChannel.ClosedHiHat},
            {68, DrumChannel.OpenHiHat},
            {69, DrumChannel.OpenHiHat},
            {70, DrumChannel.OpenHiHat},
            {71, DrumChannel.OpenHiHat},
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

        var pendingNotes = new List<(int t, HitObjectData data, byte velocity)>();

        var usedMappings = new Dictionary<HitObjectData, HashSet<byte>>();

        var maxT = 0;
        foreach (var track in midi.Tracks)
        {
            var t = 0;
            foreach (var ev in track.events)
            {
                t = ev.time;
                if (ev is MidiTrack.MidiEvent me)
                {
                    if (me.type == 9) // note on event
                    {
                        if (me.parameter2 > 0)
                        {
                            var channel = DrumChannel.None;
                            if (me.channel != 9 && hasChannel9 || track.Name == "Sample Electronic Beat"
                                || track.Name == "Sample Perc.") continue;
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
                                if (me.parameter1 == 49)
                                    modifiers |= NoteModifiers.Left;
                                if (me.parameter1 == 57 || me.parameter1 == 52)
                                    modifiers |= NoteModifiers.Right;
                                var data = new HitObjectData(channel, modifiers);
                                pendingNotes.Add((t, data, me.parameter2));
                                usedMappings.HashAdd(data, me.parameter1);
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
        foreach (var (data, usedMidi) in usedMappings)
        {
            if (usedMidi.Count > 1)
                Console.WriteLine($"{string.Join(", ", usedMidi)} mapped to {data}");
        }
        var noteMap = new Dictionary<(HitObjectData data, byte velocity), HitObjectData>();
        var groupedPending = pendingNotes.GroupBy(e => e.data).Select(e => new
        {
            Data = e.Key,
            Velocities = e.GroupBy(e => e.velocity).OrderBy(e => e.Key).Select(e => (e.Key, e.Count())).ToList()
        }).ToList();
        const int velocityResolution = 5; // ignore velocities that are closer than this
        foreach (var group in groupedPending)
        {
            var vs = group.Velocities;
            if (vs.Count == 1) continue;

            var remove = new List<int>();

            var target = vs.Count - 1;
            var weightedTotal = vs[target].Key * vs[target].Item2;

            for (var i = vs.Count - 2; i >= 0; i--)
            {
                var v = vs[i];
                if (Math.Abs(vs[target].Key - v.Key) <= velocityResolution)
                {
                    weightedTotal += v.Key * v.Item2;
                    var newCount = vs[target].Item2 + v.Item2;
                    vs[target] = ((byte)(weightedTotal / newCount), newCount);
                    remove.Add(i);
                }
                else
                {
                    target = i;
                    weightedTotal = v.Key * v.Item2;
                }
            }
            foreach (var i in remove) vs.RemoveAt(i);
        }
        (map.NotePresets ??= new()).Clear();
        foreach (var channelGroup in groupedPending)
        {
            var data = channelGroup.Data;
            var velocities = channelGroup.Velocities;
            if (velocities.Count == 1) continue;
            if (velocities.Count > 1)
            {
                var baseLine = velocities[^1].Key; // TODO should probably be the mode
                foreach (var velocity in velocities)
                {
                    if (velocity.Key == baseLine)
                        noteMap[(data, velocity.Key)] = data;
                    else
                    {
                        var preset = new NotePreset
                        {
                            Key = $"{data.Channel}_{velocity.Key}",
                            Channel = data.Channel,
                            // ^0.75 makes the size not so extreme
                            Size = (float)Math.Round(Math.Pow((double)velocity.Key / baseLine, 0.75), 2),
                            Volume = (float)Math.Round((double)velocity.Key / baseLine, 2),
                        };
                        map.NotePresets.Add(preset);
                        noteMap[(data, velocity.Key)] = new HitObjectData(data.Channel, data.Modifiers, preset: preset);
                    }
                }
            }
        }
        foreach (var (t, data, velocity) in pendingNotes)
        {
            var mappedData = noteMap.TryGetValue((data, velocity), out var o) ? o : data;
            hitObjects.Add(new HitObject(t, mappedData));
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
        map.Print3Hands();
        return map;
    }

    public static Beatmap ImportFileAndSave(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path) + ".bjson";
        var outputTarget = Path.Join(Util.MapStorage.AbsolutePath, name);

        var map = Beatmap.Create();
        map.TempoChanges = [];
        LoadMidi(map, path);
        map.Source = new BJsonSource(outputTarget, BJsonFormat.Instance);
        map.Export();
        map.SaveToDisk(Util.MapStorage);
        Logger.Log($"imported {path} to {outputTarget}");
        return map;
    }
}

