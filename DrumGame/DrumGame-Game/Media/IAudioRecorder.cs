using System;
using ManagedBass;

namespace DrumGame.Game.Media;

public interface IAudioRecorder : IDisposable
{
    public string Device { set; }
    public Action<RecorderInitArgs> AfterInit { set; }
    public Action<AudioAvailableArgs> AudioAvailable { set; }
    public void StartRecording();
}
public record AudioAvailableArgs
{
    public unsafe ReadOnlySpan<byte> Buffer => new ReadOnlySpan<byte>(Pointer.ToPointer(), Length);

    public IntPtr Pointer;
    public int Length;
}
public record RecorderInitArgs
{
    public int Channels;
    public int SampleRate;
    public int BytesPerSample;
    public int BytesPerFrame => Channels * BytesPerSample;
    public Resolution Resolution;
    public static int GetBytesPerSample(Resolution resolution) => resolution switch
    {
        Resolution.Byte => 1,
        Resolution.Short => 2,
        Resolution.Float => 4,
        _ => -1
    };
    public NAudio.Wave.WaveFormat WaveFormat => Resolution switch
    {
        Resolution.Float => NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels),
        Resolution.Short => new NAudio.Wave.WaveFormat(SampleRate, Channels),
        _ => throw new NotSupportedException()
    };
}