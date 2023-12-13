using System;
using System.IO;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Media;
using ManagedBass;
using osu.Framework.Allocation;
using osu.Framework.Audio.Callbacks;

namespace DrumGame.Game.Beatmaps.Editor.Timing;

[LongRunningLoad]
public class VolumePlot : WaveformPlot
{
    float MaxVolume = 0;
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

        (data.Data, MaxVolume) = new AudioDump(decodeStream, info, e => Progress.Current.Value = e / 2)
            .VolumeTransform(WindowWidth, e => Progress.Current.Value = e / 2 + 0.5);

        Bass.StreamFree(decodeStream);

        data.ScalingFactor = 1 / MaxVolume;


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
                if (Editor.UseYouTubeOffset)
                {
                    var newOffset = Math.Round(Beatmap.YouTubeOffset + diff, 2);
                    var change = newOffset - Beatmap.YouTubeOffset;
                    var oldOffset = Beatmap.YouTubeOffset;
                    Editor.PushChange(new BeatmapChange(() =>
                    {
                        Beatmap.YouTubeOffset = newOffset;
                        Beatmap.FireOffsetUpdated();
                    }, () =>
                    {
                        Beatmap.YouTubeOffset = oldOffset;
                        Beatmap.FireOffsetUpdated();
                    }, $"set YouTube offset to {newOffset}"));
                    Track.Seek(Track.AbsoluteTime + change, true);
                }
                else
                {
                    var newOffset = Math.Round(Beatmap.StartOffset + diff, 2);
                    var change = newOffset - Beatmap.StartOffset;
                    Editor.PushChange(new OffsetBeatmapChange(Editor, newOffset));
                    Track.Seek(Track.AbsoluteTime + change, true);
                }
            }
        };

        data.Offset = WindowWidth * 1000;

        return data;
    }
}