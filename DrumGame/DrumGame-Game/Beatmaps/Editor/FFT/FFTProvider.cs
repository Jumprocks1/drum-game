using System;
using System.IO;
using System.IO.Compression;
using DrumGame.Game.Beatmaps.Editor.FFT;
using DrumGame.Game.Utils;
using ManagedBass;
using osu.Framework.Audio.Callbacks;
using osu.Framework.Caching;
using osu.Framework.Extensions;
using osu.Framework.Logging;

namespace DrumGame.Game.Beatmaps.Editor.Timing;

public class FFTSettings
{
    public FFTSettings(DataFlags baseFlag)
    {
        FFTFlagSize = GetFFTFlagSize(baseFlag);
        FFTFlag = baseFlag | DataFlags.FFTRemoveDC;
        FFTBinCount = FFTFlagSize / 2;
        StoredBins = FFTBinCount;
    }
    public readonly DataFlags FFTFlag;
    public readonly int FFTFlagSize;
    public readonly int FFTBinCount;

    public bool IgnoreCache;

    // can be limited to reduce memory usage
    public int StoredBins;

    // Default means if we're using FFT4096, we run an FFT every 4096 samples
    // this provides poor time accuracy, so we can oversample to correct this
    // oversample for our FrequencyImage => (FFT_Flag / SampleRate) / (TimeWidth / 1000 / TextureWidth) => currently 27.8
    // 32 should be good
    public int Oversample = 1;

    public int ChunkWidth => FFTFlagSize / Oversample; // in samples

    public string TargetPath;

    public static int GetFFTBinCount(DataFlags flag) => GetFFTFlagSize(flag) / 2;
    public static int GetFFTFlagSize(DataFlags flag) => flag switch
    {
        DataFlags.FFT256 => 256,
        DataFlags.FFT512 => 512,
        DataFlags.FFT1024 => 1024,
        DataFlags.FFT2048 => 2048,
        DataFlags.FFT4096 => 4096,
        DataFlags.FFT8192 => 8192,
        DataFlags.FFT16384 => 16384,
        DataFlags.FFT32768 => 32768,
        _ => throw new Exception($"Missing FFT flag {flag}")
    };

    public string MD5()
    {
        // this is really inefficient but it doesn't really matter
        return Util.MD5(FFTFlag.ToString(), StoredBins.ToString(), Oversample.ToString(), TargetPath);
    }
}

// not thread safe, make sure to call from consistent thread
public class FFTProvider : IDisposable
{
    public static string AutoMapperSettingsPath => Util.Resources.GetAbsolutePath("auto-mapper.json");


    public AutoMapperSettings LatestSettings;
    public FileWatcher<AutoMapperSettings> Watcher;

    // ~3ms with 4096 flag and 32 oversample
    public double TimeResolution => (double)Settings.FFTFlagSize / Oversample / SampleRate;

    FFTSettings Settings;
    int Oversample => Settings.Oversample;

    public int AvailableBins => Settings.StoredBins;

    MemoryStream AudioStream;
    int DecodeStream; // BASS stream
    ChannelInfo ChannelInfo;
    public int SampleRate => ChannelInfo.Frequency;
    long EstimatedLengthSamples;

    public bool Disposed { get; private set; }

    float[] CachedData;
    float[] Buffer; // just used when pulling data from BASS

    public readonly int ChunkCount;
    readonly int ChunkWidth;
    public double ChunkWidthS => (double)ChunkWidth / SampleRate;
    public double ChunkWidthMs => 1000d * ChunkWidth / SampleRate;
    readonly int BytesPerSample;

    public int FFTFlagSize => Settings.FFTFlagSize;

    public string GetCachePath() => Util.Resources.GetTemp($"fft-{Settings.MD5()}.bin");

    bool[] loadedChunks;
    bool allLoaded;

    public static FFTProvider FromSettingsFile(string path, string audioPath)
    {
        var settings = FileWatcher<AutoMapperSettings>.Load(path);
        settings.Source = path;
        return new FFTProvider(settings, audioPath);
    }

    public FFTProvider(AutoMapperSettings settings, string audioPath) : this(settings.FFT.ToFFTSettings(audioPath))
    {
        LatestSettings = settings;
        if (settings.EnableWatcher)
        {
            Watcher = new(settings.Source);
            Watcher.Register();
            Watcher.JsonChanged += SettingsChanged;
        }
    }

    public event Action<AutoMapperSettings, AutoMapperSettings> OnSettingsChanged;

    void SettingsChanged(AutoMapperSettings newSettings)
    {
        Util.UpdateThread.Scheduler.Add(() =>
        {
            var oldSettings = LatestSettings;
            var newFftSettings = newSettings.FFT.ToFFTSettings(Settings.TargetPath);
            if (newFftSettings.MD5() != Settings.MD5())
                LoadNewFFT(newFftSettings);
            LatestSettings = newSettings;
            OnSettingsChanged?.Invoke(oldSettings, LatestSettings);
        });
    }

    public FFTProvider(FFTSettings settings)
    {
        Settings = settings;
        if (settings.StoredBins > Settings.FFTBinCount) throw new Exception("Requested bins greated than supplied");
        ChunkWidth = Settings.ChunkWidth;

        using var file = File.OpenRead(settings.TargetPath);
        AudioStream = new MemoryStream(file.ReadAllRemainingBytesToArray());
        var fileCallbacks = new FileCallbacks(new DataStreamFileProcedures(AudioStream));
        DecodeStream = Bass.CreateStream(StreamSystem.NoBuffer, BassFlags.Decode | BassFlags.Float, fileCallbacks.Callbacks, fileCallbacks.Handle);
        ChannelInfo = Bass.ChannelGetInfo(DecodeStream);
        BytesPerSample = 4 * ChannelInfo.Channels;
        EstimatedLengthSamples = Bass.ChannelGetLength(DecodeStream) / BytesPerSample;
        Buffer = new float[Settings.FFTBinCount];

        ChunkCount = (int)(EstimatedLengthSamples / ChunkWidth);
        // ChunkCount = 500;
        CachedData = new float[Settings.StoredBins * ChunkCount];
        loadedChunks = new bool[ChunkCount];

        if (!Settings.IgnoreCache)
        {
            var cachePath = GetCachePath();
            // we don't use gz since it makes it 10x slower
            if (File.Exists(cachePath))
            {
                using var _ = Util.WriteTime();
                var bytes = File.ReadAllBytes(cachePath);
                if (bytes.Length != CachedData.Length * 4)
                {
                    Logger.Log("Length mismatch, ignoring cache", level: LogLevel.Important);
                }
                else
                {
                    // using var gz = new GZipStream(fileS, CompressionMode.Decompress);
                    // var bytes = gz.ReadAllRemainingBytesToArray();
                    System.Buffer.BlockCopy(bytes, 0, CachedData, 0, bytes.Length);
                    allLoaded = true;
                }
            }
        }
    }

    public void LoadNewFFT(FFTSettings settings) // for now we won't support this
    {
        // Settings = settings;
    }

    public Span<float> FFTAtMs(double milliseconds)
    {
        var s = milliseconds / 1000;
        var sample = (int)(s * SampleRate);
        return FFTAtSample(sample);
    }
    public double ChunkToMs(int chunk) => (double)chunk * ChunkWidth / SampleRate * 1000;
    public int ChunkAt(double milliseconds) => ((int)(milliseconds / 1000 * SampleRate)) / ChunkWidth;
    public Span<float> FFTAtSample(int sample) => FFTAtChunk(sample / ChunkWidth);
    public Span<float> FFTAtChunk(int chunk, bool seek = true)
    {
        chunk = Math.Clamp(chunk, 0, ChunkCount - 1);
        var start = chunk * Settings.StoredBins;

        if (!loadedChunks[chunk] && !allLoaded)
        {
            if (seek)
            {
                var bytePosition = chunk * ChunkWidth * BytesPerSample;
                Bass.ChannelSetPosition(DecodeStream, bytePosition);
            }
            var read = Bass.ChannelGetData(DecodeStream, Buffer, (int)Settings.FFTFlag);
            if (read < 0) return null;
            if (read > Settings.StoredBins) read = Settings.StoredBins;
            Array.Copy(Buffer, 0, CachedData, start, read);
            loadedChunks[chunk] = true;
        }

        return CachedData.AsSpan(start, Settings.StoredBins);
    }

    public void CacheAll()
    {
        if (allLoaded) return;
        for (var i = 0; i < Settings.Oversample; i += 1)
        {
            // we don't do this in order so we can optimize for number of seeks, this seems to make it ~4 times faster
            Bass.ChannelSetPosition(DecodeStream, i * Settings.ChunkWidth * BytesPerSample);
            for (var j = i; j < ChunkCount; j += Settings.Oversample)
                FFTAtChunk(j, false);
        }
        allLoaded = true;
        if (!Settings.IgnoreCache)
        {
            using var fs = File.Open(GetCachePath(), FileMode.Create);
            // using var gz = new GZipStream(fs, CompressionLevel.Optimal);
            using var cacheFile = new BinaryWriter(fs);
            for (var i = 0; i < CachedData.Length; i++)
                cacheFile.Write(CachedData[i]);
        }
    }

    public void Dispose()
    {
        if (Disposed) return;
        Buffer = null;
        CachedData = null;
        Disposed = true;
        Watcher?.Dispose();
        Watcher = null;
        AudioStream?.Dispose();
        AudioStream = null;
        Bass.StreamFree(DecodeStream);
    }
}