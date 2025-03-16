using System;
using System.Collections.Generic;
using DrumGame.Game.Beatmaps.Practice;
using DrumGame.Game.Channels;
using DrumGame.Game.Stores;
using DrumGame.Game.Stores.DB;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Beatmaps.Scoring;

// Really happy with the algorithm this uses
// It could use some refactoring (mostly due to crazy while loops with breaks), but overall it works really well

// Note, this does not support modifications to the beatmap at all
// It must be recreated if beatmap changes
// Usually this is fine since it's recreated when switching modes

// this is meant to contain all the methods necessary for scoring a beatmap based on events alone
// the full BeatmapScorer contains methods that work when playing a beatmap
// the biggest difference is this base class does not depend on a TrackClock and it has no update method

public class BeatmapScorerBase : IDisposable
{
    public record RollEvent(DrumChannel Channel, double EndTime)
    {
        public int HitCount { get; set; } = 1;
    }

    // this occurs if we seek in the middle of the song
    // if we do that, we don't want to immediately start marking everything as a miss
    // instead we should wait for the user to hit a note, then we can start marking notes
    // it gets set inside `BeatmapScorer.cs`
    public bool SeekPracticeMode = false;
    public PracticeMode PracticeMode;

    public event Action<ScoreEvent> OnScoreEvent;
    public event Action OnChange;
    // TODO in some cases we should be pulling the HitWindows from replay info
    public readonly HitWindows HitWindows = HitWindows.GetWindowsForCurrentPreference();
    protected List<HitObjectRealTime> HitObjects;
    readonly Beatmap Beatmap;
    // make sure to only update this after triggering all events
    protected int HitThrough = -1; // we don't consider any hit objects with index <= this
    List<RollEvent> ActiveRolls = new();
    public BeatmapScorerBase(Beatmap beatmap)
    {
        Beatmap = beatmap;
        Beatmap.OffsetUpdated += LoadHitObjects;
        Beatmap.TempoUpdated += LoadHitObjects;
    }

    public virtual double LoadTime => -Beatmap.ComputedLeadIn();

    public void LoadHitObjects()
    {
        // costs ~3ms first time, 0.3ms after that
        HitObjects = Beatmap.GetRealTimeHitObjects();
        ResetTo(LoadTime);
    }

    const int BaseScoreMultiplier = 100; // multiply by 100 just for excitment
    public ReplayInfo ReplayInfo = new();
    public string Accuracy => ReplayInfo.Accuracy;
    public MultiplierHandler MultiplierHandler = new();
    // Notes in here can be hit a second time. The second time will be inserted as an ignored hit
    public List<HitObjectRealTime> RehitNotes = new();
    public void TriggerIgnore(DrumChannelEvent ev, HitObjectRealTime hitObject) => TriggerScoreEvent(new ScoreEvent
    {
        Channel = ev.Channel,
        Rating = HitScoreRating.Ignored,
        Time = ev.Time,
        InputEvent = ev,
        OriginalObjectIndex = hitObject?.OriginalObjectIndex ?? -1
    });
    public static int RatingValue(HitScoreRating rating) => rating switch
    {
        HitScoreRating.Miss => 0,
        HitScoreRating.Bad => 1,
        HitScoreRating.Good => 2,
        _ => 3
    };
    public virtual void TriggerScoreEvent(ScoreEvent scoreEvent, bool roll = false)
    {
        var inputEvent = scoreEvent.InputEvent;
        if (inputEvent != null && inputEvent.NeedsSamplePlayback) // this should include everything except force triggered misses
        {
            // we need a sample to play, so we need to try to find a hit object
            // this could definitely be slow for long maps
            if (scoreEvent.OriginalObjectIndex < 0)
            {
                var t = inputEvent.Time;
                int? hitIndex = null;
                var hitError = 0d;
                for (var target = 0; target < HitObjects.Count; target++)
                {
                    var ho = HitObjects[target];
                    if (ChannelMatch(ho, inputEvent))
                    {
                        var error = Math.Abs(t - ho.Time);
                        if (hitIndex.HasValue)
                        {
                            // we prefer hits that have less error
                            if (error < hitError)
                            {
                                hitError = error;
                                hitIndex = target;
                            }
                            else break; // if the error starts increasing, we can stop looking for candidates
                        }
                        else
                        {
                            hitError = error;
                            hitIndex = target;
                        }
                    }
                }
                if (hitIndex.HasValue) scoreEvent.OriginalObjectIndex = HitObjects[hitIndex.Value].OriginalObjectIndex;
            }
            if (scoreEvent.OriginalObjectIndex >= 0)
                scoreEvent.InputEvent.HitObject ??= Beatmap.HitObjects[scoreEvent.OriginalObjectIndex]; // used for sample playback if enabled
        }
        SeekPracticeMode = false; // make sure seek practice is off after we hit a note
        if (PracticeMode != null)
        {
            var t = scoreEvent.Time;
            var obj = scoreEvent.ObjectTime;
            if (t.HasValue) // player hit
            {
                if (!obj.HasValue || obj < PracticeMode.StartTime || obj >= PracticeMode.EndTime)
                    scoreEvent.Rating = HitScoreRating.Ignored;
            }
            else
            {
                // should just be auto misses
                if (obj < PracticeMode.StartTime || obj >= PracticeMode.EndTime)
                    return;
            }
        }
        if (!roll) ReplayInfo.CountHit(scoreEvent.Rating);
        switch (scoreEvent.Rating)
        {
            case HitScoreRating.Miss:
                ReplayInfo.Combo = 0;
                ReplayInfo.AccuracyTotal += 3;
                MultiplierHandler.Miss();
                break;
            case HitScoreRating.Bad:
            case HitScoreRating.Good:
            case HitScoreRating.Perfect:
                if (!roll)
                {
                    var value = RatingValue(scoreEvent.Rating);
                    ReplayInfo.Score += value * BaseScoreMultiplier * MultiplierHandler.Multiplier;
                    MultiplierHandler.Hit(scoreEvent);
                    ReplayInfo.AccuracyHit += value;
                    ReplayInfo.AccuracyTotal += 3;
                }
                ReplayInfo.IncrementCombo();
                break;
            default: break;
        }
        OnScoreEvent?.Invoke(scoreEvent);
        OnChange?.Invoke();
    }

    public void Dispose()
    {
        OnScoreEvent = null;
        Beatmap.OffsetUpdated -= LoadHitObjects;
        Beatmap.TempoUpdated -= LoadHitObjects;
    }

    // returns true if we seeked past any notes
    public virtual bool AfterSeek(double newPosition)
    {
        var oldHitThrough = HitThrough;
        HitThrough = HitObjects.BinarySearchFirst(newPosition - 0.001) - 1;
        if (oldHitThrough != HitThrough)
        {
            ResetTo(newPosition);
            return true;
        }
        return false; // no reset required since we just seeked forward
    }

    public virtual void ResetTo(double position)
    {
        ActiveRolls.Clear();
        RehitNotes.Clear();
        MultiplierHandler.Reset();
        ReplayInfo.ResetTo(position);
        // we subtract a little to make sure if we seek exactly on a note that we don't mark it as hit already
        ReplayInfo.StartNote = HitThrough = HitObjects.BinarySearchFirst(position - 0.001) - 1;
        // if we seek past the end, OnCompleted won't be trigger
        Completed = HitThrough == HitObjects.Count - 1;
        OnChange?.Invoke();
    }

    public void TriggerMissEvent(HitObjectRealTime hitObject)
    {
        // we skip misses if we are in SeekPracticeMode
        if (SeekPracticeMode) return;
        TriggerScoreEvent(new ScoreEvent
        {
            Channel = hitObject.Data.Channel,
            Rating = HitScoreRating.Miss,
            ObjectTime = hitObject.Time,
            OriginalObjectIndex = hitObject.OriginalObjectIndex
        });
    }

    public void ActivateHit(DrumChannelEvent ev, int hitIndex, double t)
    {
        var hit = HitObjects[hitIndex];
        // move the hit note to the front of it's note group (excluding already hit notes)
        // this makes it so we don't have to mark notes in this same group as force missed
        var swapTarget = hitIndex;
        // smallest value for hitThrough is -1, so this also guarentees we are > 0 =>
        //    we won't have array out of bounds for `swapTarget - 1`
        while (swapTarget > (HitThrough + 1) && HitObjects[swapTarget - 1].Time == hit.Time) swapTarget -= 1;
        if (swapTarget != hitIndex)
        {
            HitObjects[hitIndex] = HitObjects[swapTarget];
            HitObjects[swapTarget] = hit;
            hitIndex = swapTarget;
        }
        // we have hit through candidateIndex now, so we need to mark everything between as a miss
        for (var i = HitThrough + 1; i < hitIndex; i++)
        {
            TriggerMissEvent(HitObjects[i]);
            RehitNotes.Add(HitObjects[i]);
        }
        var rating = HitWindows.GetRating(Math.Abs(t - hit.Time));
        TriggerScoreEvent(new ScoreEvent
        {
            Channel = hit.Data.Channel,
            Rating = rating,
            Time = t,
            ObjectTime = hit.Time,
            OriginalObjectIndex = hit.OriginalObjectIndex,
            InputEvent = ev
        });
        // if we were in the early bad window, add to recently skipped
        if (t - hit.Time < -HitWindows.GoodWindow) RehitNotes.Add(hit);
        if (hit.IsRoll && rating != HitScoreRating.Miss) ActiveRolls.Add(new RollEvent(hit.Data.Channel, hit.Time + hit.Duration));
        HitThrough = hitIndex;
    }

    public bool CheckRolls(DrumChannelEvent ev)
    {
        // note that rolls are only checked when the next event in this channel would be considered "early" (non-perfect)
        for (int i = ActiveRolls.Count - 1; i >= 0; i--)
        {
            var roll = ActiveRolls[i];
            if (roll.EndTime > ev.Time)
            {
                if (ChannelMatch(ev.Channel, roll.Channel))
                {
                    roll.HitCount += 1;
                    TriggerScoreEvent(new ScoreEvent
                    {
                        Channel = ev.Channel,
                        Rating = HitScoreRating.Perfect,
                        Time = ev.Time,
                        InputEvent = ev
                    }, true);
                    return true;
                }
            }
            else
            {
                ActiveRolls.RemoveAt(i);
            }
        }
        return false;
    }

    public bool ChannelMatch(HitObjectRealTime ho, DrumChannelEvent ev) => ChannelMatch(ev.Channel, ho.Data.Channel);
    public bool ChannelMatch(DrumChannel inputChannel, DrumChannel mapChannel) => inputChannel == mapChannel ||
        Util.ConfigManager.ChannelEquivalents.Value.AllowTrigger(inputChannel, mapChannel);

    public void Hit(DrumChannelEvent ev)
    {
        ev.CurrentBeatmap = Beatmap;
        int? hitIndex = null;
        double hitError = 0;

        var target = HitThrough + 1;
        var t = ev.Time;
        while (target < HitObjects.Count)
        {
            var ho = HitObjects[target];
            // occurs when there are no more hit objects to check in the window
            if (ho.Time - t > HitWindows.HitWindow)
            {
                break;
            }
            else
            {
                // This checks if there are unhit notes that are outside of the hit window
                // We say these notes "fell out of the hit window"
                // Ideally these would also be pruned in some sort of update method
                //   so that the user sees a `X` right when it falls out of the window
                if (t - ho.Time <= HitWindows.HitWindow)
                {
                    // we get here when there are hittable notes in the current hit window
                    // the algorithm is planned as follows
                    // 1. find first note in hit window that matches our channel
                    // 1a. If we see a note in the hit window with less error that also matches our channel, prefer that note.
                    // 1b. If we see multiple notes with the same error and same ChannelMatch, prefer the exact channel match

                    // 2. play said note
                    // 3. move note to front of hit group (so notes that are at the same time are "after" this one)
                    // 4. mark all notes before it that are in our hit window as misses

                    if (ChannelMatch(ho, ev))
                    {
                        var error = Math.Abs(t - ho.Time);
                        if (hitIndex.HasValue)
                        {
                            // we prefer hits that have less error
                            if (error < hitError)
                            {
                                // we found a closer note, but our current hitError is within the GoodWindow, so we hit the first note
                                //    since the first note is closer to expiring
                                // this has the effect allowing us to be late on fast notes on the same channel by up to GoodWindow
                                if (hitError <= HitWindows.GoodWindow) break;
                                hitError = error;
                                hitIndex = target;
                            }
                            else if (error == hitError)
                            {
                                if (ho.Channel == ev.Channel) // if the error is equal, we only overwrite if the channel is an exact match
                                {
                                    hitError = error;
                                    hitIndex = target;
                                }
                            }
                            else break; // if the error starts increasing, we can stop looking for candidates
                        }
                        else
                        {
                            hitError = error;
                            hitIndex = target;
                        }
                    }
                }
                else
                {
                    // these are no longer hittable (since they are too far back)
                    // usually these are found in the Update method first
                    TriggerMissEvent(ho);
                    HitThrough += 1;
                }
            }
            target += 1;
        }
        // note that we only check for rolls and rehits if we are early by more than the perfect window
        if (hitIndex.HasValue)
        {
            if (t - HitObjects[hitIndex.Value].Time < -HitWindows.PerfectWindow)
            {
                if (CheckRolls(ev)) return;
                // if we are early by more than the perfect hit window, we will check recently skipped
                // if we find something in recently skiped that is closer than the current target,
                //    we will output an ignore event instead
                HitObjectRealTime remove = null;
                for (int i = RehitNotes.Count - 1; i >= 0; i--)
                {
                    var ho = RehitNotes[i];
                    var diff = t - ho.Time;
                    if (diff > HitWindows.HitWindow) RehitNotes.RemoveAt(i);
                    else
                    {
                        if (ChannelMatch(ho, ev))
                        {
                            var error = Math.Abs(diff);
                            if (error < hitError || (error == hitError && ho.Channel == ev.Channel))
                            {
                                hitError = error;
                                remove = ho;
                            }
                        }
                    }
                }
                if (remove != null)
                {
                    RehitNotes.Remove(remove);
                    TriggerIgnore(ev, remove);
                    return;
                }
            }
            ActivateHit(ev, hitIndex.Value, t);
        }
        else
        {
            if (CheckRolls(ev)) return;
            TriggerIgnore(ev, null);
        }
        CheckComplete();
    }

    public bool Completed;
    public event Action OnCompleted;
    public void CheckComplete()
    {
        if (HitThrough == HitObjects.Count - 1 && !Completed)
        {
            Completed = true;
            OnCompleted?.Invoke();
        }
    }
}
