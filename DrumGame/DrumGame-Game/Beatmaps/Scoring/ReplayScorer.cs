using System.Collections.Generic;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Replay;

namespace DrumGame.Game.Beatmaps.Scoring;

public class ReplayResults
{
    public List<double> HitErrors;
    public List<(double, int)> Judgements;
}

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

    public ReplayResults ComputeResults()
    {
        var hitErrors = new List<double>();
        var judgements = new List<(double, int)>();
        void scoreEvent(ScoreEvent ev)
        {
            if (ev.HitError is double error)
                hitErrors.Add(error);
            if (ev.ObjectTime is double t)
                judgements.Add((t, RatingValue(ev.Rating)));
        }
        OnScoreEvent += scoreEvent;
        foreach (var e in Replay.Events)
        {
            if (e is DrumChannelEvent ev) Hit(ev);
        }
        return new ReplayResults
        {
            HitErrors = hitErrors,
            Judgements = judgements
        };
    }
}