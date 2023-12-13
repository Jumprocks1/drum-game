using System;
using System.Collections.Generic;
using DrumGame.Game.Channels;
using DrumGame.Game.Stores;
using DrumGame.Game.Stores.DB;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Beatmaps.Scoring;
public class BeatmapScorer : BeatmapScorerBase
{
    double MaxOffset => Math.Max(0, Util.ConfigManager.MidiInputOffset.Value);
    public readonly TrackClock Track;
    public BeatmapScorer(Beatmap beatmap, TrackClock track) : base(beatmap)
    {
        Track = track;
        LoadHitObjects();
    }

    public override void TriggerScoreEvent(ScoreEvent scoreEvent, bool roll = false)
    {
        if (HitThrough == -1) // if this is the first hit, we can safely hard-set the replay playback speed
            ReplayInfo.PlaybackSpeed = Track.PlaybackSpeed.Value;
        else
            ReplayInfo.PlaybackSpeed = Math.Min(ReplayInfo.PlaybackSpeed, Track.PlaybackSpeed.Value);
        base.TriggerScoreEvent(scoreEvent, roll);
    }

    public override void ResetTo(double position)
    {
        ReplayInfo.PlaybackSpeed = Track.PlaybackSpeed.Value;
        base.ResetTo(position);
        SeekPracticeMode = ReplayInfo.StartNote != -1;
    }

    public override double LoadTime => Track.CurrentTime;

    // Calling this is not super critical since the hit method will also expire old notes
    public void Update()
    {
        var target = HitThrough + 1;
        // we have to subtract max offset here since we don't want to expire a note that can actually still be hit
        // ie. if our midi controller is super slow and has a 2s delay, we only want to expire notes 2s after they should technically expire
        var t = Track.AbsoluteTime - MaxOffset * Track.EffectiveRate;
        HitObjectRealTime ho;
        while (target < HitObjects.Count &&
            (t - (ho = HitObjects[target]).Time > HitWindows.HitWindow))
        {
            TriggerMissEvent(ho);
            HitThrough += 1;
            target += 1;
        }
        CheckComplete();
    }
}