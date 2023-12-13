using System;
using System.IO;
using NAudio.Wave;
using osu.Framework.Timing;

namespace DrumGame.Game.Media;

public record AsioInitArgs
{
    public int FramesPerBuffer;
    public int Channels;
    public int SampleRate;
}

// could use interface here if we ever start using Wasapi again
public class AsioAudioRecorder : IDisposable
{
    readonly string Device;
    AsioOut asioOut;

    public Action<AsioInitArgs> AfterInit;
    // if set, will disable wave file recording
    public EventHandler<AsioAudioAvailableEventArgs> AudioAvailable;
    public EventHandler<StoppedEventArgs> PlaybackStopped;


    // usually just pass in a TrackClock
    public AsioAudioRecorder(string device)
    {
        Device = device;
    }

    public void StartRecording(string outputDirectory = null)
    {
        Utils.Util.Host.InputThread.Scheduler.Add(() =>
        {
            var time = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            asioOut = new AsioOut(Device);


            var channels = Math.Min(asioOut.DriverInputChannelCount, 4);

            var sampleRate = 44100;

            asioOut.InitRecordAndPlayback(null, channels, sampleRate);

            // TODO figure out best WaveFormat
            // we want whatever the raw TD27 outputs

            if (AfterInit != null)
            {
                AfterInit(new AsioInitArgs
                {
                    FramesPerBuffer = asioOut.FramesPerBuffer,
                    Channels = channels,
                    SampleRate = sampleRate
                });
            }

            var bufferSize = asioOut.FramesPerBuffer * channels;

            var buffer = new float[bufferSize];

            if (AudioAvailable != null)
            {
                asioOut.AudioAvailable += AudioAvailable;
                asioOut.PlaybackStopped += PlaybackStopped;
            }
            else if (outputDirectory != null)
            {
                var outputFilePath = Path.Join(outputDirectory, $"{time}_track.wav");
                var fileWriter = new CustomWaveFileWriter(outputFilePath, new WaveFormat(sampleRate, channels));
                asioOut.AudioAvailable += (_, args) =>
                {
                    var length = args.GetAsInterleavedSamples(buffer);
                    fileWriter.WriteSamples(buffer, 0, length);
                };
                asioOut.PlaybackStopped += (_, a) =>
                {
                    fileWriter.Dispose();
                    fileWriter = null;
                };
            }

            asioOut.Play();
        });
    }

    public void Stop()
    {
        asioOut?.Stop();
        asioOut?.Dispose();
    }

    public void Dispose()
    {
        Stop();
    }
}