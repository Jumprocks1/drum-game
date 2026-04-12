using DrumGame.Game.Media;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using System;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Timing;

public enum MetronomeMode
{
    // value = 0 is the default for practice mode
    LeadIn,
    Disabled,
    Always
}

public class GlobalMetronome(BeatmapPlayer _player, DrumsetAudioPlayer _drumset) : Metronome(_player, _drumset)
{
    protected override double GetNextBeat(int currentMeasure, double currentBeat)
    {
        var next = base.GetNextBeat(currentMeasure, currentBeat);
        var config = Util.ConfigManager;
        var mode = config.MetronomeModeBindable.Value;
        if (mode == MetronomeMode.Disabled)
            return double.PositiveInfinity;
        if (mode == MetronomeMode.LeadIn)
        {
            var firstBeat = Beatmap.FirstBeat;
            if (next > firstBeat)
                return double.PositiveInfinity;
        }
        return next;
    }
}

public abstract class Metronome(BeatmapPlayer Player, DrumsetAudioPlayer Drumset) : IBeatEventHandler
{
    protected Beatmap Beatmap => Player.Beatmap;
    double nextBeat = double.NaN;
    double nextBeatTime = double.NaN;
    bool measureBeat; // if nextBeatTime is a measure start beat
    public void SkipTo(int _, double __)
    {
        nextBeatTime = double.NaN;
        Queue.UnbindAndClear();
    }

    // if current beat is an integer, this should NOT return that beat
    protected virtual double GetNextBeat(int currentMeasure, double currentBeat)
    {
        var measureStart = (double)Beatmap.TickFromMeasureNegative(currentMeasure) / Beatmap.TickRate;
        return measureStart + Math.Floor(currentBeat - measureStart + Beatmap.BeatEpsilon) + 1;
    }

    void UpdateNextBeat(double currentBeat)
    {
        var currentMeasure = Beatmap.MeasureFromTickNegative(Beatmap.TickFromBeatSlow(currentBeat));

        nextBeat = GetNextBeat(currentMeasure, currentBeat);
        if (double.IsPositiveInfinity(nextBeat))
        {
            nextBeatTime = double.PositiveInfinity;
            return;
        }

        var nextMeasure = Beatmap.BeatFromMeasure(currentMeasure + 1);

        measureBeat = nextBeat >= nextMeasure;
        if (measureBeat) nextBeat = nextMeasure;

        nextBeatTime = Beatmap.MillisecondsFromBeat(nextBeat);
    }

    SyncQueue Queue = new();

    public virtual DrumChannel Channel => DrumChannel.Metronome;

    public virtual DrumChannelEvent MakeEvent(bool measureBeat) => new(0, Channel, measureBeat ? (byte)1 : (byte)2);

    public void TriggerThrough(int _, BeatClock clock, bool __)
    {
        if (double.IsNaN(nextBeatTime))
            UpdateNextBeat(clock.CurrentBeat);
        var time = clock.CurrentTime;
        var playbackSpeed = clock.PlaybackSpeed.Value;
        if (time + IBeatEventHandler.Prefire * playbackSpeed > nextBeatTime)
        {
            Drumset.PlayAt(MakeEvent(measureBeat), clock, nextBeatTime, Queue);
            UpdateNextBeat(nextBeat);
        }
    }
}
