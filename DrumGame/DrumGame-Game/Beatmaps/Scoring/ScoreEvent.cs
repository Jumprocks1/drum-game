using DrumGame.Game.Channels;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;

namespace DrumGame.Game.Beatmaps.Scoring;

public record ScoreEvent
{
    public HitScoreRating Rating;
    public DrumChannel Channel; // this should match InputEvent.HitObject.Channel in most cases. It may not match InputEvent.Channel when equivalents are involved
    public int OriginalObjectIndex = -1;
    public double? Time; // Time of event ms, can be ignored (null?) for misses
    public double? ObjectTime; // Intended time of event ms, for ignored and rolls this is null
    public double? HitError => Time - ObjectTime;
    public bool IsMiss => Rating == HitScoreRating.Miss;
    public bool Ignored => Rating == HitScoreRating.Ignored;
    public DrumChannelEvent InputEvent; // this will be null for some miss events since they aren't triggered by hitting a drum
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
public enum HitScoreRating // integer values are used for shaders
{
    Ignored = 0,
    Perfect = 1,
    Good = 2,
    Bad = 3,
    Miss = 4,
}
