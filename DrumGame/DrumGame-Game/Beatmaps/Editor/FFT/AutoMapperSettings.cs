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
    public class AutoTriggerSettings
    {
        public DrumChannel Channel;
        public int LowBin;
        public int HighBin;
        public float Multiplier;
        public float Climb;
        public float MinimumThreshold;
        public double TimeCorrectionMs;
        public string PlotColor;
        public Colour4? GetPlotColor() => Colour4.TryParseHex(PlotColor, out var o) ? o : null;
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

    [JsonIgnore] public string Source;
}