using System;
using System.Collections.Generic;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Components;
using DrumGame.Game.Skinning;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace DrumGame.Game.Graphics3D.View;

public class ErrorGraph : CompositeDrawable
{
    public const int SampleCount = 65; // odd number lets us center the middle sample
    public Circle[] Circles = new Circle[SampleCount];
    float[] errors = new float[SampleCount];
    HitWindows HitWindows = HitWindows.GetWindows(HitWindowPreference.Standard);
    public void UpdateData(List<(double beat, double time)> data, double currentTime, double slope, double intercept)
    {
        var maxError = 50f;
        var currentBeat = (currentTime - intercept) / slope;
        var beat = (int)Math.Floor(currentBeat + 0.5); // I think this rounding good
        var beatStart = beat - SampleCount / 2;
        var beatEnd = beatStart + SampleCount;
        Array.Fill(errors, float.NaN);
        foreach (var (b, t) in data)
        {
            var error = t - (b * slope + intercept);
            if (Math.Abs(error) > maxError) maxError = (float)Math.Abs(error);
            if (b >= beatStart && b < beatEnd)
                errors[(int)b - beatStart] = (float)error;
        }
        var offset = (float)(-(currentBeat - beat - 0.5f) / SampleCount);
        for (var i = 0; i < SampleCount; i++)
        {
            var c = Circles[i];
            if (float.IsNaN(errors[i]))
                c.Alpha = 0;
            else
            {
                c.Alpha = 1;
                c.X = (float)i / SampleCount + offset;
                c.Y = errors[i] / maxError / 2;
                // we double the sensitivity here just to make it easier to see the colors
                c.Colour = DrumColors.BlendedHitColor(errors[i] * 2, HitColors, HitWindows);
            }
        }
    }
    Skin.Skin_HitColors HitColors;
    [BackgroundDependencyLoader]
    private void load()
    {
        HitColors = Util.Skin.HitColors.Clone();
        if (HitColors.EarlyPerfect == HitColors.LatePerfect)
        {
            HitColors.EarlyPerfect = HitColors.LatePerfect.Darken(Skin.Skin_HitColors.DefaultShadeAmount);
            HitColors.LatePerfect = HitColors.LatePerfect.Lighten(Skin.Skin_HitColors.DefaultShadeAmount);
        }
        for (var i = 0; i < SampleCount; i++)
        {
            // we could use Canvas if it had circle drawing support
            AddInternal(Circles[i] = new Circle
            {
                Width = 8,
                Height = 8,
                Origin = Anchor.Centre,
                Anchor = Anchor.CentreLeft,
                RelativePositionAxes = Axes.Both
            });
        }
        AddInternal(new Box
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Width = 3,
            Colour = Colour4.AliceBlue.MultiplyAlpha(0.5f),
            RelativeSizeAxes = Axes.Y
        });
        AddInternal(new Box
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Height = 2,
            Colour = Colour4.AliceBlue.MultiplyAlpha(0.5f),
            RelativeSizeAxes = Axes.X
        });
    }
}