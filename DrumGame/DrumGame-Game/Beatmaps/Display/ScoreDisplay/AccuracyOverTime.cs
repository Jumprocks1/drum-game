using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Beatmaps.Replay;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Browsers;
using DrumGame.Game.Channels;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Stores.DB;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;

namespace DrumGame.Game.Beatmaps.Display.ScoreDisplay;

public class AccuracyOverTime : CompositeDrawable
{
    Beatmap Beatmap;
    double PracticeAcc => Util.ConfigManager.PracticeConfig.Value.TargetAccuracyPercent / 100;
    Box PracticeWindowBox;
    public AccuracyOverTime(Beatmap beatmap, ReplayResults results)
    {
        Beatmap = beatmap;
        // this whole constructor costs like 1-2ms
        Height = 100;
        var plotH = 70;

        var judgements = results.Judgements;
        if (judgements.Count == 0) return;

        AddInternal(new SpriteText
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Text = "Accuracy Over Time"
        });
        AddInternal(PracticeWindowBox = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = Colour4.Gray.MultiplyAlpha(0.5f),
            Alpha = 0,
            RelativePositionAxes = Axes.Both,
        });

        // weighting function is based on e^-x, where x is the distance to the current sample point
        // at this distance, the weight is 36% of what it was at 0 distance
        // at 2x this distance, the weight is ~2%
        var windowWidthMs = 5000;

        // anything outside 3x this window should be ignored as the weight is effectively 0 (it would be e^-9)
        var fullWindow = windowWidthMs * 3;

        // how many plot samples to collect, has little impact on final values
        var resolution = 200;

        // we will take samples at start, start + 1 / (resolution - 1), ..., end
        var start = judgements[0].Item1;
        var end = judgements[^1].Item1;
        var length = end - start;

        var plotData = new (double time, double accuracy)[resolution];
        var min = PracticeAcc;
        var max = PracticeAcc;

        var globalMin = double.MaxValue;
        var globalMinI = 0;

        // current position in the judgement list. Used to prevent seeking through whole song
        var judgementI = 0;
        for (var sampleI = 0; sampleI < resolution; sampleI++)
        {
            // center of window
            var time = start + sampleI * length / (resolution - 1);
            var windowStart = time - fullWindow;
            var windowEnd = time + fullWindow;
            while (judgementI < judgements.Count && judgements[judgementI].Item1 < windowStart)
                judgementI += 1;
            var scored = 0d;
            var total = 0d;
            for (var i = judgementI; i < judgements.Count; i++)
            {
                var (judgementTime, rating) = judgements[i];
                if (judgementTime > windowEnd) break;
                var d = (time - judgementTime) / windowWidthMs;
                var weight = Math.Pow(Math.E, -(d * d));
                scored += rating * weight;
                total += 3 * weight;
            }
            var v = total == 0 ? 1 : scored / total;

            if (v > max) max = v;
            if (v < globalMin)
            {
                globalMin = v;
                globalMinI = sampleI;
            }
            if (v < min) min = v;
            plotData[sampleI] = (time, v);
        }
        max += 0.05;
        min -= 0.05;

        (double StartBeat, double EndBeat) UpdatePracticeWindow(int i)
        {
            if (cachedWindow != null && cachedWindow.Value.i == i)
                return (cachedWindow.Value.StartBeat, cachedWindow.Value.EndBeat);

            while (i > 0 && plotData[i - 1].accuracy < plotData[i].accuracy) i--;
            while (i < plotData.Length - 1 && plotData[i + 1].accuracy < plotData[i].accuracy) i++;
            if (cachedWindow != null && cachedWindow.Value.i == i)
                return (cachedWindow.Value.StartBeat, cachedWindow.Value.EndBeat);

            var hardMeasure = beatmap.MeasureFromBeat(beatmap.BeatFromMilliseconds(plotData[i].time) + Beatmap.BeatEpsilon);

            int scoreStartMeasure(int s)
            {
                var res = -Math.Abs(hardMeasure - 2 - s); // try to start 2 measures before low point
                var tick = beatmap.TickFromMeasure(s);
                foreach (var hit in beatmap.GetHitObjectsAtTick(tick))
                {
                    var ho = beatmap.HitObjects[hit];
                    if (ho.Channel == DrumChannel.Crash && ho.Modifiers.HasFlag(NoteModifiers.Accented)) res += 4;
                    if (ho.Channel == DrumChannel.Snare) res += 1;
                    else if (ho.Channel == DrumChannel.BassDrum) res += 2;
                    else if (ho.Channel == DrumChannel.Crash || ho.Channel == DrumChannel.China) res += 4;
                    else if (ho.Channel == DrumChannel.Splash) res += 4;
                }
                if (!beatmap.GetHitObjectsInTicks(tick - beatmap.TickRate * 4, tick).Any())
                    res += 10;
                else if (!beatmap.GetHitObjectsInTicks(tick - beatmap.TickRate * 3, tick).Any())
                    res += 8;
                else if (!beatmap.GetHitObjectsInTicks(tick - beatmap.TickRate * 2, tick).Any())
                    res += 6;
                else if (!beatmap.GetHitObjectsInTicks(tick - beatmap.TickRate, tick).Any())
                    res += 3;
                else if (!beatmap.GetHitObjectsInTicks(tick - beatmap.TickRate / 2, tick).Any())
                    res += 1;
                return res;
            }
            var startMeasure = hardMeasure - 2;
            var score = scoreStartMeasure(startMeasure);

            for (var j = hardMeasure - 5; j < hardMeasure; j++)
            {
                var newScore = scoreStartMeasure(j);
                if (newScore > score)
                {
                    startMeasure = j;
                    score = newScore;
                }
            }

            var endMeasure = Math.Max(hardMeasure + 2, startMeasure + 4);
            var res = (beatmap.BeatFromMeasure(startMeasure), beatmap.BeatFromMeasure(endMeasure));
            cachedWindow = (i, res.Item1, res.Item2);

            PracticeWindowBox.Alpha = 1;
            var x1 = Math.Clamp((float)((beatmap.MillisecondsFromBeat(res.Item1) - start) / length), 0, 1);
            var x2 = Math.Clamp((float)((beatmap.MillisecondsFromBeat(res.Item2) - start) / length), 0, 1);
            PracticeWindowBox.X = x1;
            PracticeWindowBox.Width = x2 - x1;
            return res;
        }
        UpdatePracticeWindow(globalMinI);

        var vertices = new float[resolution];
        Plot plot = null;
        plot = new Plot
        {
            RelativeSizeAxes = Axes.X,
            Height = plotH,
            Anchor = Anchor.BottomCentre,
            Origin = Anchor.BottomCentre,
            Clicked = (i, _) =>
            {
                var (StartBeat, EndBeat) = UpdatePracticeWindow(i);
                practice(StartBeat, EndBeat);
            },
            SampleTooltip = (i, _) =>
            {
                try
                {
                    var (time, accuracy) = plotData[i];
                    var beat = (int)beatmap.BeatFromMilliseconds(time);
                    var (StartBeat, EndBeat) = UpdatePracticeWindow(i);
                    return
                        $"Accuracy: {accuracy * 100:0.00}%\n"
                        + $"<brightGreen>Time:</c> {Util.FormatTime(time)}\n<brightGreen>Beat:</c> {beat}\n"
                        + $"Recommended practice window: {StartBeat}-{EndBeat} <faded>(click or activate {IHasCommand.GetMarkupTooltipIgnoreUnbound(Command.PracticeMode)} to practice)</c>\n"
                        + $"<good>Target accuracy:</c> {PracticeAcc * 100}";
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Accuracy tooltip error");
                    return "Tooltip failed to load, see log for details";
                }
            },
            Colour = Colour4.White,
            PathRadius = 1.5f,
            Vertices = vertices
        };
        AddInternal(new Box
        {
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.CentreLeft,
            Y = (float)(-(PracticeAcc - min) / (max - min) * plotH),
            RelativeSizeAxes = Axes.X,
            Colour = Util.HitColors.Good.MultiplyAlpha(0.5f),
            Height = 2
        });
        for (var i = 0; i < resolution; i++)
            vertices[i] = (float)((1 - (plotData[i].accuracy - min) / (max - min)) * plotH);
        plot.Invalidate();
        AddInternal(plot);
        Util.CommandController.RegisterHandlers(this);
    }

    [CommandHandler]
    public void PracticeMode()
    {
        if (cachedWindow.HasValue) practice(cachedWindow.Value.StartBeat, cachedWindow.Value.EndBeat);
    }

    void practice(double startBeat, double endBeat)
    {
        bool tryEnter(BeatmapPlayer player)
        {
            if (player?.Display is MusicNotationBeatmapDisplay display && display.Beatmap == Beatmap)
            {
                display.Player.EnterPracticeMode(startBeat, endBeat);
                return true;
            }
            return false;
        }
        if (tryEnter(Util.GetParent<BeatmapPlayer>(this))) return;
        tryEnter(Util.Find<BeatmapSelectorLoader>(Util.DrumGame)?.LoadMap(Beatmap));
    }

    (int i, double StartBeat, double EndBeat)? cachedWindow;

    protected override void Dispose(bool isDisposing)
    {
        Util.CommandController.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }
}