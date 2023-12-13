using System.Collections.Generic;
using Commons.Music.Midi;
using DrumGame.Game.Interfaces;

namespace DrumGame.Game.Beatmaps.Data;

public class TempoChange : IBeatmapChangePoint<TempoChange>, IMidiEvent
{
    public readonly int Time;
    public readonly Tempo Tempo;
    public TempoChange(int time, Tempo tempo)
    {
        Time = time;
        Tempo = tempo;
    }
    // default microseconds per quarter note
    public readonly static Tempo DefaultTempo = new Tempo { MicrosecondsPerQuarterNote = 500_000 };
    public static TempoChange Default => new TempoChange(0, DefaultTempo);
    public double BPM => Tempo.BPM;
    public double HumanBPM => Tempo.HumanBPM;
    public int MicrosecondsPerQuarterNote => Tempo.MicrosecondsPerQuarterNote;
    public double MsPerBeat => MicrosecondsPerQuarterNote / 1000.0;

    int ITickTime.Time => Time;

    public TempoChange WithTime(int time) => new TempoChange(time, Tempo);
    public TempoChange WithTempo(int microseconds) =>
        new TempoChange(Time, new Tempo { MicrosecondsPerQuarterNote = microseconds });
    public TempoChange WithTempo(Tempo tempo) =>
        new TempoChange(Time, tempo);
    public bool Congruent(TempoChange o)
    {
        if (o == null) return false;
        return o.Tempo.MicrosecondsPerQuarterNote == Tempo.MicrosecondsPerQuarterNote;
    }
    public static List<TempoChange> GetList(Beatmap beatmap) => beatmap.TempoChanges;
    public MidiEvent MidiEvent()
    {
        var ms = MicrosecondsPerQuarterNote;
        return new MidiEvent(Commons.Music.Midi.MidiEvent.Meta, MidiMetaType.Tempo, 0, new byte[] {
                    (byte)((ms >> 16) & 255),
                    (byte)((ms >> 8) & 255),
                    (byte)(ms & 255),
                }, 0, 3);
    }
}
