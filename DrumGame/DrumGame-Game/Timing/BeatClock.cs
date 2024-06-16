using System;
using System.Collections.Generic;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Commands;
using DrumGame.Game.Utils;
using osu.Framework.Audio.Track;


namespace DrumGame.Game.Timing;

public class BeatClock : TrackClock
{
    public override double CurrentTime
    {
        get => base.CurrentTime; protected set
        {
            if (value == base.CurrentTime) return;
            base.CurrentTime = value;
            _currentBeat = null;
        }
    }
    public readonly Beatmap Beatmap;
    double? _currentBeat;
    public double CurrentBeat => _currentBeat ?? (_currentBeat = Beatmap.BeatFromMilliseconds(CurrentTime)).Value;
    public Tempo CurrentBPM => Beatmap.RecentChange<TempoChange>(CurrentBeat).Tempo;
    // we round so that if we're at like beat 3.999 it counts as the next measure
    public int CurrentMeasure => Beatmap.MeasureFromBeat(CurrentBeat + Beatmap.BeatEpsilon);
    public BeatClock(Beatmap beatmap, Track track) : base(track, beatmap.ComputedLeadIn())
    {
        Beatmap = beatmap;
        beatmap.TempoUpdated += Invalidate;
        beatmap.OffsetUpdated += Invalidate;
        beatmap.BookmarkUpdated += UpdateBookmarkEvents;
        UpdateBookmarkEvents();
        Util.CommandController.RegisterHandlers(this);
    }
    public void UpdateBookmarkEvents() => BookmarkEvents = BookmarkEvent.CreateList(Beatmap);

    public override void Dispose()
    {
        Util.CommandController.RemoveHandlers(this);
        base.Dispose();
    }
    public void Invalidate()
    {
        LeadIn = Beatmap.ComputedLeadIn();
        _currentBeat = null;
        if (Events != null)
        {
            var tickTime = Beatmap.TickFromBeat(Beatmap.RoundBeat(CurrentBeat));
            for (int i = 0; i < Events.Count; i++) Events[i].SkipTo(tickTime, CurrentTime);
        }
    }
    List<IBeatEventHandler> Events;

    public void RegisterEvents(IBeatEventHandler events)
    {
        Events ??= new();
        Events.Add(events);
        events.SkipTo(Beatmap.TickFromBeat(Beatmap.RoundBeat(CurrentBeat)), CurrentTime);
    }
    public void UnregisterEvents(IBeatEventHandler events)
    {
        if (Events == null) return;
        Events.Remove(events);
    }

    public List<BookmarkEvent> BookmarkEvents;
    public override void Update(double dt)
    {
        var previousBeat = CurrentBeat;
        base.Update(dt);
        if (BookmarkEvents != null && BookmarkEvents.Count > 0)
        {
            foreach (var e in BookmarkEvents)
                if (e.Beat > previousBeat && e.Beat <= CurrentBeat)
                    e.Trigger(this);
        }
        // Note, this works with Start() calls since there's always a SeekCommit before Start
        if (IsRunning && AsyncSeeking == 0 && Events != null) // don't want to trigger events when paused or async seeking
        {
            var tick = Beatmap.TickFromBeat(CurrentBeat);
            for (var i = 0; i < Events.Count; i++) Events[i].TriggerThrough(tick, this, !Manual);
        }
    }

    protected override void SeekCommit(double position)
    {
        // this is triggered after CurrentTime is updated
        if (Events != null)
        {
            // we round so that we can handle seeking "exactly" to certain notes
            var tickTime = Beatmap.TickFromBeat(Beatmap.RoundBeat(CurrentBeat));
            for (var i = 0; i < Events.Count; i++) Events[i].SkipTo(tickTime, CurrentTime);
        }
        base.SeekCommit(position);
    }

    public double NextHitOrBeat(bool forward) => NextHitOrBeat(CurrentBeat, forward);
    public double NextHitOrBeat(double position, bool forward)
    {
        var currentTick = position * Beatmap.TickRate;
        var tickEpsilon = Beatmap.BeatEpsilon * Beatmap.TickRate;
        int? target = null;
        foreach (var note in Beatmap.HitObjects)
        {
            if (forward)
            {
                if (note.Time > currentTick + tickEpsilon)
                {
                    target = note.Time; break;
                }
            }
            else
            {
                if (note.Time > currentTick - tickEpsilon) break;
                target = note.Time;
            }
        }
        if (!target.HasValue)
        {
            if (forward)
            {
                return Math.Floor(Beatmap.RoundBeat(position) + 1);
            }
            else
            {
                return Math.Ceiling(Beatmap.RoundBeat(position) - 1);
            }
        }
        else
        {
            return (double)target.Value / Beatmap.TickRate;
        }
    }

    public void SeekToBeat(double beat) => Seek(Beatmap.MillisecondsFromBeat(beat));
    [CommandHandler] public bool SeekToBeat(CommandContext context) => context.GetNumber(SeekToBeat, "Seek to beat", "Beat", Math.Floor(CurrentBeat + Beatmap.BeatEpsilon));
    [CommandHandler]
    public bool SeekXBeats(CommandContext context)
    {
        context.GetNumber(e => SeekToBeat(CurrentBeat + e), "Seeking By Beats", "Beat count");
        return true;
    }
    [CommandHandler] public void SeekToMeasureStart() => SeekToBeat(Beatmap.BeatFromMeasure(CurrentMeasure));
    [CommandHandler]
    public void SeekToMeasureEnd()
    {
        var measure = CurrentMeasure;
        var nextMeasureTick = Beatmap.TickFromMeasure(measure + 1);
        var seek = Beatmap.LastHitInRange(Beatmap.TickFromMeasure(measure), nextMeasureTick) ?? nextMeasureTick;
        SeekToBeat((double)seek / Beatmap.TickRate);
    }
    [CommandHandler] public void SeekToStart() => SeekToBeat(0);
    [CommandHandler]
    public void SeekToEnd()
    {
        var c = Beatmap.HitObjects.Count;
        if (c > 0) Seek(Beatmap.MillisecondsFromTick(Beatmap.HitObjects[c - 1].Time));
    }

    [CommandHandler]
    public bool SetPlaybackBPM(CommandContext context)
    {
        var currentBPM = CurrentBPM.BPM;
        var current = Math.Round(CurrentBPM.BPM * PlaybackSpeed.Value, 2);
        context.GetNumber(v =>
        {
            var ratio = v / currentBPM;
            PlaybackSpeed.Value = ratio;
        }, "Set Playback BPM", "BPM", current);
        return true;
    }
}

