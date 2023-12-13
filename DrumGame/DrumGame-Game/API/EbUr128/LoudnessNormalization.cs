using System;
using System.Collections.Generic;
using System.Linq;
using ManagedBass;
using osu.Framework.Audio.Track;

namespace DrumGame.Game.API.EbUr128;

// The bulk of this code is copied from FontanaLuca, originally written for osu!lazer under MIT license
// https://github.com/ppy/osu/blob/54cb433d89362b4d0fa57cff4d8fc5e30cc97188/osu.Game/Audio/EbUr128LoudnessNormalization.cs
// I fixed some bugs in this code and removed features I do not need
// Specifically, it had a thread safety issue that caused it to return different values each time
// I've noticed that this doesn't perfectly match rsgain for some reason.
//   Typically it's off by ~0.02 dB, which at 100 volume, would be ~99.5-100.5 in error

// See also:
//  https://github.com/ppy/osu/issues/1600
//  https://www.un4seen.com/forum/?topic=20129.0
//  https://github.com/ppy/osu/pull/17486

public class LoudnessNormalization
{
    /*
     * references links:
     * ITU-R BS.1770-4, Algorithms to measure audio programme loudness and true-peak audio level (https://www.itu.int/rec/R-REC-BS.1770-4-201510-I)
     * EBU R 128, Loudness normalisation and permitted maximum level of audio signals (https://tech.ebu.ch/publications/e128)
     * EBU TECH 3341, Loudness Metering: ‘EBU Mode’ metering to supplement loudness normalisation in accordance with EBU R 128 (https://tech.ebu.ch/publications/tech3341)
     */

    // absolute silence in ITU_R_BS._1770._4 recommendation (LUFS)
    private const double absolute_silence = -70;

    private ChannelInfo info;

    // number of samples in a 400ms window for 1 channel
    private int totalWindowLength;

    private List<double>[] squaredSegments;

    // list of the squared mean for the overlapping 400ms windows
    private double[,] squaredMeanByChannel;

    // loudness of each 400ms window when all channels are summed
    private List<double> blockLoudness;

    // pre-filter coeffs to model a spherical head
    private double[] headA;
    private double[] headB;

    // high-pass coeffs
    private double[] highPassA;
    private double[] highPassB;

    LoudnessNormalization() { }

    public static double GetLufs(string path) => new LoudnessNormalization().calculateValues(path);

    private double calculateValues(string filePath)
    {
        // load the track and read it's info
        var decodeStream = Bass.CreateStream(filePath, 0, 0, BassFlags.Decode | BassFlags.Float);
        Bass.ChannelGetInfo(decodeStream, out info);

        if (info.Channels == 0) return 0;

        /*
         *  this section allocates the coefficients fot the “K” frequency weighting
         *  requested from the from ITU_R_BS._1770._4 recommendation.
         */

        if (info.Frequency == 48000)
        {
            // filter coeffs for 48000hz sampling rate (from ITU_R_BS._1770._4 guidelines)
            headA = [-1.69065929318241, 0.73248077421585];
            headB = [1.53512485958697, -2.69169618940638, 1.19839281085285];
            highPassA = [-1.99004745483398, 0.99007225036621];
            highPassB = [1.0, -2.0, 1.0];
        }
        else if (info.Frequency == 44100)
        {
            // filter coeffs for 44100hz sampling rate precalculated for speed
            headA = [-1.6636551132560202, 0.7125954280732254];
            headB = [1.5308412300503478, -2.650979995154729, 1.1690790799215869];
            highPassA = [-1.9891696736297957, 0.9891990357870394];
            highPassB = [0.9995600645425144, -1.999120129085029, 0.9995600645425144];
        }
        else throw new Exception($"Unsupported sample rate: {info.Frequency}");

        /*
         * Multi-threaded calculus of the sum of squared samples for each channel after the
         * “K” frequency weighting is applied.
         * Peak amplitude is also found in a naive way by storing the max absolute value
         * of the samples.
         */


        // 100 ms window
        var samplesPerWindow = (int)(info.Frequency * 0.1f * info.Channels);
        var bytesPerWindow = samplesPerWindow * TrackBass.BYTES_PER_SAMPLE;


        totalWindowLength = samplesPerWindow * 4 / info.Channels;
        squaredSegments = new List<double>[info.Channels].Select(item => new List<double>()).ToArray();

        var sampleBuffer = new float[samplesPerWindow];
        int length;
        // read the full track excluding last segment if it does not fill the buffer
        while ((length = Bass.ChannelGetData(decodeStream, sampleBuffer, bytesPerWindow)) == bytesPerWindow)
        {
            for (var i = 0; i < info.Channels; i++)
                squaredSegments[i].Add(segmentSquaredByChannel(i, sampleBuffer));
        }

        Bass.StreamFree(decodeStream);

        /*
         * calculation of the mean square for each channel over 400ms windows overlapping by 75%
         * as per ITU_R_BS._1770._4 recommendation.
         */

        // list of the squared mean for the overlapping 400ms windows
        var squaredMeanLength = squaredSegments[0].Count - 3;
        squaredMeanByChannel = new double[info.Channels, squaredMeanLength];

        if (squaredSegments[0].Count == 0)
            return 0;

        // start from 400ms in since windowedSquaredMean reads previous segments
        for (var i = 3; i < squaredSegments[0].Count; i++)
        {
            for (var j = 0; j < info.Channels; j++)
            {
                squaredMeanByChannel[j, i - 3] =
                    (squaredSegments[j][i - 3]
                    + squaredSegments[j][i - 2]
                    + squaredSegments[j][i - 1]
                    + squaredSegments[j][i]) / totalWindowLength;
            }
        }

        /*
         * Pre gating loudness of each 400ms window in LUFS as per ITU_R_BS._1770._4 recommendation.
         */

        // loudness of each 400ms window when all channels are summed
        blockLoudness = new List<double>();

        if (info.Channels > 3) throw new Exception($"Too many channels: {info.Channels}");

        for (var i = 0; i < squaredMeanLength; i++)
        {
            double tempSum = 0;

            for (var j = 0; j < info.Channels; j++)
                tempSum += squaredMeanByChannel[j, i];

            blockLoudness.Add(-0.691 + 10 * Math.Log10(tempSum));
        }

        /*
         * gated loudness calc as per ITU_R_BS._1770._4 recommendation.
         */

        var relativeGate = Math.Max(gatedLoudness(absolute_silence) - 10, absolute_silence);
        return gatedLoudness(relativeGate);
    }

    // for a window apply pre filters and find it's sum of all samples + update peak amplitude value if needed
    private double segmentSquaredByChannel(int channel, float[] data)
    {
        // Variables to apply the 1st pre-filter
        double pastX0 = 0;
        double pastX1 = 0;

        double pastZ0 = 0;
        double pastZ1 = 0;

        // Variables for the high-pass filter
        double pastZlow0 = 0;
        double pastZlow1 = 0;

        double pastY0 = 0;
        double pastY1 = 0;

        double partialSample = 0;

        for (var s = channel; s < data.Length; s += info.Channels)
        {
            /*
             * “K” frequency weighting for each sample
             */

            // apply the 1st pre-filter to the sample
            var yuleSample = headB[0] * data[s] + headB[1] * pastX0 + headB[2] * pastX1 - headA[0] * pastZ0 - headA[1] * pastZ1;

            pastX1 = pastX0;
            pastZ1 = pastZ0;
            pastX0 = data[s];
            pastZ0 = yuleSample;

            // apply the high-pass filter to the sample
            var tempsample = highPassB[0] * yuleSample + highPassB[1] * pastZlow0 + highPassB[2] * pastZlow1 - highPassA[0] * pastY0 - highPassA[1] * pastY1;

            pastZlow1 = pastZlow0;
            pastY1 = pastY0;
            pastZlow0 = yuleSample;
            pastY0 = tempsample;

            partialSample += tempsample * tempsample;
        }
        return partialSample;
    }

    private double gatedLoudness(double gate)
    {
        double totalLoudness = 0;
        var aboveGatesSegments = 0;

        // removal of segments below the gate threshold
        for (var i = 0; i < blockLoudness.Count; i++)
        {
            if (blockLoudness[i] > gate)
            {
                for (var j = 0; j < info.Channels; j++)
                    totalLoudness += squaredMeanByChannel[j, i];

                aboveGatesSegments++;
            }
        }

        return -0.691 + 10 * Math.Log10(totalLoudness / aboveGatesSegments);
    }
}