using System;
using System.IO;
using System.Linq;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Media;
using ManagedBass;
using osu.Framework.Allocation;
using osu.Framework.Audio.Callbacks;

namespace DrumGame.Game.Beatmaps.Editor.Timing;

[LongRunningLoad]
public class VolumePlot : WaveformPlot
{
    readonly double WindowWidth; // in seconds
    public VolumePlot(BeatmapEditor editor, double windowWidth, int sampleCount) : base(editor, sampleCount)
    {
        WindowWidth = windowWidth;
    }

    protected override void Update()
    {
        base.Update(); // we have to update children first so we get an updated loadedViewSample
        Plot?.UpdateTooltip();
    }

    (Tempo, TempoChange, double) GetNewTempo(int i, double diff)
    {
        // we subtract 1 tick since we don't want the tempo change to be exactly on our current beat
        var tempoChange = Beatmap.RecentChangeTicks<TempoChange>(Beatmap.TickFromBeat(TargetBeat) - 1);
        var tempoChangeBeat = Beatmap.BeatFromTick(tempoChange.Time);
        var distance = TargetBeat - tempoChangeBeat;

        // careful since we might divide by 0
        if (distance == 0) return (tempoChange.Tempo, tempoChange, tempoChangeBeat);

        var goalMsPerBeat = tempoChange.MsPerBeat + diff / distance;
        return (new Tempo { BPM = 60_000 / goalMsPerBeat }, tempoChange, tempoChangeBeat);
    }

    public double ComputeBestOffset()
    {
        // TODO we should be able to bin all tempo changes across the song
        // not sure how yet though
        // I think you take the slowest tempo and use that for the offsetoptions width
        // then you recenter the bins so they are based on the current offset
        var tempo = Beatmap.TempoChanges.FirstOrDefault() ?? TempoChange.Default;
        var bpm = tempo.BPM;
        var samplesPerBeat = 60 * Data.DataSampleRate / bpm;
        // couldn't find a way of doing this without binning each sample
        // note that the number of bins isn't really an integer, so we can't do a simple mod to distribute data across the bins
        var offsetOptions = new double[(int)samplesPerBeat];
        var counts = new double[(int)samplesPerBeat];
        for (var j = 0; j < Data.Data.Length; j++)
        {
            var value = Data.Data[j] / Data.Peak;
            // note, this bin is fractional
            var beat = j / samplesPerBeat;
            var targetBin = beat % 1 * offsetOptions.Length;
            // TODO instead of rounding, we should anti-alias and split the value across 2 bins
            offsetOptions[(int)targetBin] += value;
            counts[(int)targetBin] += 1;
        }
        var bestIndex = -1;
        var best = 0.0;
        for (var j = 0; j < offsetOptions.Length; j++)
        {
            if (counts[j] == 0) continue;
            var v = offsetOptions[j] / counts[j];
            if (v > best)
            {
                best = v;
                bestIndex = j;
            }
        }
        // this is the first beat peak relative to the start of the audio track
        var offset = (double)bestIndex / offsetOptions.Length * samplesPerBeat / Data.DataSampleRate * 1000
             - WindowWidth * 1000;
        // typically offset should be the first beat in the first measure though, so this could be += 0.5/1/2/3/4 beats or so
        // we should add as many half beats as needed to get as close as possible to the current offset
        // we round to half a beat since some songs are mapped in half time to keep the BPM ~120-200
        offset += Math.Round((Beatmap.CurrentTrackStartOffset - offset) / tempo.MsPerBeat * 2) * tempo.MsPerBeat / 2;
        // var changeMs = offset - Beatmap.StartOffset;
        // Console.WriteLine($"offset: {offset:0.0} change: {changeMs:0.0}ms ({changeMs / tempo.MsPerBeat:0.00}beats)");
        return offset;
    }

    protected override WaveformData LoadData()
    {
        var data = new WaveformData();


        using var audioStream = File.OpenRead(Editor.CurrentAudioPath);

        var fileCallbacks = new FileCallbacks(new DataStreamFileProcedures(audioStream));
        var decodeStream = Bass.CreateStream(StreamSystem.NoBuffer, BassFlags.Decode | BassFlags.Float, fileCallbacks.Callbacks, fileCallbacks.Handle);
        Bass.ChannelGetInfo(decodeStream, out ChannelInfo info);
        var length = Bass.ChannelGetLength(decodeStream);

        data.DataSampleRate = info.Frequency;
        data.ViewSampleRate = 1000;

        (data.Data, data.Peak) = new AudioDump(decodeStream, info, e => Progress.Progress = e / 2)
            .VolumeTransform(WindowWidth, e => Progress.Progress = e / 2 + 0.5);

        Bass.StreamFree(decodeStream);

        data.ScalingFactor = 1 / data.Peak;


        Plot.SampleTooltip = (i, _) =>
        {
            var targetTime = Beatmap.MillisecondsFromBeat(TargetBeat);
            var cursorTime = (loadedViewSample + i) / data.ViewSampleRate * 1000 - WindowWidth * 1000;

            var cursorBeat = Beatmap.BeatFromMilliseconds(cursorTime);
            var beatDiff = cursorBeat - TargetBeat;

            var diff = cursorTime - targetTime;
            var (newTempo, _, timingChangeBeat) = GetNewTempo(i, diff);

            return $"{diff:+0.0;-0.0;0.0}ms <faded>(click to apply)</c>\n{beatDiff:+0.00;-0.00;0.00} beats"
            + $"\n{newTempo.BPM:0.00} BPM at beat {timingChangeBeat} <faded>(Ctrl+click to apply)</c>";
        };
        Plot.Clicked = (i, e) =>
        {
            var targetTime = Beatmap.MillisecondsFromBeat(TargetBeat);
            var time = (loadedViewSample + i) / data.ViewSampleRate * 1000 - WindowWidth * 1000;
            var diff = time - targetTime;
            if (e.ControlPressed)
            {
                var (newTempo, tempoChange, tempoChangeBeat) = GetNewTempo(i, diff);
                var description = $"set bpm at {tempoChangeBeat} to {newTempo.HumanBPM}";

                Editor.PushChange(new TempoBeatmapChange(Beatmap, () =>
                {
                    Beatmap.UpdateChangePoint<TempoChange>(tempoChange.Time, e => e.WithTempo(newTempo));
                    Beatmap.RemoveExtras<TempoChange>();
                }, description));
                Track.Seek(Track.AbsoluteTime + diff, true);
            }
            else
            {
                var newOffset = Math.Round(Beatmap.CurrentTrackStartOffset + diff, 2);
                var change = newOffset - Beatmap.CurrentTrackStartOffset;
                Editor.PushChange(new OffsetBeatmapChange(Editor, newOffset, Editor.UseYouTubeOffset));
                Track.Seek(Track.AbsoluteTime + change, true);
            }
        };

        data.Offset = WindowWidth * 1000;

        return data;
    }
}