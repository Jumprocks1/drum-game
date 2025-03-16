using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics;
using System;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Stores.DB;
using DrumGame.Game.Beatmaps.Replay;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using DrumGame.Game.Components;

namespace DrumGame.Game.Beatmaps.Display.ScoreDisplay;

public class AccuracyPlot : CompositeDrawable
{
    public AccuracyPlot(HitWindows hitWindows, ReplayInfo replayInfo, ReplayResults results)
    {
        AutoSizeAxes = Axes.Both;


        var hitErrors = results.HitErrors;
        var count = 101;
        var vertices = new float[count];
        var rawData = new double[count];
        var h = 100;
        var plot = new Plot
        {
            Width = count * 3,
            Height = h,
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Colour = Colour4.White,
            PathRadius = 1,
            Vertices = vertices,
            SampleTooltip = (i, _) =>
            {

                var ms = i - 50;

                var absMs = Math.Abs(ms);

                var rating = hitWindows.GetRating(absMs);
                var color = hitWindows.GetColor(rating);

                var coloredMs = MarkupText.Color(absMs.ToString(), color) + "ms";

                var signedMs = ms < 0 ? $"-{coloredMs}" : $"+{coloredMs}";
                var r = $"{signedMs} - {rawData[i]:0.00}%\n\n";
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
            },
            Y = 30
        };
        var accuracyTotal = replayInfo.AccuracyTotal;
        var min = 100.0;
        var max = 0.0;
        for (var i = 0; i < count; i++)
        {
            var accuracyHit = 0;
            var change = i - 50;
            for (var j = 0; j < hitErrors.Count; j++)
            {
                var error = hitErrors[j] - change;
                var rating = hitWindows.GetRating(Math.Abs(error));
                accuracyHit += BeatmapScorerBase.RatingValue(rating);
            }
            rawData[i] = (double)(accuracyHit * 100) / accuracyTotal;
            if (rawData[i] > max) max = rawData[i];
            if (rawData[i] < min) min = rawData[i];
        }
        for (var i = 0; i < count; i++)
            vertices[i] = (float)((1 - (rawData[i] - min) / (max - min)) * h);
        plot.Invalidate();
        AddInternal(new SpriteText
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Colour = Colour4.White,
            Text = "Accuracy at different offset adjustments"
        });
        AddInternal(new Box
        {
            Colour = Colour4.White.MultiplyAlpha(0.3f),
            Width = 2,
            Height = h,
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Y = 30
        });
        AddInternal(plot);
    }
}