using DrumGame.Game.Media;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using System;
using DrumGame.Game.Utils;
using ManagedBass.Mix;
using osu.Framework.Audio.Track;

namespace DrumGame.Game.Timing;

public class Metronome : IBeatEventHandler
{
    DrumsetAudioPlayer drumset;
    readonly BeatmapPlayer player;
    Beatmap beatmap => player.Beatmap;
    public Metronome(BeatmapPlayer player, DrumsetAudioPlayer drumset)
    {
        this.player = player;
        this.drumset = drumset;
    }
    double nextBeat = double.NaN;
    double nextBeatTime = double.NaN;
    bool measureBeat; // if nextBeatTime is a measure start beat
    public void SkipTo(int _, double __)
    {
        nextBeatTime = double.NaN;
        Queue.UnbindAndClear(player.Track.Track);
    }

    void UpdateNextBeat(double currentBeat) // if current beat is an integer, nextBeat will NOT be that beat
    {
        var currentMeasure = beatmap.MeasureFromBeat(currentBeat);

        var measureStart = beatmap.BeatFromMeasure(currentMeasure);
        nextBeat = measureStart + Math.Floor(currentBeat - measureStart) + 1;

        var nextMeasure = beatmap.BeatFromMeasure(currentMeasure + 1);

        measureBeat = nextBeat >= nextMeasure;
        if (measureBeat) nextBeat = nextMeasure;

        nextBeatTime = beatmap.MillisecondsFromBeat(nextBeat);
    }

    SyncQueue Queue = new();

    public void TriggerThrough(int _, BeatClock clock, bool __)
    {
        if (double.IsNaN(nextBeatTime))
            UpdateNextBeat(clock.CurrentBeat);
        var time = clock.CurrentTime;
        var playbackSpeed = clock.PlaybackSpeed.Value;
        if (time + IBeatEventHandler.Prefire * playbackSpeed > nextBeatTime)
        {
            drumset.PlayAt(new DrumChannelEvent(0, DrumChannel.Metronome, measureBeat ? (byte)1 : (byte)2), clock, nextBeatTime, Queue);
            UpdateNextBeat(nextBeat);
        }
    }
}
