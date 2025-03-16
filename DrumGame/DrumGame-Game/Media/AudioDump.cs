using System;
using System.IO;
using ManagedBass;
using NAudio.Wave;
using osu.Framework.Audio.Track;
using WaveFileWriter = NAudio.Wave.WaveFileWriter;
using WaveFormat = NAudio.Wave.WaveFormat;

namespace DrumGame.Game.Media;


public class AudioDump
{
    public float[,] SampleBuffer;
    public int SampleCount; // per channel
    public int ChannelCount;
    public int SampleRate;

    // Make sure to seek stream to 0 if needed afterwards
    public AudioDump(int decodeStream, ChannelInfo? channelInfo = null, Action<double> progress = null)
    {
        var info = channelInfo ?? Bass.ChannelGetInfo(decodeStream);
        var channels = info.Channels;
        // for some reason this is 8x the number of samples
        // we multiply by to add 10% to be safe
        var length = (int)(Bass.ChannelGetLength(decodeStream) / (4 * channels) * 1.1);

        SampleRate = info.Frequency;

        const int samplesPerIteration = 400_000;
        const int bytesPerIteration = 4 * samplesPerIteration;

        float[] sampleBuffer = new float[samplesPerIteration];

        var o = new float[channels, length];

        var position = 0;

        while (true)
        {
            // we don't clamp this data, it may be better if we did
            // I found one .ogg that had values in the range of +-15, clearly in error, and it caused some minor issues
            var bytesRead = Bass.ChannelGetData(decodeStream, sampleBuffer, bytesPerIteration);
            if (bytesRead <= 0) break;

            var samplesRead = (int)(bytesRead / 4);

            for (int i = 0; i < samplesRead; i += channels) // sample channels are interleaved L,R,L,R etc
            {
                for (var j = 0; j < channels; j++)
                    o[j, position] = sampleBuffer[i + j];
                position += 1;
            }
            progress?.Invoke((double)position / length);
        }

        SampleBuffer = o;
        SampleCount = position;
        ChannelCount = channels;

        // Bass.ChannelSetPosition(decodeStream, 0); // expected to be done outside this method
    }



    public (float[], float) VolumeTransform(double windowWidth, Action<double> progress) // recommend ~1000 output sample rate
    {
        var sampleCount = SampleCount;
        if (sampleCount <= 0) return ([], 1);
        var samples = SampleBuffer;
        var sampleWidth = (int)(SampleRate * windowWidth);
        var max = 0f;

        // volume formula is just sqrt sum of squares
        // we can actually just calculate the volume at every single sample then worry about downsampling later
        // it is more efficient this way
        var diffSquares = new double[sampleCount - 1];
        if (ChannelCount == 1)
        {
            for (var i = 0; i < sampleCount - 1; i++)
            {
                var d = samples[0, i + 1] - samples[0, i];
                diffSquares[i] = d * d;
            }
        }
        else
        {
            for (var i = 0; i < sampleCount - 1; i++)
            {
                var d = samples[0, i + 1] - samples[0, i];
                var d2 = samples[1, i + 1] - samples[1, i];
                diffSquares[i] = d * d + d2 * d2;
            }
        }
        progress(0.33);
        // this only includes the sum of diffs at/before i
        // it is not symmetric
        var volumeSquares = new double[sampleCount - 1];
        var v = 0.0;
        for (var i = 0; i < sampleCount - 1; i++)
        {
            // if we are past the initial window width, we can start subtracting
            // since these are floating point, we accumulate error over the entire track
            // this isn't a problem since values are compared relatively
            if (i >= sampleWidth) v = Math.Max(0, v - diffSquares[i - sampleWidth]);
            v += diffSquares[i];
            volumeSquares[i] = v;
        }
        progress(0.66);
        var res = new float[sampleCount - 1];
        for (var i = 0; i < sampleCount - 1; i++)
        {
            res[i] = (float)Math.Sqrt(volumeSquares[i]);
            max = Math.Max(res[i], max);
        }
        return (res, max);
    }

    public void WriteToFile(string path)
    {
        using var stream = File.OpenWrite(path);
        using var writer = new WaveFileWriter(stream, new WaveFormat(SampleRate, ChannelCount));
        for (var i = 0; i < SampleCount; i++)
            for (var j = 0; j < ChannelCount; j++)
                writer.WriteSample(SampleBuffer[j, i]);
    }
}