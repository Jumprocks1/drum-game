using DrumGame.Game.Utils;
using osu.Framework.Graphics;

namespace DrumGame.Game.Beatmaps.Scoring;

// Nice graph https://www.reddit.com/r/osugame/comments/781ot4/od_in_milliseconds/

// all in ms
public record HitWindows(
    // this is the window for notes to even be considered
    // this means notes outside of this window effectively do not exist
    float HitWindow = 200,
    float BadWindow = 135,
    float GoodWindow = 76,
    float PerfectWindow = 35)
{
    public HitScoreRating GetRating(double hitError) =>
            hitError <= PerfectWindow ? HitScoreRating.Perfect :
            hitError <= GoodWindow ? HitScoreRating.Good :
            hitError <= BadWindow ? HitScoreRating.Bad :
            HitScoreRating.Miss;
    public Colour4? GetColor(HitScoreRating rating) => rating switch
    {
        HitScoreRating.Perfect => Util.Skin.HitColors.Perfect,
        HitScoreRating.Good => Util.Skin.HitColors.Good,
        HitScoreRating.Bad => Util.Skin.HitColors.Bad,
        HitScoreRating.Miss => Util.Skin.HitColors.Miss,
        _ => null
    };
}
