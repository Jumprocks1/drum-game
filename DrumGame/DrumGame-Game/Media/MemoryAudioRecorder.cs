using System;
using System.IO;
using DrumGame.Game.Utils;
using NAudio.Wave;
using osu.Framework.Logging;

namespace DrumGame.Game.Media;

public class MemoryAudioRecorder : IDisposable
{
    IAudioRecorder InnerRecorder;
    public DateTime StartTime { get; private set; }
    public readonly string Device;

    public readonly TimeSpan Length;

    byte[] Buffer;
    int bufferPosition = 0; // current circular buffer position
    int bufferLength = 0;
    int framesRead = 0;
    int BufferFrameCount;

    public RecorderInitArgs InitInfo;

    public float this[int frame, int channel]
    {
        get
        {
            var offset = (frame - framesRead) * InitInfo.BytesPerFrame + channel * InitInfo.BytesPerSample;
            var index = bufferPosition + offset;
            if (index < 0) index += Buffer.Length;
            return BitConverter.ToSingle(Buffer, index);
        }
    }

    object Lock = new();

    public bool SavingToFile = false;

    public MemoryAudioRecorder(string device, IAudioRecorder recorder, TimeSpan? bufferTime = null)
    {
        Length = bufferTime ?? TimeSpan.FromMinutes(10);
        Device = device;
        recorder.Device = device;
        InnerRecorder = recorder;
        InnerRecorder.AfterInit = args =>
        {
            lock (Lock)
            {
                InitInfo = args;
                // a frame is a 1 sample per channel
                BufferFrameCount = (int)Math.Ceiling(InitInfo.SampleRate * Length.TotalSeconds);
                // we have to be a multiple of the audio available buffer length
                Buffer = new byte[BufferFrameCount * args.BytesPerFrame];
                var bytes = Buffer.Length * sizeof(byte);
                Logger.Log($"Created {bytes / 1048576d:0.##} MB audio buffer", level: LogLevel.Important);
            }
        };
        InnerRecorder.AudioAvailable = args =>
        {
            lock (Lock)
            {
                if (SavingToFile || Buffer == null) return; // Lock when saving. Buffer is null when we are disposed.
                                                            // technically SavingToFile might get set right here, but it shouldn't be a major problem

                if (bufferPosition + args.Length > Buffer.Length) // can't do it all in one copy
                {
                    var size1 = Buffer.Length - bufferPosition;
                    args.Buffer[..size1].CopyTo(Buffer.AsSpan(bufferPosition, size1));
                    var size2 = args.Length - size1; // remaining bytes
                    args.Buffer.Slice(size1, size2).CopyTo(Buffer.AsSpan(0, size2));
                    bufferPosition = size2;
                    bufferLength = Buffer.Length; // make sure we know that we are using the entire array now
                }
                else
                {
                    args.Buffer.CopyTo(Buffer.AsSpan(bufferPosition, args.Length));
                    bufferPosition += args.Length;
                    if (bufferLength < bufferPosition) bufferLength = bufferPosition;
                    if (bufferPosition == Buffer.Length) bufferPosition = 0; // this should very rarely happen
                }
                framesRead += args.Length / InitInfo.BytesPerFrame;
            }
        };
    }

    public void Start()
    {
        StartTime = DateTime.UtcNow;
        InnerRecorder.StartRecording();
    }

    public float[] GetMonoSamples(int frameStart, int frameCount)
    {
        lock (Lock)
        {
            var frameEnd = frameStart + frameCount;
            if (framesRead < frameEnd) return null; // not available yet
            if (frameStart < framesRead - BufferFrameCount) return null; // no longer available
            var res = new float[frameCount];
            for (var i = 0; i < frameCount; i++)
            {
                for (var j = 0; j < InitInfo.Channels; j++)
                    res[i] += this[i + frameStart, j];
            }
            return res;
        }
    }

    public void WriteToFile(DateTime startTime, string directory)
    {
        lock (Lock)
        {
            SavingToFile = true; // stop updating the buffer

            // we could run this on a different thread to make sure we don't mess up the recorder


            var endTime = DateTime.Now;
            if (startTime < StartTime) startTime = StartTime;
            var age = endTime.ToUniversalTime() - startTime;
            var framesRequested = (int)Math.Ceiling(InitInfo.SampleRate * age.TotalSeconds);

            var bytesRequested = framesRequested * InitInfo.BytesPerFrame;

            // can't request data we don't have
            if (bytesRequested > bufferLength) bytesRequested = bufferLength;


            var path = Path.Join(directory, $"{startTime.ToLocalTime():yyyy-MM-dd_HH-mm-ss}_{endTime:HH-mm-ss}.wav");
            using var writer = new CustomWaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(InitInfo.SampleRate, InitInfo.Channels));

            var startPosition = bufferPosition - bytesRequested;
            if (startPosition < 0) startPosition += Buffer.Length;

            var firstWrite = Math.Min(bufferLength - startPosition, bytesRequested);
            Logger.Log($"writing bytes: {startPosition} length {firstWrite}", level: LogLevel.Important);
            writer.Write(Buffer, startPosition, firstWrite);
            var remainingWrites = bytesRequested - firstWrite;
            if (remainingWrites > 0)
            {
                startPosition += firstWrite;
                if (startPosition >= Buffer.Length) startPosition -= Buffer.Length;
                Logger.Log($"writing bytes: {startPosition} length {remainingWrites}", level: LogLevel.Important);
                writer.Write(Buffer, startPosition, remainingWrites);
            }

            SavingToFile = false;
        }
    }

    public void Dispose()
    {
        lock (Lock)
        {
            InnerRecorder?.Dispose();
            InnerRecorder = null;
            Buffer = null;
        }
    }
}