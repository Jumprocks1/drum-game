using System;
using System.IO;
using DrumGame.Game.Media;

// Discrete Fourier Transform
public class DFT
{
    float[,] Data;
    public readonly int Channels;
    public readonly int SampleCount;
    public readonly int SampleRate;

    public DFT(AudioDump dump, int sampleRate)
    {
        Data = dump.SampleBuffer;
        Channels = Data.GetLength(0);
        SampleCount = dump.SampleCount;
        SampleRate = sampleRate;
    }

    float Mono(int sample) => Channels == 1 ? Data[0, sample] : (Data[0, sample] + Data[1, sample]) / 2;

    public DFT(Stream stream)
    {
        stream.Dispose();
    }

    public int ToSample(double time) => (int)(time / SampleRate);

    // https://www.mstarlabs.com/dsp/goertzel/goertzel.html
    // https://medium.com/reverse-engineering/audio-player-with-voice-control-using-arduino-a-frequency-analyzer-based-on-the-goertzel-algorithm-6f79766e37ad
    public double Goertzel(float hz, int start, int end)
    {
        // Console.WriteLine($"{hz} {start} {end}");
        // end sample exclusive
        var N = end - start;

        var omega = 2.0 * Math.PI * hz / SampleRate;
        var (imagW, realW) = Math.SinCos(omega);
        realW *= 2;


        var d1 = 0.0;
        var d2 = 0.0;
        for (var i = 0; i < N; ++i)
        {
            var window = 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / N); // hamming window greatly improves output
            var y = Data[0, i + start] * window + realW * d1 - d2;
            d2 = d1;
            d1 = y;
        }
        var rr = 0.5 * realW * d1 - d2;
        var ri = imagW * d1;
        return Math.Sqrt(rr * rr + ri * ri);
    }

    public void Goertzel(float[] target, double time)
    {
        for (var i = 0; i < Config.SampleCount; i++)
        {
            var config = Config.SampleConfig(i);
            var hz = config.hz;
            var radius = (int)(SampleRate / hz * 5);
            var start = (int)(time / 1000 * SampleRate - radius);
            // since lower frequencyies need wider windows, they also have larger outputs
            // we can normalize this by multiplying by the frequency
            // this gives a very flat graph maximum (when frequency have same volume)
            var normalize = hz / 500;
            target[i] = (float)Goertzel(hz, Math.Max(0, start), Math.Min(start + radius * 2, SampleCount)) * normalize;
        }
    }
    public record SampleConfig(float hz);
    public class LoadedConfig
    {
        public readonly int StartHz;
        public readonly int EndHz;
        public readonly int SampleCount;
        readonly SampleConfig[] sampleConfigs;
        public LoadedConfig(DFTConfig config)
        {
            StartHz = config.StartHz;
            EndHz = config.EndHz;
            var range = EndHz - StartHz;
            var logRange = EndHz / (double)StartHz;
            SampleCount = config.SampleCount;
            float Sampler(int i) => (float)(StartHz * Math.Pow(logRange, i / (float)SampleCount));

            sampleConfigs = new SampleConfig[SampleCount];

            for (var i = 0; i < SampleCount; i++)
            {
                var hz = Sampler(i);
                sampleConfigs[i] = new SampleConfig(hz);
            }
        }
        public float SampleHz(int i) => sampleConfigs[i].hz;
        public SampleConfig SampleConfig(int i) => sampleConfigs[i];
    }
    public record DFTConfig(int StartHz, int EndHz, int SampleCount = 100);
    public LoadedConfig Config;
    public void LoadConfig(DFTConfig config)
    {
        Config = new LoadedConfig(config);
    }
}