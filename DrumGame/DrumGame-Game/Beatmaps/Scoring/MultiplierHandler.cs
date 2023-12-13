using System;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;

namespace DrumGame.Game.Beatmaps.Scoring;

public class MultiplierHandler
{
    public const int MaxMeter = 75;
    public const double MissCost = 0.5;
    public int Multiplier { get; private set; } = 1;
    public double MeterLevel { get; private set; } = 0;
    public readonly (int Multiplier, int Combo, Colour4 Colour)[] ComboLevels = new[]
    {
        (1, 0, Colour4.Transparent),
        (2, 5, Util.HitColors.Miss),
        (4, 15, Util.HitColors.Bad),
        (8, 35, Util.HitColors.Good),
        (8, MaxMeter, Util.HitColors.Perfect),
    };

    public void Hit(int change)
    {
        MeterLevel = Math.Clamp(MeterLevel + change, 0, MaxMeter);
        UpdateMultiplier();
    }
    public void Hit(ScoreEvent e)
    {
        if (e.Rating == HitScoreRating.Perfect)
        {
            Hit(1);
        }
        else if (e.Rating == HitScoreRating.Bad)
        {
            Hit(-1);
        }
    }
    public void Reset()
    {
        MeterLevel = 0;
        UpdateMultiplier();
    }
    public void Miss()
    {
        var level = 0;
        for (int i = 1; i < ComboLevels.Length; i++)
        {
            if (MeterLevel < ComboLevels[i].Combo) break;
            level = i;
        }
        if (level == ComboLevels.Length - 1)
        {
            level = ComboLevels.Length - 2;
        }
        var a = ComboLevels[level].Combo;
        var b = ComboLevels[level + 1].Combo;
        var levelF = Math.Max(0, level + (MeterLevel - a) / (b - a) - MissCost);
        level = (int)levelF;
        a = ComboLevels[level].Combo;
        b = ComboLevels[level + 1].Combo;
        MeterLevel = a + (b - a) * (levelF - level);
        UpdateMultiplier();
    }
    public void UpdateMultiplier()
    {
        var j = 0;
        for (int i = 1; i < ComboLevels.Length; i++)
        {
            if (MeterLevel < ComboLevels[i].Combo) break;
            j = i;
        }
        Multiplier = ComboLevels[j].Multiplier;
    }
}
