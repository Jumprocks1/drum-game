using System;
using System.Collections.Generic;
using ManagedBass;
using osu.Framework;
using osu.Framework.Logging;

namespace DrumGame.Game.Media;

public record AlsaInitArgs
{
    public int FramesPerBuffer;
    public int Channels;
    public int SampleRate;
}

public class AlsaAudioRecorder : IAudioRecorder
{
    public static IEnumerable<string> GetDeviceNames()
    {
        var count = Bass.RecordingDeviceCount;
        for (var i = 0; i < count; i++)
        {
            var d = Bass.RecordGetDeviceInfo(i);
            yield return d.Name;
        }
    }
    public string Device { get; set; }
    public Action<RecorderInitArgs> AfterInit { get; set; }
    public Action<AudioAvailableArgs> AudioAvailable { get; set; }

    int recordingHandle;


    bool recording = false;

    public AlsaAudioRecorder() { }

    public void StartRecording()
    {
        Utils.Util.Host.AudioThread.Scheduler.Add(() =>
        {
            var count = Bass.RecordingDeviceCount;
            Logger.Log($"Found {count} recording devices");

            var targetI = -1;

            for (var i = 0; i < count; i++)
            {
                var d = Bass.RecordGetDeviceInfo(i);
                Logger.Log($"\tDevice {i}: {d.Name}");
                if (d.Name == Device)
                    targetI = i;
            }

            if (targetI == -1)
            {
                Logger.Log($"Failed to find device: {Device}", level: LogLevel.Error);
                return;
            }

            var init = Bass.RecordInit(targetI);
            if (!init)
            {
                Logger.Log($"Failed to initialize device: {Device}", level: LogLevel.Error);
                return;
            }
            // Bass.RecordGetInputName(0); // some devices might have these, not sure what they are. Loopback had 0
            // Bass.RecordGetInput();
            Bass.RecordGetInfo(out var deviceInfo);

            var channels = Math.Min(deviceInfo.Channels, 4);
            if (RuntimeInfo.IsUnix) // channel count doesn't work on Linux, so we have to just set it to 4
                channels = 4;

            if (channels == 0)
            {
                Logger.Log("Channel count must be > 0", level: LogLevel.Error);
                return;
            }

            recording = true;
            var sampleRate = 44100;
            recordingHandle = Bass.RecordStart(sampleRate, channels, BassFlags.Float, RecordProcedure);
            if (recordingHandle == 0)
            {
                Logger.Log($"Failed to start recording: {Bass.LastError}", level: LogLevel.Error);
                return;
            }
            var info = Bass.ChannelGetInfo(recordingHandle);
            AfterInit?.Invoke(new RecorderInitArgs
            {
                BytesPerSample = RecorderInitArgs.GetBytesPerSample(info.Resolution),
                Channels = channels,
                SampleRate = sampleRate,
                Resolution = info.Resolution
            });
        }, false);
    }

    unsafe bool RecordProcedure(int _, IntPtr Buffer, int Length, IntPtr __)
    {
        AudioAvailable?.Invoke(new AudioAvailableArgs { Pointer = Buffer, Length = Length });
        return recording;
    }

    public void Stop()
    {
        Logger.Log("Recording complete");
        Utils.Util.Host.AudioThread.Scheduler.Add(() =>
        {
            if (!recording) return;
            recording = false;
            Bass.ChannelStop(recordingHandle);
            Bass.RecordFree();
        }, false);
    }

    public void Dispose()
    {
        AudioAvailable = null;
        Stop();
    }
}