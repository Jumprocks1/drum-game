using System;
using System.Collections.Generic;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Beatmaps.Editor.FFT;
using DrumGame.Game.Components;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace DrumGame.Game.Beatmaps.Editor.Timing;

public class TriggerPlot : CompositeDrawable, IHasMarkupTooltip
{
    BeatmapEditor Editor;
    FFTProvider FFT;
    BeatClock Track => Editor.Track;
    MusicNotationBeatmapDisplay Display => Editor.Display;
    Beatmap Beatmap => Editor.Beatmap;
    List<AutoMapperSettings.AutoTriggerSettings> TriggerSettings => FFT.LatestSettings.Triggers;
    float Inset => Display.Inset;

    public string MarkupTooltip
    {
        get
        {
            var pos = ToLocalSpace(Util.Mouse.Position);
            var t = (double)pos.X / DrawWidth * (CurrentRange.Item2 - CurrentRange.Item1) + CurrentRange.Item1;
            var beat = Beatmap.BeatFromMilliseconds(t);
            var h = 1 - pos.Y / DrawHeight;

            var i = Math.Clamp((int)(pos.X / DrawWidth * Resolution), 0, Resolution - 1);
            var tt = $"beat {beat:0.00}, {h * maxValue:0.00}";
            for (var j = 0; j < Plots.Length; j++)
                if (Plots[j] != null)
                {
                    var name = MarkupText.Color($"Plot[{j}]", Plots[j].Colour);
                    tt += $"\n{name} {(1 - Plots[j].Vertices[i] / DrawHeight) * maxValue:0.00}";
                }
            return tt;
        }
    }

    public TriggerPlot(BeatmapEditor editor)
    {
        Editor = editor;
        RelativeSizeAxes = Axes.X;
        FFT = Editor.GetFFT();
        FFT.OnSettingsChanged += OnSettingsChanged;
        OnSettingsChanged(null, FFT.LatestSettings);
    }

    Plot[] Plots;

    void OnSettingsChanged(AutoMapperSettings oldSettings, AutoMapperSettings settings)
    {
        var triggers = settings.Triggers;
        if (Plots != null && Plots.Length != triggers.Count)
        {
            foreach (var plot in Plots)
                RemoveInternal(plot, true);
            Plots = null;
        }
        Plots ??= new Plot[triggers.Count];
        Resolution = settings.PlotResolutionX;
        Height = settings.PlotHeight;
        for (var i = 0; i < triggers.Count; i++)
        {
            var v = triggers[i];
            var p = Plots[i];
            if (v.PlotColor != null && Colour4.TryParseHex(v.PlotColor, out var color))
            {
                if (p == null)
                {
                    Plots[i] = p = new()
                    {
                        RelativeSizeAxes = Axes.Both,
                        PathRadius = 1,
                        SampleTooltip = (_, __) => null,
                    };
                    AddInternal(p);
                }
                if (p.Vertices == null || p.Vertices.Length != Resolution)
                    p.Vertices = new float[Resolution];
                p.Colour = color;
            }
            else
            {
                if (p != null) RemoveInternal(p, true);
                Plots[i] = null;
            }
        }
        verticesValid = false;
    }

    public int Resolution = 1000;
    float maxValue;
    float NextMaxValue = 1.33f; // loaded into maxValue next render

    bool verticesValid = false;

    (double, double) CurrentRange;
    protected override void Update()
    {
        maxValue = NextMaxValue;
        var currentQuarterNote = Track.CurrentBeat;
        var leftBeat = currentQuarterNote - Inset;
        var rightBeat = leftBeat + Display.DrawWidth / Display.BeatWidth;
        var newRange = (Beatmap.MillisecondsFromBeat(leftBeat), Beatmap.MillisecondsFromBeat(rightBeat));
        if (newRange != CurrentRange)
        {
            CurrentRange = newRange;
            verticesValid = false;
        }
        UpdatePlot();
        base.Update();
    }


    void UpdatePlot()
    {
        if (verticesValid) return;
        // this is pretty CPU intensive. Could be 100% fixed by rounding time to chunks and caching
        // GPU upload isn't too bad, so we can just do a simple cache
        var triggers = TriggerSettings;
        for (var i = 0; i < Resolution; i++)
        {
            // careful, if there's BPM changes, this is NOT correct.
            // time is only linear if BPM is linear over the time range (which only happens when there's no changes)
            var t = (double)i / Resolution * (CurrentRange.Item2 - CurrentRange.Item1) + CurrentRange.Item1;
            for (var j = 0; j < Plots.Length; j++)
            {
                if (Plots[j] != null)
                {
                    var s = triggers[j].LowBin;
                    var e = triggers[j].HighBin;
                    var sum = 0f;
                    var chunk = FFT.ChunkAt(t - triggers[j].TimeCorrectionMs);
                    var fft = FFT.FFTAtChunk(chunk);
                    for (var k = s; k <= e; k++)
                        sum += fft[k];
                    sum *= triggers[j].Multiplier;

                    if (sum > NextMaxValue) NextMaxValue = sum;

                    Plots[j].Vertices[i] = (1 - sum / maxValue) * Height;
                }
            }
        }
        verticesValid = true;
        for (var j = 0; j < Plots.Length; j++)
            Plots[j]?.Invalidate();
    }
}