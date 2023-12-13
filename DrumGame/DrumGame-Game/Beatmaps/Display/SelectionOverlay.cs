using System;
using System.Collections.Generic;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace DrumGame.Game.Beatmaps.Display;

public class SelectionOverlay : CompositeDrawable
{
    readonly MusicNotationBeatmapDisplay Display;
    public Beatmap Beatmap => Display.Beatmap;
    List<Box> Divisors = new();
    Box box;
    public int VisibleDivisors = 0;
    public int Step = 0;
    public int Start = 0;
    public int End = 0;
    public void Update(int start, int end, int step)
    {
        if (start == end)
        {
            Alpha = 0;
            return;
        }
        else
        {
            Alpha = 1;
        }
        var small = Math.Min(start, end);
        end = Math.Max(start, end);
        start = small;
        if (Start == start && End == end && Step == step) return;
        Start = start;
        End = end;
        Step = step;
        Width = (float)((double)(end - start) / Beatmap.TickRate * Display.Font.Spacing);
        X = (float)((double)start / Beatmap.TickRate * Display.Font.Spacing);
        UpdateDividers();
    }
    public void UpdateDividers()
    {
        var i = 0;
        if (Step > 0)
        {
            for (int t = Start; t < End; t += Step)
            {
                if (Divisors.Count <= i)
                {
                    var newD = new Box
                    {
                        Colour = Colour4.SeaGreen.Darken(0.2f).MultiplyAlpha(0.4f),
                        Y = -4,
                        Height = 12,
                        Width = 0.15f,
                        Origin = Anchor.TopCentre
                    };
                    Divisors.Add(newD);
                    AddInternal(newD);
                }
                var d = Divisors[i];
                d.Alpha = 1;
                d.X = (float)((double)(t - Start) / Beatmap.TickRate * Display.Font.Spacing);
                i += 1;
            }
        }
        for (int j = i; j < VisibleDivisors; j++) Divisors[j].Alpha = 0;
        VisibleDivisors = i;
    }
    public SelectionOverlay(MusicNotationBeatmapDisplay display)
    {
        Display = display;
        Depth = -1;
        Alpha = 0;
        AddInternal(box = new Box
        {

            Colour = Colour4.SeaGreen.MultiplyAlpha(0.2f),
            Y = -4,
            Height = 12,
            RelativeSizeAxes = Axes.X
        });
    }
}
