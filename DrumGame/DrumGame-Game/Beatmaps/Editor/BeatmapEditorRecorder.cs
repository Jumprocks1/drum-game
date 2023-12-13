
using System;
using DrumGame.Game.Commands;
using DrumGame.Game.Input;
using DrumGame.Game.Midi;
using DrumGame.Game.Stores;
using osu.Framework.Allocation;


namespace DrumGame.Game.Beatmaps.Editor;

public partial class BeatmapEditor
{
    DrumChannelInputHandler RecordHandler;

    bool Recording
    {
        get => RecordHandler != null; set
        {
            if (value == Recording) return;
            if (value)
            {
                RecordHandler = new DrumChannelInputHandler(ev =>
                {
                    var beat = Math.Max(0, RoundBeat(Beatmap.BeatFromMilliseconds(ev.Time)));
                    var tick = Beatmap.TickFromBeat(beat);
                    var change = new NoteBeatmapChange(Display, () =>
                    {
                        if (Beatmap.AddHit(tick, new HitObjectData(ev.Channel), false))
                        {
                            return new AffectedRange(tick);
                        }
                        else
                        {
                            return false;
                        }
                    }, "recording");
                    MergeChangeIf(change, e => e.Description == change.Description);
                }, Track)
                {
                    ConsumeInputs = false
                };
            }
            else
            {
                RecordHandler.Dispose();
                RecordHandler = null;
            }
        }
    }

    bool OnMidiNote(MidiNoteOnEvent e) => RecordHandler?.OnMidiNote(e) ?? false;

    [CommandHandler]
    public void ToggleRecordMode() => Mode = Mode == BeatmapPlayerMode.Record ?
        BeatmapPlayerMode.Edit : BeatmapPlayerMode.Record;
}
