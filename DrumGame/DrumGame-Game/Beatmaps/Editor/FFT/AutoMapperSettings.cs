using System;
using System.Collections.Generic;
using DrumGame.Game.Beatmaps.Editor.Timing;
using DrumGame.Game.Channels;
using ManagedBass;
using Newtonsoft.Json;
using osu.Framework.Graphics;

namespace DrumGame.Game.Beatmaps.Editor.FFT;

public class AutoMapperSettings
{
    // should match auto-mapper-schema.json
    public class AutoTriggerSettingsBase
    {
        public int LowBin;
        public int HighBin;
        public float Multiplier;
    }
    public class AutoTriggerSettings : AutoTriggerSettingsBase
    {
        public DrumChannel Channel;
        public float Climb;
        public float MinimumThreshold;
        public float MinimumIntensity; // default to 0
        public double TimeCorrectionMs;
        public string PlotColor;
        public Colour4? GetPlotColor() => Colour4.TryParseHex(PlotColor, out var o) ? o : null;
        public AutoTriggerSettingsBase Subtract;

        public float ComputeValue(Span<float> bins)
        {
            var endBin = HighBin;
            var sum = 0f;
            var min = MinimumIntensity / 100; // tooltips are multiplied by 100
            if (min <= 0)
            {
                for (var j = LowBin; j <= endBin; j++)
                    sum += bins[j];
            }
            else
            {
                for (var j = LowBin; j <= endBin; j++)
                    if (RescaleFftValue(bins[j], j) > min)
                        sum += bins[j];
            }
            sum *= Multiplier;
            var sub = Subtract;
            if (sub != null)
            {
                for (var j = sub.LowBin; j <= sub.HighBin; j++)
                    sum -= bins[j] * sub.Multiplier;
            }
            return sum;
        }
    }
    public List<AutoTriggerSettings> Triggers;
    public class AutoMapperSettings_FFT
    {
        public DataFlags FFTSamples;
        public int Oversample;
        public int StoredBins;
        public FFTSettings ToFFTSettings(string audioPath) => new(FFTSamples)
        {
            Oversample = Oversample,
            StoredBins = StoredBins,
            TargetPath = audioPath
        };
    }
    public AutoMapperSettings_FFT FFT;
    public int PlotResolutionX;
    public int PlotHeight;
    public bool EnableWatcher;

    public static float RescaleFftValue(float value, int bin) => value * (bin / 20f + 0.75f);

    [JsonIgnore] public string Source;
}