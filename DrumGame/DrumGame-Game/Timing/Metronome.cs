using DrumGame.Game.Media;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using System;

namespace DrumGame.Game.Timing;

public class Metronome(BeatmapPlayer Player, DrumsetAudioPlayer Drumset) : IBeatEventHandler
{
    Beatmap beatmap => Player.Beatmap;
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
        var measureStart = beatmap.BeatFromMeasure(currentMeasure);
        return measureStart + Math.Floor(currentBeat - measureStart + Beatmap.BeatEpsilon) + 1;
    }

    void UpdateNextBeat(double currentBeat)
    {
        var currentMeasure = beatmap.MeasureFromBeat(currentBeat);

        nextBeat = GetNextBeat(currentMeasure, currentBeat);
        if (double.IsPositiveInfinity(nextBeat))
        {
            nextBeatTime = double.PositiveInfinity;
            return;
        }

        var nextMeasure = beatmap.BeatFromMeasure(currentMeasure + 1);

        measureBeat = nextBeat >= nextMeasure;
        if (measureBeat) nextBeat = nextMeasure;

        nextBeatTime = beatmap.MillisecondsFromBeat(nextBeat);
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
