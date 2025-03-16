using System;
using System.Collections.Generic;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Data;


namespace DrumGame.Game.Timing;

public interface IBeatEventHandler
{
    // how many ms early we will queue events (using Bass sync)
    const double Prefire = 300; // has to be quite large due to mixer buffer
    void TriggerThrough(int tick, BeatClock clock, bool prefire);
    void SkipTo(int tick, double time);
}
public interface IHasHitObjects
{
    List<HitObject> HitObjects { get; }
}
// have to use ref since things like undo/redo replace the whole list
public class BeatEventHitObjects : BeatEventList<HitObject>
{
    protected override List<HitObject> Events => Beatmap.HitObjects;
    public BeatEventHitObjects(Beatmap beatmap, Action<HitObject, double> callback) : base(beatmap, null, callback)
    {
    }
}
public class BeatEventList<T> : IBeatEventHandler where T : ITickTime
{
    int playedThrough = -1;
    protected Beatmap Beatmap;
    protected virtual List<T> Events { get; }
    readonly Action<T, double> Callback;
    public Action OnReset; // should probably call this on dispose
    public void TriggerThrough(int _, BeatClock clock, bool __)
    {
        var time = clock.CurrentTime;
        var playbackSpeed = clock.PlaybackSpeed.Value;
        var nextEventTime = Events.Count > playedThrough + 1 ? Beatmap.MillisecondsFromTick(Events[playedThrough + 1].Time) : -1;
        while (time + IBeatEventHandler.Prefire * playbackSpeed > nextEventTime && Events.Count > playedThrough + 1)
        {
            playedThrough += 1;
            Callback(Events[playedThrough], playbackSpeed);
            nextEventTime = Events.Count > playedThrough + 1 ? Beatmap.MillisecondsFromTick(Events[playedThrough + 1].Time) : -1;
        }
    }
    public void SkipTo(int tick, double _)
    {
        OnReset?.Invoke();
        // could do binary search here
        for (int i = 0; i < Events.Count; i++)
        {
            var e = Events[i];
            // if we skip to beat 4, we don't want to skip events exactly on beat 4 - they will get triggered on the next update
            if (e.Time >= tick)
            {
                playedThrough = i - 1;
                return;
            }
        }
        playedThrough = Events.Count;
    }

    public BeatEventList(Beatmap beatmap, List<T> events, Action<T, double> callback)
    {
        Beatmap = beatmap;
        Events = events;
        Callback = callback;
    }
}

