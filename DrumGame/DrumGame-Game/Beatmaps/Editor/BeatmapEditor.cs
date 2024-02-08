using System;
using System.Collections.Generic;
using System.IO;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Beatmaps.Editor.Timing;
using DrumGame.Game.Channels;
using DrumGame.Game.Commands;
using DrumGame.Game.Commands.Requests;
using DrumGame.Game.Components;
using DrumGame.Game.Midi;
using DrumGame.Game.Modals;
using DrumGame.Game.Modifiers;
using DrumGame.Game.Utils;
using ManagedBass;
using osu.Framework.Allocation;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Beatmaps.Editor;

public enum PasteTarget
{
    RoundBeat, // Basically aligns to Tick
    Beat,
    Measure,
}
public class CopyBuffer
{
    public List<HitObject> hitObjects = new();
    public PasteTarget Target;
}
public partial class BeatmapEditor : BeatmapPlayer
{
    public override BeatmapPlayerMode Mode
    {
        get => base.Mode; set
        {
            base.Mode = value;
            var edit = value.HasFlagFast(BeatmapPlayerMode.Edit);
            var record = value == BeatmapPlayerMode.Record;
            var measureLines = edit && !record || Util.Skin.Notation.MeasureLines;

            Display.SnapIndicator = measureLines;
            Display.SongCursorVisible = !measureLines;
            if (Display.MeasureLines == null == measureLines) Display.ToggleMeasureLines();

            if (value == BeatmapPlayerMode.Fill)
            {
                Track.Stop();
                Display.DragStart.Running = false; // prevent restart on drag release
                Track.Seek(Beatmap.MillisecondsFromBeat(SnapTarget));
            }
            Recording = record;
        }
    }
    public new readonly MusicNotationBeatmapDisplay Display;
    public BeatmapEditor(Beatmap beatmap, MusicNotationBeatmapDisplay display, BeatmapOpenMode openMode,
        List<BeatmapModifier> mods) : base(beatmap, display, openMode, mods)
    {
        this.Display = display;
        Mode = BeatmapPlayerMode.Edit;
    }
    double? _beatSnap = 4;
    public double? BeatSnap
    {
        get => _beatSnap; set
        {
            if (_beatSnap == value) return;
            _beatSnap = value;
            Display?.BeatSnapChanged();
        }
    }
    public CopyBuffer CopyBuffer;
    public double SnapTargetClamp() => Math.Max(0, SnapTarget);
    public double SnapTarget => SnapBeat(Track.CurrentBeat);
    public double SnapBeat(double beat)
    {
        var offset = Beatmap.BeatFromMeasure(Beatmap.MeasureFromBeat(beat));
        return FloorBeat(beat - offset) + offset;
    }
    double FloorBeat(double beat) => BeatSnap.HasValue ? Beatmap.FloorBeat(beat, BeatSnap.Value) : beat;
    public double RoundBeat(double beat) => BeatSnap.HasValue ? Beatmap.RoundBeat(beat, BeatSnap.Value) : beat;
    double? fillBuffer;
    protected override void OnKeyUp(KeyUpEvent e)
    {
        if (fillBuffer.HasValue && BeatSnap.HasValue)
        {
            Track.Seek(Beatmap.MillisecondsFromBeat(fillBuffer.Value + 1 / BeatSnap.Value));
            fillBuffer = null;
        }
        base.OnKeyUp(e);
    }

    bool ChannelPressed(DrumChannel channel, bool repeat = false)
    {
        if (Editing && !Recording && channel != DrumChannel.None && channel != DrumChannel.Metronome)
        {
            var data = new HitObjectData(channel);
            if (Display.Selection != null && Display.Selection.IsComplete)
            {
                var s = Display.Selection.Clone(); // have to clone so that redo works
                var desc = $"toggle {channel} notes {s.RangeString}";
                var stride = TickStride;
                PushChange(new NoteBeatmapChange(Display, () => Beatmap.AddHits(s, stride, data, true), desc, s));
            }
            else
            {
                var target = SnapTarget;
                var tickTarget = Beatmap.TickFromBeat(target);
                if (Mode == BeatmapPlayerMode.Fill && repeat)
                {
                    if (fillBuffer.HasValue && BeatSnap.HasValue)
                    {
                        target = fillBuffer.Value + 1 / BeatSnap.Value;
                        Track.Seek(Beatmap.MillisecondsFromBeat(target));
                    }
                }
                var added = false;
                var desc = $"toggle {channel} note at {target}";
                PushChange(new NoteBeatmapChange(Display, () =>
                {
                    added = Beatmap.AddHit(tickTarget, data);
                    return new AffectedRange(tickTarget);
                }, desc, target));
                if (Mode == BeatmapPlayerMode.Fill)
                {
                    Track.Stop();
                    Display.DragStart.Running = false; // prevent restart on drag release
                    if (!added) fillBuffer = null;
                    else fillBuffer = target;
                }
            }
            return true;
        }
        return false;
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (Editing && !Recording)
        {
            // this lets us also trigger note insertion even if we're holding Ctrl/Alt/Shift key
            var keyCombo = new KeyCombo(e);
            if (Command.KeyBindings.TryGetValue(keyCombo, out var commands)
                || Command.KeyBindings.TryGetValue(KeyCombination.FromKey(e.Key), out commands))
            {
                for (var i = commands.Count - 1; i >= 0; i--) // iterate backwards so commands registered last are prioritized
                {
                    if (commands[i].Command == Commands.Command.ToggleDrumChannel)
                    {
                        Util.KeyPressOverlay?.Handle(commands[i].Name, keyCombo);
                        ChannelPressed((DrumChannel)commands[i].Parameter);
                        return true;
                    }
                }
            }
        }
        return base.OnKeyDown(e);
    }

    [CommandHandler]
    public bool ToggleDrumChannel(CommandContext context)
    {
        if (Editing && !Recording)
        {
            if (context.TryGetParameter<DrumChannel>(out var channel))
            {
                ChannelPressed(channel);
                return true;
            }
            var req = context.GetItem<DrumChannel>(e => ChannelPressed(e), "Toggling Drum Channel");
            req.initialFocus = null; // don't focus search since we would prefer just clicking the buttons
            req.AddCommandButtons(Commands.Command.ToggleDrumChannel, e => e.Parameter.ToString());
            return req;
        }
        return false;
    }

    FFTProvider _fft; // get with GetFFT() - understand that this is very expensive
    public FFTProvider GetFFT()
    {
        if (_fft == null)
        {
            var path = Util.Resources.TryFind(Beatmap.DrumOnlyAudio) ?? CurrentAudioPath;
            if (!File.Exists(path)) return null;
            _fft = FFTProvider.FromSettingsFile(Util.Resources.GetAbsolutePath("auto-mapper.json"), path);
            _fft.CacheAll();
        }
        return _fft;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        Command.RegisterHandlers(this);
        DrumMidiHandler.AddNoteHandler(OnMidiNote, true);
    }
    protected override void Dispose(bool isDisposing)
    {
        _fft?.Dispose();
        _fft = null;
        Command.RemoveHandlers(this);
        DrumMidiHandler.RemoveNoteHandler(OnMidiNote, true);
        if (offsetWizard != null)
        {
            // we can't close because we aren't always allowed to mutate children
            // not sure how to safely remove it, probably need to schedule the closing
            // offsetWizard.Close();
            offsetWizard.Dispose();
            offsetWizard = null;
        }
        base.Dispose(isDisposing);
    }
    [CommandHandler]
    public void ToggleSnapIndicator()
    {
        Display.SnapIndicator = !Display.SnapIndicator;
        Display.SongCursorVisible = !Display.SnapIndicator;
    }
    [CommandHandler]
    public void Copy()
    {
        var (from, to) = GetCurrentRange();
        CopyBuffer = new CopyBuffer { Target = PasteTarget.Measure };
        foreach (var hit in Beatmap.HitObjects)
        {
            if (hit.Time >= to) break;
            if (hit.Time >= from)
            {
                CopyBuffer.hitObjects.Add(hit.WithTime(hit.Time - from));
            }
        }
    }
    [CommandHandler]
    public void InsertRoll()
    {
        if (Display.Selection != null && Display.Selection.HasVolume)
        {
            var s = Display.Selection.Clone();
            foreach (var i in Beatmap.GetHitObjectsAt(s.Left))
            {
                var data = Beatmap.HitObjects[i].Data;
                var desc = $"add {data.Channel} roll {s.RangeString}";
                PushChange(new NoteBeatmapChange(Display, () => Beatmap.AddRoll(s, data), desc, s));
            }
        }
    }
    [CommandHandler]
    public void Paste()
    {
        if (CopyBuffer == null || CopyBuffer.hitObjects.Count == 0) return;
        // TODO look at PasteTarget
        var toBeat = Beatmap.BeatFromMeasure(Track.CurrentMeasure);
        var to = Beatmap.TickFromBeat(toBeat);
        // we have to clone so that if we undo + redo this operation, we have the CopyBuffer stored
        var paste = new List<HitObject>(CopyBuffer.hitObjects);
        var desc = $"paste at beat {toBeat}";
        PushChange(new NoteBeatmapChange(Display, () =>
        {
            var lastTick = to;
            foreach (var hit in paste)
            {
                if (hit is RollHitObject roll)
                {
                    Beatmap.AddRoll(hit.Time + to, roll.Duration, hit.Data);
                }
                else
                {
                    Beatmap.AddHit(hit.Time + to, hit.Data, false);
                }
            }
            return new AffectedRange(to, paste[^1].Time + to + 1);
        }, desc, toBeat));
    }
    [CommandHandler] public void Cut() { Copy(); Delete(); }
    [CommandHandler]
    public void CropMeasure()
    {
        var (from, to) = GetCurrentRange();
        var beat = (double)from / Beatmap.TickRate;
        var noteChange = new NoteBeatmapChange(Display, () =>
        {
            var replace = new List<HitObject>();
            foreach (var hit in Beatmap.HitObjects)
            {
                if (hit.Time < from) replace.Add(hit);
                else if (hit.Time >= to) replace.Add(hit.WithTime(hit.Time - to + from));
            }
            var last = Beatmap.HitObjects.Count > 0 ? Math.Max(from, Beatmap.HitObjects[^1].Time) : from;
            Beatmap.HitObjects = replace;
            return new AffectedRange(from, last + 1);
        }, null, beat);
        var timingChange = new TempoBeatmapChange(Beatmap, () =>
        {
            var replace = new List<TempoChange>();
            foreach (var timing in Beatmap.TempoChanges)
            {
                if (timing.Time < from || timing.Time == 0) replace.Add(timing);
                else if (timing.Time >= to) replace.Add(timing.WithTime(timing.Time - to + from));
            }
            Beatmap.TempoChanges = replace;
            Beatmap.RemoveExtras<TempoChange>();
        }, null);
        PushChange(new CompositeHistoryChange($"crop measure at beat {beat}", noteChange, timingChange));
    }


    void InsertMeasureAt(int measure)
    {
        var beat = Beatmap.BeatFromMeasure(measure);
        var start = Beatmap.TickFromMeasure(measure);
        var gap = Beatmap.TickFromMeasure(measure + 1) - start;
        var noteChange = new NoteBeatmapChange(Display, () =>
        {
            var replace = new List<HitObject>(Beatmap.HitObjects.Capacity);
            foreach (var hit in Beatmap.HitObjects)
            {
                replace.Add(hit.Time < start ? hit : hit.WithTime(hit.Time + gap));
            }
            Beatmap.HitObjects = replace;
            var last = Beatmap.HitObjects.Count > 0 ? Math.Max(start, Beatmap.HitObjects[^1].Time) : start;
            return new AffectedRange(start, last + 1);
        }, null, beat);
        var timingChange = new TempoBeatmapChange(Beatmap, () =>
        {
            var replace = new List<TempoChange>();
            foreach (var timing in Beatmap.TempoChanges)
            {
                replace.Add(timing.Time < start || timing.Time == 0 ? timing : timing.WithTime(timing.Time + gap));
            }
            Beatmap.TempoChanges = replace;
            Beatmap.RemoveExtras<TempoChange>();
        }, null);
        PushChange(new CompositeHistoryChange($"insert measure at beat {beat}", noteChange, timingChange));
    }

    [CommandHandler] public void InsertMeasure() => InsertMeasureAt(Track.CurrentMeasure);
    [CommandHandler]
    public void InsertMeasureAtStart()
    {
        using var _ = UseCompositeChange("insert measure at start");
        InsertMeasureAt(0);
        var measureSize = Beatmap.MillisecondsFromBeat(Beatmap.BeatFromMeasure(1)) - Beatmap.StartOffset;
        var newOffset = Beatmap.StartOffset - measureSize;
        PushChange(new OffsetBeatmapChange(this, newOffset));
    }
    [CommandHandler]
    public void Delete()
    {
        var (from, to) = GetCurrentRange();
        var removeStart = 0;
        var removeCount = 0;
        var i = 0;
        foreach (var hit in Beatmap.HitObjects)
        {
            if (hit.Time >= to) break;
            if (hit.Time >= from)
            {
                if (removeCount == 0) removeStart = i;
                removeCount += 1;
            }
            i += 1;
        }
        if (removeCount >= 0)
        {
            var beat = (double)from / Beatmap.TickRate;
            var desc = $"delete measure at beat {beat}";
            PushChange(new NoteBeatmapChange(Display, () =>
            {
                Beatmap.HitObjects.RemoveRange(removeStart, removeCount);
                return new AffectedRange(from, to);
            }, desc, beat));
        }
    }
    [CommandHandler]
    public bool SetEditorSnapping(CommandContext context)
    {
        if (context.TryGetParameter(out double p))
        {
            BeatSnap = p;
            Display.BeatSnapChanged();
            return true;
        }
        var request = context.Palette.RequestNullableNumber("Setting Editor Snapping", "Beat divisor", BeatSnap,
            o => BeatSnap = o <= 0 ? null : o);
        request.AddCommandButtons(Commands.Command.SetEditorSnapping, c => $"Snap {c.Parameters[0]}");
        return request;
    }
    [CommandHandler]
    public bool SetBeatmapLeadIn(CommandContext context) =>
        context.Palette.RequestNumber("Setting Beatmap Lead-In", "Lead-in", Beatmap.LeadIn,
            o => PushChange(new LeadInBeatmapChange(Beatmap, o)));
    [CommandHandler]
    public bool EditBeatmapMetadata(CommandContext context) => context.Palette.Push(MetadataEditor.Build(Beatmap, this));

    [CommandHandler]
    public bool EditBeatsPerMeasure(CommandContext context)
    {
        var beat = Beatmap.BeatFromMeasure(CurrentMeasure);
        var currentMeasureChange = Beatmap.ChangeAt<MeasureChange>(beat);
        return context.Palette.RequestNumber($"Setting Beats Per Measure at Beat {beat}", "Beats", currentMeasureChange.Beats,
            o =>
            {
                var description = $"set beats per measure at {beat} to {o}";
                PushChange(new MeasureBeatmapChange(Beatmap, () =>
                {
                    Beatmap.UpdateChangePoint<MeasureChange>(Beatmap.TickFromBeat(beat), e => e.WithBeats(o));
                    Beatmap.SnapMeasureChanges();
                    Beatmap.RemoveExtras<MeasureChange>();
                }, description));
            });
    }
    [CommandHandler]
    public bool EditTiming(CommandContext context)
    {
        var beat = SnapTargetClamp();
        var ticks = Beatmap.TickFromBeat(beat);
        var existingTiming = Beatmap.RecentChangeTicks<TempoChange>(ticks);
        var title = existingTiming.Time == ticks ? "Editing Tempo Change" : "Adding Tempo Change";
        return context.Palette.RequestNumber($"{title} at Beat {beat}", "BPM", existingTiming.Tempo.HumanBPM,
            o =>
            {
                var description = $"set bpm at {beat} to {o}";
                PushChange(new TempoBeatmapChange(Beatmap, () =>
                {
                    Beatmap.UpdateChangePoint<TempoChange>(Beatmap.TickFromBeat(beat), e => new TempoChange(e.Time, new Tempo { BPM = o }));
                    Beatmap.RemoveExtras<TempoChange>();
                }, description));
            });
    }

    [CommandHandler]
    public bool ModifyCurrentBPM(CommandContext context)
    {
        var beat = Math.Max(0, Beatmap.RoundBeat(Track.CurrentBeat));
        var recentTiming = Beatmap.RecentChange<TempoChange>(beat);
        var humanBPM = recentTiming.Tempo.HumanBPM;
        var targetBeat = (double)recentTiming.Time / Beatmap.TickRate;
        context.Palette.RequestNumber("Modifying Current BPM", "Beats per minute", humanBPM,
            newBpm =>
            {
                var description = $"set bpm at {targetBeat} to {newBpm}";
                PushChange(new TempoBeatmapChange(Beatmap, () =>
                {
                    Beatmap.UpdateChangePoint<TempoChange>(recentTiming.Time, e => e.WithTempo(new Tempo { BPM = newBpm }));
                    Beatmap.RemoveExtras<TempoChange>();
                }, description));
            }, $"This will modify the most recent timing point (beat {targetBeat}).");
        return true;
    }

    [CommandHandler] public void RemoveDuplicateNotes() => PushChange(new NoteBeatmapChange(Display, Beatmap.RemoveDuplicates, "remove duplicate notes"));

    [CommandHandler]
    public bool MultiplySectionBPM(CommandContext context) => context.GetNumber(ChangeSectionTiming,
        "Adjusting the BPM of this section without changing the timing of notes.", "BPM multiplier", 1);

    public void ChangeSectionTiming(double bpmMulitplier)
    {
        var outputTick = (int)Math.Round(Beatmap.TickRate * bpmMulitplier);
        var gcd = Util.GCD(outputTick, Beatmap.TickRate);
        var numer = outputTick / gcd;
        var denom = Beatmap.TickRate / gcd;


        var beat = Math.Max(0, Beatmap.RoundBeat(Track.CurrentBeat));
        var currentTick = Beatmap.TickFromBeat(beat);

        var start = 0;
        int? end = null;
        for (int i = 0; i < Beatmap.TempoChanges.Count; i++)
        {
            var t = Beatmap.TempoChanges[i];
            if (t.Time > currentTick)
            {
                end = t.Time;
                break;
            }
            start = t.Time;
        }

        var targetBeat = (double)start / Beatmap.TickRate;
        var description = $"multiplying BPM by {numer} / {denom} at {targetBeat}";
        if (end.HasValue) description += " to " + (double)end.Value / Beatmap.TickRate;
        PushChange(
        new CompositeHistoryChange(description, new TempoBeatmapChange(Beatmap, () =>
        {
            Beatmap.AddExtraDefault<TempoChange>();
            for (int i = 0; i < Beatmap.TempoChanges.Count; i++)
            {
                var t = Beatmap.TempoChanges[i];
                if (t.Time >= start)
                {
                    if (end.HasValue && end.Value <= t.Time)
                    { // we are after the change, so we need to offset
                        Beatmap.TempoChanges[i] = t.WithTime(t.Time + (end.Value - start) * (numer - denom) / denom);
                    }
                    else
                    {
                        Beatmap.TempoChanges[i] = new TempoChange((t.Time - start) * numer / denom + start,
                            new Tempo() { MicrosecondsPerQuarterNote = t.MicrosecondsPerQuarterNote * denom / numer });
                    }
                }
            }
            Beatmap.RemoveExtras<TempoChange>();
        }, null), new NoteBeatmapChange(Display, () =>
        {
            for (int i = 0; i < Beatmap.HitObjects.Count; i++)
            {
                var h = Beatmap.HitObjects[i];
                if (h.Time >= start)
                {
                    if (end.HasValue && end.Value <= h.Time)
                    { // we are after the change, so we need to offset
                        Beatmap.HitObjects[i] = h.WithTime(h.Time + (end.Value - start) * (numer - denom) / denom);
                    }
                    else
                    {
                        Beatmap.HitObjects[i] = h.WithTime((h.Time - start) * numer / denom + start);
                    }
                }
            }
        }, null)));
    }

    bool DangerCommand(CommandContext context, Command safe, Command force)
    {
        // don't want to close if we are dirty
        if (Dirty)
        {
            var modal = new SaveRequest(e =>
            {
                if (e == SaveOption.Save)
                {
                    Save();
                    Command.ActivateCommand(safe);
                }
                else if (e == SaveOption.DontSave)
                {
                    Command.ActivateCommand(force);
                }
            });
            context.Palette.Push(modal);
            return true;
        }
        else
        {
            return false;
        }
    }

    [CommandHandler] public bool QuitGame(CommandContext context) => DangerCommand(context, Commands.Command.QuitGame, Commands.Command.ForceQuitGame);
    [CommandHandler] public bool Close(CommandContext context) => DangerCommand(context, Commands.Command.Close, Commands.Command.CloseWithoutSaving);
    [CommandHandler]
    public void Save()
    {
        Beatmap.Export();
        Beatmap.TrySaveToDisk(MapStorage, this);
    }
    [CommandHandler] public bool ImportMidi(CommandContext context) => OpenFile(context);
}
