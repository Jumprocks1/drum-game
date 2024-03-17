using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics;
using System;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Stores.DB;
using DrumGame.Game.Beatmaps.Replay;
using osu.Framework.Graphics.Shapes;
using DrumGame.Game.Utils;
using osu.Framework.Graphics.Sprites;
using DrumGame.Game.Stores.Skins;
using DrumGame.Game.Components;

namespace DrumGame.Game.Beatmaps.Display.ScoreDisplay;

public class ErrorHistogram : CompositeDrawable
{
    public ErrorHistogram(ReplayResults results)
    {
        var hitErrors = results.HitErrors;
        var radius = 100;
        var count = radius * 2 + 1;
        var vertices = new float[count];
        var rawData = new float[count];
        var h = 100;
        var scaling = 2;
        Width = count * scaling;
        Height = h + 30;
        var plot = new Plot
        {
            Width = count * scaling,
            Height = h,
            Anchor = Anchor.BottomCentre,
            Origin = Anchor.BottomCentre,
            Colour = Colour4.White,
            PathRadius = 1,
            Vertices = vertices,
            SampleTooltip = (i, _) =>
            {
                var ms = i - radius;

                var absMs = Math.Abs(ms);

                var rating = BeatmapScorer.HitWindows.GetRating(absMs);
                var color = BeatmapScorer.HitWindows.GetColor(rating);

                var coloredMs = MarkupText.Color(absMs.ToString(), color) + "ms";
                var signedMs = ms < 0 ? $"-{coloredMs}" : $"+{coloredMs}";

                var r = $"{signedMs} - {rawData[i]} hits\n\n";
                if (ms < 0)
                {
                    r += $"To correct for this, subtract {coloredMs} from your input offset";
                    r += $"\nIf you believe the map is mistimed, subtract {coloredMs} from the map offset instead.";
                }
                else
                {
                    r += $"To correct for this, add {coloredMs} to your input offset";
                    r += $"\nIf you believe the map is mistimed, add {coloredMs} to the map offset instead.";
                }
                return r;
            }
        };
        var max = 0f;
        foreach (var error in hitErrors)
        {
            var i = (int)Math.Round(error + radius);
            if (i < 0 || i >= count) continue;
            max = Math.Max(max, ++rawData[i]);
        }
        for (var i = 0; i < count; i++)
            plot.Vertices[i] = (1 - rawData[i] / max) * h;
        plot.Invalidate();
        AddInternal(plot);

        var windows = new HitWindows();
        void AddLine(float ms, Colour4 color)
        {
            if (ms > radius) return;
            AddInternal(new Box
            {
                Colour = color.MultiplyAlpha(0.5f),
                Width = 2,
                Height = h,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                X = ms * scaling
            });
            if (ms != 0)
                AddInternal(new Box
                {
                    Colour = color.MultiplyAlpha(0.5f),
                    Width = 2,
                    Height = h,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    X = -ms * scaling
                });
        }
        AddLine(0, Util.HitColors.Perfect);
        AddLine(windows.PerfectWindow, Util.HitColors.Good);
        AddLine(windows.GoodWindow, Util.HitColors.Bad);
        AddLine(windows.BadWindow, Util.HitColors.Miss);
        AddInternal(new SpriteText
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Colour = Colour4.White,
            Text = "Hit error histogram"
        });
    }
}