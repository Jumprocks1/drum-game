using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DrumGame.Game.Beatmaps.Replay;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Modifiers;

namespace DrumGame.Game.Stores.DB;

// This only stores summary statistics related to a replay
// The actual hits should be stored in `Path`
public class ReplayInfo
{
    public int Id { get; set; }
    public string MapId { get; set; }
    public long CompleteTimeTicks { get; private set; }
    public long StartTimeTicks { get; set; }

    public long AccuracyHit { get; set; }
    public long AccuracyTotal { get; set; }
    public long Score { get; set; }
    public int MaxCombo { get; set; }
    public int Combo { get; set; } // current combo
    public double StartPosition { get; set; } // position (in ms) when we start tracking score
    // lowest playback speed reach during the duration of the score
    // can only be increased before hitting any notes
    public double PlaybackSpeed { get; set; }
    public int StartNote { get; set; } // hit object we started recording score on. -1 for beginning

    public string Mods { get; set; }
    public string Extra { get; set; } // for future use

    public int Perfect { get; set; }
    public int Good { get; set; }
    public int Bad { get; set; }
    public int Miss { get; set; }

    public int NoteCount => Perfect + Good + Bad + Miss;
    public string NotePercent(int count)
    {
        var total = NoteCount;
        if (count == total) return "100.0%";
        else return $"{(double)(count * 100) / total:0.00}%";
    }

    public ReplayInfo Clone() => (ReplayInfo)MemberwiseClone();

    public void IncrementCombo()
    {
        Combo += 1;
        if (Combo > MaxCombo) MaxCombo = Combo;
    }

    // doesn't support rolls
    // should probably just compute this one on save, since it isn't used during gameplay
    public void CountHit(HitScoreRating rating)
    {
        if (rating == HitScoreRating.Perfect) Perfect += 1;
        else if (rating == HitScoreRating.Good) Good += 1;
        else if (rating == HitScoreRating.Bad) Bad += 1;
        else if (rating == HitScoreRating.Miss) Miss += 1;
    }

    public void ResetTo(double position)
    {
        StartNote = int.MaxValue; // needs to get set outside of this
        StartPosition = position;
        StartTimeTicks = DateTimeOffset.Now.UtcTicks;
        AccuracyHit = 0;
        AccuracyTotal = 0;
        Score = 0;
        MaxCombo = 0;
        Combo = 0;
        Perfect = 0;
        Good = 0;
        Bad = 0;
        Miss = 0;
    }

    public double AccuracyPercent => AccuracyTotal == AccuracyHit ? 100 : (double)(AccuracyHit * 100) / AccuracyTotal;
    public string Accuracy => $"{AccuracyPercent:00.00}%";
    public string AccuracyNoLeading => $"{AccuracyPercent:0.00}%";
    public const string DateTimeString = "yyyyMMddHHmmss";
    public void SetCompleteTime(DateTimeOffset completeTime)
        => CompleteTimeTicks = completeTime.UtcTicks;
    public void SetCompleteTime() => CompleteTimeTicks = DateTimeOffset.Now.UtcTicks;
    public void SetCompleteTime(string completeTime)
        => CompleteTimeTicks = DateTimeOffset.ParseExact(completeTime, DateTimeString, CultureInfo.InvariantCulture).UtcTicks;
    public DateTimeOffset CompleteTime => new DateTimeOffset(CompleteTimeTicks, TimeSpan.Zero);
    public DateTime CompleteTimeLocal => CompleteTime.LocalDateTime;
    public DateTimeOffset StartTime => new DateTimeOffset(StartTimeTicks, TimeSpan.Zero);
    public DateTime StartTimeLocal => StartTime.LocalDateTime;
    public string Path => System.IO.Path.Join("replays", MapId, CompleteTime.ToString(DateTimeString));
    public bool Exists => Utils.Util.Resources.Exists(Path);
    public BeatmapReplay LoadReplay() => BeatmapReplay.From(Utils.Util.Resources, this);

    public void SetMods(List<BeatmapModifier> modifiers) => Mods = BeatmapModifier.Serialize(modifiers);
    public List<BeatmapModifier> ParseMods() => BeatmapModifier.ParseModifiers(Mods);
}
