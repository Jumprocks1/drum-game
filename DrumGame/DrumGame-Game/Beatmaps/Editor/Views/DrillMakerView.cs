using System;
using System.Collections.Generic;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Commands;
using DrumGame.Game.Modals;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Beatmaps.Editor.Views;


public static class DrillMakerView
{
    public class DrillMakerConfig
    {
        public double DurationSeconds = 30;
        public double DurationBeats = 0;
        public double MaxBpm = 200;
        public double BeatGap = 4;
        public double TempoRampFlat = 10;
        public double TempoRampPercent = 0;
    }
    public static void Open(BeatmapEditor editor)
    {
        var buffer = editor.NewCopyBufferFromSelection();
        if (buffer.hitObjects.Count == 0) return;
        var beatmap = editor.Beatmap;
        var (from, to) = editor.GetCurrentRange();
        var length = to - from;
        if (length <= 0) return;
        var beatLength = beatmap.BeatFromTick(length);


        var config = new DrillMakerConfig();
        Util.Palette.Request(new RequestConfig()
        {
            Title = "Drill Maker",
            Fields = new FieldBuilder()
                .AddClass(config)
                .Build(),
            OnCommit = request =>
            {
                request.ToObject(config);
                using var _ = editor.UseCompositeChange("make drill");
                var tempoChanges = new List<TempoChange>();
                editor.PushChange(new NoteBeatmapChange(() =>
                {
                    beatmap.HitObjects.RemoveAll(e => e.Time >= from);
                    var currentBpm = beatmap.ChangeAt<TempoChange>(beatmap.BeatFromTick(from)).BPM;
                    var position = from;
                    var first = true;
                    while (true)
                    {
                        // This can cause the last round to go from 199 => 200bpm
                        // Oh well
                        var roundedBpm = Math.Min(Math.Round(currentBpm), config.MaxBpm);
                        tempoChanges.Add(new(position, new()
                        {
                            MicrosecondsPerQuarterNote = Tempo.MicrosecondsFromBPM(roundedBpm)
                        }));
                        if (!first) position += beatmap.TickFromBeat(config.BeatGap);
                        var minDurationBeats = config.DurationBeats + config.DurationSeconds * currentBpm / 60;
                        var loopCount = Math.Ceiling(minDurationBeats / beatLength);
                        for (var loop = 0; loop < loopCount; loop++)
                        {
                            editor.WriteBufferAt(buffer, position);
                            position += length;
                        }

                        if (roundedBpm >= config.MaxBpm)
                            break;

                        var nextBpm = currentBpm * (1 + config.TempoRampPercent / 100) + config.TempoRampFlat;
                        if (nextBpm <= currentBpm) break;
                        currentBpm = nextBpm;
                        first = false;
                    }
                }, null));
                editor.PushChange(new TempoBeatmapChange(beatmap, () =>
                {
                    beatmap.TempoChanges.RemoveAll(e => e.Time >= from);
                    foreach (var tempo in tempoChanges)
                        beatmap.UpdateChangePoint<TempoChange>(tempo.Time, e => e.WithTempo(tempo.Tempo));
                    beatmap.RemoveExtras<TempoChange>();
                }, null));
            }
        });
    }
}