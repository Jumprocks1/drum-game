using DrumGame.Game.Channels;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;

namespace DrumGame.Game.Beatmaps.Scoring;

public record ScoreEvent
{
    public HitScoreRating Rating;
    public DrumChannel Channel;
    public double? Time; // Time of event ms, can be ignored for misses
    public double? ObjectTime; // Intended time of event ms, for ignored and rolls this is null
    public double? HitError => Time - ObjectTime;
    public bool IsMiss => Rating == HitScoreRating.Miss;
    public bool Ignored => Rating == HitScoreRating.Ignored;
    // Could do smooth colors outside of perfect
    public Colour4 Colour
    {
        get
        {
            if (Time > ObjectTime)
            {
                return Rating switch
                {
                    HitScoreRating.Perfect => Util.HitColors.LatePerfect,
                    HitScoreRating.Good => Util.HitColors.LateGood,
                    HitScoreRating.Bad => Util.HitColors.LateBad,
                    HitScoreRating.Miss => Util.HitColors.LateMiss,
                    _ => Colour4.Black
                };
            }
            else if (Time < ObjectTime)
            {
                return Rating switch
                {
                    HitScoreRating.Perfect => Util.HitColors.EarlyPerfect,
                    HitScoreRating.Good => Util.HitColors.EarlyGood,
                    HitScoreRating.Bad => Util.HitColors.EarlyBad,
                    HitScoreRating.Miss => Util.HitColors.EarlyMiss,
                    _ => Colour4.Black
                };
            }
            return Rating switch
            {
                HitScoreRating.Perfect => Util.HitColors.Perfect,
                HitScoreRating.Good => Util.HitColors.Good,
                HitScoreRating.Bad => Util.HitColors.Bad,
                HitScoreRating.Miss => Util.HitColors.Miss,
                _ => Colour4.Black
            };
        }
    }

    public override string ToString() => $"{Channel} {Rating} hit at {Time}{(Rating != HitScoreRating.Miss ? $" ({HitError})" : "")}";
}
public enum HitScoreRating
{
    Miss,
    Ignored,
    Perfect,
    Good,
    Bad,
}
