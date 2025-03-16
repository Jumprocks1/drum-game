using System;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;

namespace DrumGame.Game.Beatmaps.Scoring;

// Nice graph https://www.reddit.com/r/osugame/comments/781ot4/od_in_milliseconds/

// all in ms
public class HitWindows
{
    private HitWindows(HitWindowPreference sourcePreference, float perfectWindow, float goodWindow, float badWindow, float hitWindow)
    {
        SourcePreference = sourcePreference;
        PerfectWindow = perfectWindow;
        GoodWindow = goodWindow;
        BadWindow = badWindow;
        HitWindow = hitWindow;
    }

    public readonly float PerfectWindow;
    public readonly float GoodWindow;
    public readonly float BadWindow;
    // this is the window for notes to even be considered
    // this means notes outside of this window effectively do not exist
    public readonly float HitWindow;

    public const float DefaultPerfectWindow = 35;
    public const float DefaultGoodWindow = 76;
    public const float DefaultBadWindow = 135;
    public const float DefaultHitWindow = 200;
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

    public static HitWindows Standard => new(HitWindowPreference.Standard, DefaultPerfectWindow, DefaultGoodWindow, DefaultBadWindow, DefaultHitWindow);

    // store a reference from this, don't call it repeatedly
    public static HitWindows GetWindowsForCurrentPreference() => GetWindows(Util.ConfigManager.Get<HitWindowPreference>(DrumGameSetting.HitWindowPreference));
    public static HitWindows GetWindows(HitWindowPreference preference) => preference switch
    {
        // See https://osu.ppy.sh/wiki/en/Beatmap/Overall_difficulty
        // based off osu!standard OD 5
        HitWindowPreference.Lax => new HitWindows(preference, 50, 100, 150, 200),
        // similar to OD 7/8
        HitWindowPreference.Standard => Standard,
        // based off osu!standard OD 10
        HitWindowPreference.Strict => new HitWindows(preference, 20, 60, 100, 180),
        HitWindowPreference.Custom => GetCustomWindows(),
        _ => Standard
    };

    public static HitWindows MakeCustom(float perfect, float good, float bad, float total)
        => new(HitWindowPreference.Custom, perfect, good, bad, total);

    public static readonly string DefaultCustomHitWindowString = $"{DefaultPerfectWindow},{DefaultGoodWindow},{DefaultBadWindow},{DefaultHitWindow}";
    static HitWindows GetCustomWindows()
    {
        var windowString = Util.ConfigManager.Get<string>(DrumGameSetting.CustomHitWindows);
        if (string.IsNullOrWhiteSpace(windowString))
            return MakeCustom(DefaultPerfectWindow, DefaultGoodWindow, DefaultBadWindow, DefaultHitWindow);
        var spl = windowString.Split(',');
        float parse(int i, float fallback) => float.TryParse(spl.Length > i ? spl[i] : null, out var o) ? o : fallback;
        // we don't validate here, we instead validate when the user changes it in-game
        return MakeCustom(parse(0, DefaultPerfectWindow), parse(1, DefaultGoodWindow),
            parse(2, DefaultBadWindow), parse(3, DefaultHitWindow));
    }

    public string Validate()
    {
        if (PerfectWindow < 0 || GoodWindow < 0 || BadWindow < 0 || HitWindow < 0)
            return "Hit windows must be greater than 0";
        if (HitWindow < BadWindow) return $"Hit window ({HitWindow}) must be greater than bad window ({BadWindow})";
        if (BadWindow < GoodWindow) return $"Bad window ({BadWindow}) must be greater than good window ({GoodWindow})";
        if (GoodWindow < PerfectWindow) return $"Good window ({GoodWindow}) must be greater than perfect window ({PerfectWindow})";
        return null;
    }

    public readonly HitWindowPreference SourcePreference;
    public string MarkupTooltip
    {
        get
        {
            var r = $"{SourcePreference} hit windows";
            r += $"\n<perfect>Perfect</c>: ±{PerfectWindow}ms";
            r += $"\n<good>Good</c>: ±{GoodWindow}ms";
            r += $"\n<bad>Bad</c>: ±{BadWindow}ms";
            r += $"\n<miss>Miss</c>: ±{HitWindow}ms";
            return r;
        }
    }

    public string MsWindowString => $"{PerfectWindow},{GoodWindow},{BadWindow},{HitWindow}";

    public override string ToString()
    {
        if (SourcePreference == HitWindowPreference.Custom)
            return $"Custom{{{MsWindowString}}}";
        return SourcePreference.ToString();
    }
}

public enum HitWindowPreference
{
    Standard = 0,
    Lax,
    Strict,
    Custom
}