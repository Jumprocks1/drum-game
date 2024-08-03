using System;
using System.Collections.Generic;
using Commons.Music.Midi;
using DrumGame.Game.Interfaces;

namespace DrumGame.Game.Beatmaps.Data;

public class MeasureChange : IBeatmapChangePoint<MeasureChange>, IMidiEvent
{
    public readonly int Time;
    // should probably allow this to run off of Ticks instead of Beats, but that may cause problems with storing the TickRate somewhere
    public readonly double Beats;
    public MeasureChange(int time, double beats)
    {
        Time = time;
        Beats = beats;
    }
    public int Ticks(Beatmap beatmap) => Ticks(beatmap.TickRate);
    public int Ticks(int tickRate) => (int)(Beats * tickRate);
    int ITickTime.Time => Time;
    public static MeasureChange Default => new MeasureChange(0, DefaultBeats);
    public const double DefaultBeats = 4;
    public MeasureChange WithTime(int time) => new MeasureChange(time, Beats);
    public MeasureChange WithBeats(double beats) => new MeasureChange(Time, beats);
    public bool Congruent(MeasureChange other) => other == null ? false : other.Beats == Beats;
    public static List<MeasureChange> GetList(Beatmap beatmap) => beatmap.MeasureChanges;

    public MidiEvent MidiEvent()
    {
        var num = Beats;
        byte den = 2;
        while (num % 1 != 0 && num < 128)
        {
            den += 1;
            num *= 2;
        }
        return new MidiEvent(Commons.Music.Midi.MidiEvent.Meta, MidiMetaType.TimeSignature, 4,
            new byte[] { (byte)num, den, 24, 8 }, 0, 4);
    }

    public override string ToString() => $"{Beats}beats/measure at {Time}";
}
