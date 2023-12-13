using System.Collections.Generic;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Replay;

namespace DrumGame.Game.Beatmaps.Scoring;

public class ReplayScorer : BeatmapScorerBase
{
    readonly Beatmap Beatmap;
    readonly BeatmapReplay Replay;
    public ReplayScorer(Beatmap beatmap, BeatmapReplay replay) : base(beatmap)
    {
        Beatmap = beatmap;
        Replay = replay;
        LoadHitObjects();
    }

    public List<double> ComputeHitErrors()
    {
        var hitErrors = new List<double>();
        void scoreEvent(ScoreEvent ev)
        {
            if (ev.HitError is double error)
                hitErrors.Add(error);
        }
        OnScoreEvent += scoreEvent;
        foreach (var e in Replay.Events)
        {
            if (e is DrumChannelEvent ev) Hit(ev);
        }
        return hitErrors;
    }
}