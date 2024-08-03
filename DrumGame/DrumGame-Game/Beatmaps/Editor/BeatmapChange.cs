using System;
using System.Collections.Generic;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Beatmaps.Loaders;

namespace DrumGame.Game.Beatmaps.Editor;

// Classes for handling history for beatmaps
public class BeatmapChange : IHistoryChange
{
    protected readonly Func<BeatmapEditor, bool> action;
    protected readonly Action<BeatmapEditor> undo;
    public string Description { get; }
    public BeatmapChange(Action<BeatmapEditor> action, Action<BeatmapEditor> undo, string description) : this(e => { action(e); return true; }, undo, description) { }
    public BeatmapChange(Action action, Action undo, string description) : this(_ => { action(); return true; }, _ => undo(), description) { }
    public BeatmapChange(Func<bool> action, Action undo, string description) : this(_ => action(), _ => undo(), description) { }
    public BeatmapChange(Func<BeatmapEditor, bool> action, Action<BeatmapEditor> undo, string description)
    {
        Description = description;
        this.action = action;
        this.undo = undo;
    }
    public virtual void OnChange(BeatmapEditor editor) { }
    public bool Do(BeatmapEditor editor)
    {
        var res = action(editor);
        if (res) OnChange(editor);
        return res;
    }
    public void Undo(BeatmapEditor editor)
    {
        undo(editor);
        OnChange(editor);
    }
}
public class MetadataChange : BeatmapChange
{
    public MetadataChange(Action action, Action undo, string description) : base(action, undo, description)
    {
    }
    public override void OnChange(BeatmapEditor editor)
    {
        editor?.Display.InfoPanel.UpdateData();
    }
}
public class OffsetBeatmapChange : PropertyChange<double>
{
    public bool YouTube;
    public override string Description => $"set {(YouTube ? "YouTube offset" : "offset")} to {NewValue} ({NewValue - OldValue:+0.00;-0.00}ms)";
    public override double Property
    {
        // Can't use Beatmap.CurrentTrackStartOffset since that can change in the middle of the undo stack
        get => YouTube ? Beatmap.YouTubeOffset + Beatmap.StartOffset : Beatmap.StartOffset;
        set
        {
            if (YouTube)
            {
                Beatmap.YouTubeOffset = Math.Round(value - Beatmap.StartOffset, 2);
                Beatmap.FireOffsetUpdated();
            }
            else Beatmap.StartOffset = Math.Round(value, 2);
        }
    }
    public OffsetBeatmapChange(BeatmapEditor editor, double newValue, bool youTube) : base(editor.Beatmap, newValue)
    {
        YouTube = youTube;
    }
}
public class PreviewTimeChange : PropertyChange<double>
{
    public override string Description => $"set preview time to {NewValue}";
    public override double Property { get => Beatmap.PreviewTime ?? -1; set => Beatmap.PreviewTime = value; }
    public PreviewTimeChange(Beatmap beatmap, double newValue) : base(beatmap, newValue) { }
}
public class LeadInBeatmapChange : PropertyChange<double>
{
    public override string Description => $"set lead-in to {NewValue}";
    public override double Property { get => Beatmap.LeadIn; set => Beatmap.LeadIn = value; }
    public LeadInBeatmapChange(Beatmap beatmap, double newValue) : base(beatmap, newValue) { }
}
public class AudioBeatmapChange : PropertyChange<string>
{
    BeatmapPlayer Player;
    public AudioBeatmapChange(Beatmap beatmap, string newValue, BeatmapPlayer player) : base(beatmap, newValue) { Player = player; }
    public override string Description => $"set audio to {NewValue}";
    public override string Property
    {
        get => Beatmap.Audio; set
        {
            Beatmap.Audio = value;
            Player.SwapTrack(Player.LoadTrack());
        }
    }
}
public abstract class PropertyChange<T> : IHistoryChange where T : IEquatable<T>
{
    public readonly T NewValue;
    public T OldValue;
    public readonly Beatmap Beatmap;
    public PropertyChange(Beatmap beatmap, T newValue)
    {
        Beatmap = beatmap;
        NewValue = newValue;
    }
    public abstract string Description { get; }
    public abstract T Property { get; set; }

    public bool Do(BeatmapEditor editor)
    {
        // have to call this here instead of in constructor since it's virtual
        OldValue = Property;
        if (OldValue == null ? NewValue == null : OldValue.Equals(NewValue)) return false;
        Property = NewValue;
        return true;
    }
    public void Undo(BeatmapEditor editor) => Property = OldValue;
}
// not actually used much yet
public class PresetBeatmapChange : BeatmapChange
{
    NotePreset Preset;
    public PresetBeatmapChange(Action<BeatmapEditor> action, Action<BeatmapEditor> undo, string description) : base(action, undo, description)
    {
    }
    public PresetBeatmapChange(NotePreset preset, Action action, Action undo, string description) : base(action, undo, description)
    {
        Preset = preset;
    }
    public bool ReloadNoteDisplay;
    public override void OnChange(BeatmapEditor editor)
    {
        if (ReloadNoteDisplay)
        {
            var display = editor.Display;
            var start = int.MaxValue;
            var end = int.MinValue;
            foreach (var hitObject in editor.Beatmap.HitObjects)
            {
                if (hitObject.Preset == Preset)
                {
                    if (start == int.MaxValue) start = hitObject.Time;
                    end = hitObject.Time;
                }
            }
            if (start < end)
            {
                display.ReloadNoteRange(new AffectedRange(start, end + 1));
            }
        }
    }
}
public class NoteBeatmapChange : IHistoryChange
{
    Func<AffectedRange> action;
    string description;
    public string Description => description;
    ViewTarget viewTarget;
    public void OverwriteDescription(string s)
    {
        description = s;
    }
    public NoteBeatmapChange(Action action, string description, ViewTarget viewTarget = null) :
        this(() => { action(); return true; }, description, viewTarget)
    { }
    public NoteBeatmapChange(Func<AffectedRange> action, string description, ViewTarget viewTarget = null)
    {
        this.action = action;
        this.description = description;
        this.viewTarget = viewTarget;
    }
    List<HitObject> Clone;
    AffectedRange Range;
    public bool Do(BeatmapEditor editor)
    {
        var display = editor.Display;
        Clone = new List<HitObject>(display.Beatmap.HitObjects);
        Range = action();
        if (Range.HasChange)
        {
            display.ReloadNoteRange(Range);
            display.PullView(viewTarget);
            return true;
        }
        else
        {
            Clone = null;
            return false;
        }
    }
    public void Undo(BeatmapEditor editor)
    {
        var display = editor.Display;
        display.Beatmap.HitObjects = Clone;
        display.ReloadNoteRange(Range);
        display.PullView(viewTarget);
    }
}
public class TempoBeatmapChange : ListBeatmapChange<TempoChange>
{
    public TempoBeatmapChange(Beatmap beatmap, Action action, string description) : base(beatmap, action, description) { }
    public override List<TempoChange> List { get => beatmap.TempoChanges; set => beatmap.TempoChanges = value; }
    public override void FireUpdate() => beatmap.FireTempoUpdated();
}
public class BookmarkBeatmapChange : ListBeatmapChange<Bookmark>
{
    public BookmarkBeatmapChange(Beatmap beatmap, Action action, string description) : base(beatmap, action, description) { }
    public override List<Bookmark> List { get => beatmap.Bookmarks; set => beatmap.Bookmarks = value; }
    public override void FireUpdate() => beatmap.FireBookmarkUpdated();
}
public class AnnotationChange : ListBeatmapChange<Annotation>
{
    public AnnotationChange(Beatmap beatmap, Action action, string description) : base(beatmap, action, description) { }
    public override List<Annotation> List { get => beatmap.Annotations; set => beatmap.Annotations = value; }
    public override void FireUpdate() => beatmap.FireAnnotationsUpdated();
}
public class MeasureBeatmapChange : ListBeatmapChange<MeasureChange>
{
    public MeasureBeatmapChange(Beatmap beatmap, Action action, string description) : base(beatmap, action, description) { }
    public override List<MeasureChange> List { get => beatmap.MeasureChanges; set => beatmap.MeasureChanges = value; }
    public override void FireUpdate() => beatmap.FireMeasuresUpdated();
}
public abstract class ListBeatmapChange<T> : IHistoryChange
{
    Action action;
    protected Beatmap beatmap;
    public string Description { get; }
    public ListBeatmapChange(Beatmap beatmap, Action action, string description)
    {
        Description = description;
        this.action = action;
        this.beatmap = beatmap;
    }
    public abstract List<T> List { get; set; }
    public abstract void FireUpdate();
    List<T> Clone;
    public bool Do(BeatmapEditor editor)
    {
        Clone = new List<T>(List);
        action();
        FireUpdate();
        return true;
    }
    public void Undo(BeatmapEditor editor)
    {
        List = Clone;
        FireUpdate();
    }
}
