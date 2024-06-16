using System;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;

namespace DrumGame.Game.Beatmaps.Display.Components;

public class ComboMeter : CompositeDrawable
{
    double MeterLevel = 0;
    double meterDisplay = 0;
    (int Multiplier, int Combo, Colour4 Colour)[] ComboLevels => Scorer.MultiplierHandler.ComboLevels;
    readonly BeatmapScorer Scorer;
    public void UpdateValues(double level, int multiplier)
    {
        MeterLevel = level;
        InnerText.Text = $"x{multiplier}";
    }
    public void UpdateDisplay()
    {
        var level = 0;
        for (int i = 1; i < ComboLevels.Length; i++)
        {
            if (meterDisplay < ComboLevels[i].Combo) break;
            level = i;
        }
        if (level == ComboLevels.Length - 1)
        {
            CircularProgress.Colour = Colour4.Transparent;
            CircularProgress.Progress = 0;
        }
        else
        {
            CircularProgress.Colour = ComboLevels[level + 1].Colour;
            var range = ComboLevels[level + 1].Combo - ComboLevels[level].Combo;
            CircularProgress.Progress = (meterDisplay - ComboLevels[level].Combo) / range;
        }
        ProgressBacking.Colour = ComboLevels[level].Colour;
    }
    protected override void Update()
    {
        var newDisplay = Math.Clamp(Util.ExpLerp(meterDisplay, MeterLevel, 0.995, Clock.TimeInfo.Elapsed, 0.001),
            0, MultiplierHandler.MaxMeter);
        if (meterDisplay != newDisplay)
        {
            meterDisplay = newDisplay;
            UpdateDisplay();
        }
        base.Update();
    }

    SpriteText InnerText;
    CircularProgress CircularProgress;
    CircularProgress ProgressBacking;
    public ComboMeter(BeatmapScorer scorer, float size)
    {
        Scorer = scorer;
        Width = size;
        Height = size;
        var border = new CircularProgress
        {
            RelativeSizeAxes = Axes.Both,
            Colour = Util.Skin.Notation.NotationColor,
            InnerRadius = 0.1f,
            Progress = 1
        };

        AddInternal(ProgressBacking = new CircularProgress
        {
            RelativeSizeAxes = Axes.Both,
            Width = 0.95f,
            Height = 0.95f,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            InnerRadius = 0.35f
        });

        AddInternal(CircularProgress = new CircularProgress
        {
            RelativeSizeAxes = Axes.Both,
            Width = 0.95f,
            Height = 0.95f,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            InnerRadius = 0.35f
        });
        ProgressBacking.Progress = 1;
        AddInternal(border);
        AddInternal(InnerText = new SpriteText
        {
            Font = FrameworkFont.Regular.With(size: size * 0.5f),
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Colour = Util.Skin.Notation.NotationColor
        });
        UpdateDisplay();
    }
}
