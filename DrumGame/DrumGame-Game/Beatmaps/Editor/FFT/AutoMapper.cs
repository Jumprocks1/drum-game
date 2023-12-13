using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Channels;

namespace DrumGame.Game.Beatmaps.Editor.Timing;

public class AutoMapper
{
    readonly BeatmapEditor Editor;
    Beatmap Beatmap => Editor.Beatmap;
    readonly FFTProvider FFT;


    public AutoMapper(BeatmapEditor editor, AutoMapperSettings settings)
    {
        Editor = editor;
        Settings = settings;
        FFT = Editor.GetFFT();
    }
    public class AutoMapperSettings
    {
        public double BeatSnap;
    }
    readonly AutoMapperSettings Settings;
    public AffectedRange Run(BeatSelection selection = null)
    {
        var added = new HashSet<(double, DrumChannel)>();

        var triggerSettings = FFT.LatestSettings.Triggers;
        var n = triggerSettings.Count;

        var minPre = new float[n];
        Array.Fill(minPre, float.MaxValue);
        var minAfter = new float[n];
        Array.Fill(minAfter, float.MaxValue);
        var max = new float[n];
        var maxLocation = new int[n];

        var biggestCorrection = triggerSettings.Max(e => Math.Abs(e.TimeCorrectionMs));
        var extraChunks = (int)Math.Ceiling(biggestCorrection / FFT.ChunkWidthMs);

        int startI = 0;
        int endI = FFT.ChunkCount;

        if (selection != null && selection.IsComplete)
        {
            startI = Math.Clamp(FFT.ChunkAt(Beatmap.MillisecondsFromBeat(selection.Left)) - extraChunks, 0, FFT.ChunkCount);
            endI = Math.Clamp(FFT.ChunkAt(Beatmap.MillisecondsFromBeat(selection.Right)) + 1 + extraChunks, 0, FFT.ChunkCount);
        }



        for (var i = startI; i < endI; i++)
        {
            var bins = FFT.FFTAtChunk(i);
            for (var c = 0; c < triggerSettings.Count; c++)
            {
                var v = triggerSettings[c];
                var endBin = v.HighBin;
                var minimumRise = v.Climb;
                var channel = v.Channel;
                var thresh = v.MinimumThreshold;

                var sum = 0f;
                for (var j = v.LowBin; j <= endBin; j++)
                    sum += bins[j];
                sum *= v.Multiplier;

                if (sum < minAfter[c])
                    minAfter[c] = sum;
                if (sum > max[c] || (max[c] - minPre[c] <= minimumRise))
                {
                    maxLocation[c] = i;
                    if (minAfter[c] < minPre[c])
                        minPre[c] = minAfter[c];
                    minAfter[c] = max[c] = sum;
                }
                if (max[c] > thresh && max[c] - minAfter[c] > minimumRise && max[c] - minPre[c] > minimumRise)
                {
                    var ms = FFT.ChunkToMs(maxLocation[c]) + triggerSettings[c].TimeCorrectionMs;
                    var beat = Beatmap.RoundBeat(Beatmap.BeatFromMilliseconds(ms), Settings.BeatSnap);
                    var key = (beat, channel);
                    if (added.Add(key))
                    {
                        if (selection == null || selection.Contains(beat))
                            Beatmap.AddHit(Beatmap.TickFromBeat(beat), new HitObjectData(channel), false);
                    }
                    minPre[c] = minAfter[c] = max[c] = sum;
                    maxLocation[c] = i;
                }
            }
        }
        return AffectedRange.FromSelectionOrEverything(selection, Beatmap);
    }
}