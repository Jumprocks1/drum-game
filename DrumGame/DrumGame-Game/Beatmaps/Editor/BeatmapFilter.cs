using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Channels;
using DrumGame.Game.Commands;
using Newtonsoft.Json;

namespace DrumGame.Game.Beatmaps.Editor;

public class BeatmapFilter
{
    public List<FilterAction> Actions { get; set; }
    public string Name { get; set; }
    public class FilterAction
    {
        public Condition Condition { get; set; }
        public Operation Operation { get; set; }
        public Target Target { get; set; } // defaults to Stride
        public BJsonNote NewNote { get; set; }
        public FilterOperation GetOperation()
        {
            if (Operation == Operation.Replace)
            {
                var data = NewNote.ToHitObjectData();
                return new ModifierFilterOperation(e => e.With(data));
            }
            return Operation switch
            {
                Operation.Delete => new DeleteOperation(),
                Operation.LeftSticking => new ModifierFilterOperation(e => e.With(e.Modifiers & ~NoteModifiers.Right | NoteModifiers.Left)),
                Operation.RightSticking => new ModifierFilterOperation(e => e.With(e.Modifiers & ~NoteModifiers.Left | NoteModifiers.Right)),
                Operation.Accent => new ModifierFilterOperation(e => e.With(e.Modifiers & ~NoteModifiers.Ghost | NoteModifiers.Accented)),
                Operation.Ghost => new ModifierFilterOperation(e => e.With(e.Modifiers & ~NoteModifiers.Accented | NoteModifiers.Ghost)),
                _ => throw new NotSupportedException(Operation.ToString())
            };
        }
    }
    public class Condition
    {
        public HashSet<DrumChannel> Channels { get; set; }
        public Modifier? Modifier { get; set; }
        public Sticking? Sticking { get; set; }
        public bool Matches(HitObject ho)
        {
            if (Channels != null && !Channels.Contains(ho.Channel)) return false;
            if (Sticking != null)
            {
                if (Sticking == BeatmapFilter.Sticking.Left)
                {
                    if (!ho.Modifiers.HasFlag(NoteModifiers.Left)) return false;
                }
                else if (Sticking == BeatmapFilter.Sticking.Right)
                {
                    if (!ho.Modifiers.HasFlag(NoteModifiers.Right)) return false;
                }
            }
            if (Modifier is Modifier m)
            {
                if (m == BeatmapFilter.Modifier.None)
                {
                    if ((ho.Modifiers & NoteModifiers.AccentedGhost) != 0) return false;
                }
                else if (m == BeatmapFilter.Modifier.Accent)
                {
                    if (!ho.Modifiers.HasFlag(NoteModifiers.Accented)) return false;
                }
                else if (m == BeatmapFilter.Modifier.Ghost)
                {
                    if (!ho.Modifiers.HasFlag(NoteModifiers.Ghost)) return false;
                }
            }
            return true;
        }
    }
    public enum Modifier { None, Accent, Ghost, Roll };
    public enum Sticking { Left, Right };
    public enum Operation { Delete, Replace, LeftSticking, RightSticking, Ghost, Accent };
    public enum Target { Stride, All, Selection, SelectionOrAll, StrideOrAll };
}

public abstract class FilterOperation
{
    public abstract bool ApplyTo(Beatmap beatmap, List<int> hits);
}

public class DeleteOperation : FilterOperation
{
    public override bool ApplyTo(Beatmap beatmap, List<int> hits)
    {
        var HitObjects = beatmap.HitObjects;
        for (var i = hits.Count - 1; i >= 0; i--) // reverse order to keep indices in original list
            HitObjects.RemoveAt(hits[i]);
        return true;
    }
}
public class ModifierFilterOperation : FilterOperation
{
    Func<HitObject, HitObject> Modify;
    public ModifierFilterOperation(Func<HitObject, HitObject> modify)
    {
        Modify = modify;
    }
    public override bool ApplyTo(Beatmap beatmap, List<int> hits)
    {
        var HitObjects = beatmap.HitObjects;
        foreach (var hit in hits)
            HitObjects[hit] = Modify(HitObjects[hit]);
        return true;
    }
}

public partial class BeatmapEditor
{
    [CommandHandler]
    public bool ApplyFilter(CommandContext context)
    {
        context.GetString(FilterManager.GetFilterList().Select(e => e.Name), filterName =>
        {
            var filter = FilterManager.GetFilter(filterName);
            if (filter == null) return;
            ApplyFilter(filter);
        }, "Applying Filter", "Filter");
        return true;
    }
    void ApplyFilter(BeatmapFilter filter)
    {
        using var _ = UseCompositeChange($"{filter.Name} filter");
        foreach (var action in filter.Actions) ApplyFilterAction(action);
    }
    void ApplyFilterAction(BeatmapFilter.FilterAction action)
    {
        if (!Editing) return;
        var selection = Display.Selection;

        var target = action.Target;

        if (target == BeatmapFilter.Target.StrideOrAll)
        {
            if (selection != null && selection.IsComplete)
                target = BeatmapFilter.Target.Stride;
            else
                target = BeatmapFilter.Target.All;
        }
        if (target == BeatmapFilter.Target.Stride)
            selection = GetSelectionOrCursor();

        IEnumerable<int> hitsEnum = null;
        var hitObjects = Beatmap.HitObjects;
        if (target == BeatmapFilter.Target.Stride)
            hitsEnum = Beatmap.GetHitObjectsAt(selection, TickStride);
        else if (target == BeatmapFilter.Target.All)
            hitsEnum = Enumerable.Range(0, hitObjects.Count);
        else throw new NotSupportedException(target.ToString());

        var condition = action.Condition;
        if (condition != null) hitsEnum = hitsEnum.Where(e => condition.Matches(hitObjects[e]));
        var hits = hitsEnum.ToList();

        var op = action.GetOperation();
        // needs to be captured for undo stack purposes
        AffectedRange doChange()
        {
            if (hits.Count == 0) return false;
            if (!op.ApplyTo(Beatmap, hits)) return false;

            if (target == BeatmapFilter.Target.All)
                return true;
            return AffectedRange.FromSelection(selection, Beatmap);
        }
        PushChange(new NoteBeatmapChange(doChange, ""));
    }
}

