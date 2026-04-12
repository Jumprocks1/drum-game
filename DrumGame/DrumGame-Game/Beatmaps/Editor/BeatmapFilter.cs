using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Channels;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using osu.Framework.Logging;

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
                Operation.Insert => new InsertFilterOperation(),
                Operation.LeftSticking => new ModifierFilterOperation(e => e.With(e.Modifiers & ~NoteModifiers.Right | NoteModifiers.Left)),
                Operation.RightSticking => new ModifierFilterOperation(e => e.With(e.Modifiers & ~NoteModifiers.Left | NoteModifiers.Right)),
                Operation.Accent => new ModifierFilterOperation(e => e.With(e.Modifiers & ~NoteModifiers.Ghost | NoteModifiers.Accented)),
                Operation.Ghost => new ModifierFilterOperation(e => e.With(e.Modifiers & ~NoteModifiers.Accented | NoteModifiers.Ghost)),
                _ => throw new NotSupportedException(Operation.ToString())
            };
        }
        public void Apply(BeatmapEditor editor)
        {
            if (!editor.Editing) return;
            GetOperation().Apply(this, editor, editor.Display?.Selection);
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
    public enum Operation { Delete, Insert, Replace, LeftSticking, RightSticking, Ghost, Accent };
    public enum Target { Stride, All, Selection, SelectionOrAll, StrideOrAll };

    public void Apply(BeatmapEditor editor)
    {
        using var _ = editor.UseCompositeChange($"{Name} filter");
        foreach (var action in Actions)
            action.Apply(editor);
    }
}

public abstract class FilterOperation
{
    public abstract bool Apply(BeatmapFilter.FilterAction action, BeatmapEditor editor, BeatSelection selection);
}
public abstract class ModifyFilterOperation : FilterOperation
{
    public override bool Apply(BeatmapFilter.FilterAction action, BeatmapEditor editor, BeatSelection selection)
    {
        if (!editor.Editing) return false;

        var beatmap = editor.Beatmap;

        var target = action.Target;

        if (target == BeatmapFilter.Target.StrideOrAll)
        {
            if (selection != null && selection.IsComplete)
                target = BeatmapFilter.Target.Stride;
            else
                target = BeatmapFilter.Target.All;
        }
        if (target == BeatmapFilter.Target.SelectionOrAll)
        {
            if (selection != null && selection.IsComplete)
                target = BeatmapFilter.Target.Selection;
            else
                target = BeatmapFilter.Target.All;
        }
        if (target == BeatmapFilter.Target.Stride || target == BeatmapFilter.Target.Selection)
            // pretty awkward passing around the input selection like this
            // I think ideally we would move the selection field to BeatmapEditor to make this easier to test (without a display)
            selection = editor.GetSelectionOrCursor(sel: selection);

        IEnumerable<int> hitsEnum = null;
        var hitObjects = beatmap.HitObjects;
        if (target == BeatmapFilter.Target.Stride)
            hitsEnum = beatmap.GetHitObjectsAt(selection, editor.TickStride);
        else if (target == BeatmapFilter.Target.Selection)
            hitsEnum = beatmap.GetHitObjectsIn(selection);
        else if (target == BeatmapFilter.Target.All)
            hitsEnum = Enumerable.Range(0, hitObjects.Count);
        else throw new NotSupportedException(target.ToString());

        var condition = action.Condition;
        if (condition != null) hitsEnum = hitsEnum.Where(e => condition.Matches(hitObjects[e]));
        var hits = hitsEnum.ToList();

        // needs to be captured for undo stack purposes
        AffectedRange doChange()
        {
            if (hits.Count == 0) return false;
            if (!ApplyTo(beatmap, hits)) return false;

            if (target == BeatmapFilter.Target.All)
                return true;
            return AffectedRange.FromSelection(selection, beatmap);
        }
        return editor.PushChange(new NoteBeatmapChange(doChange, ""));
    }
    public abstract bool ApplyTo(Beatmap beatmap, List<int> hits);
}

public class DeleteOperation : ModifyFilterOperation
{
    public override bool ApplyTo(Beatmap beatmap, List<int> hits)
    {
        var HitObjects = beatmap.HitObjects;
        for (var i = hits.Count - 1; i >= 0; i--) // reverse order to keep indices in original list
            HitObjects.RemoveAt(hits[i]);
        return true;
    }
}
public class ModifierFilterOperation : ModifyFilterOperation
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

public class InsertFilterOperation : FilterOperation
{
    public override bool Apply(BeatmapFilter.FilterAction action, BeatmapEditor editor, BeatSelection selection)
    {
        if (action.Condition != null)
            Logger.Log("`Condition` not supported for insert filters", level: LogLevel.Error);
        if (action.Target != BeatmapFilter.Target.Stride)
            Logger.Log($"Target `{action.Target}` not supported for insert filters. Use \"stride\"", level: LogLevel.Error);
        selection = editor.GetSelectionOrCursor(sel: selection);

        var stride = editor.TickStride;
        var beatmap = editor.Beatmap;
        var newData = action.NewNote.ToHitObjectData();

        // needs to be captured for undo stack purposes
        AffectedRange doChange() => Apply(beatmap, selection, stride, newData);
        return editor.PushChange(new NoteBeatmapChange(doChange, ""));
    }

    public AffectedRange Apply(Beatmap beatmap, BeatSelection selection, int stride, HitObjectData newData)
        => beatmap.ApplyStrideAction(selection, stride, (tick, toggle) => beatmap.AddHit(tick, newData, toggle), true);
}

public partial class BeatmapEditor
{
    [CommandHandler]
    public bool ApplyFilter(CommandContext context)
    {
        var modal = context.GetString(FilterManager.GetFilterList().Select(e => e.Name), filterName =>
        {
            FilterManager.GetFilter(filterName)?.Apply(this);
        }, "Applying Filter", "Filter");
        modal?.AddFooterButtonSpaced(new DrumButton
        {
            Text = "View Filter File",
            Action = FilterManager.RevealFilterFile,
            AutoSize = true
        });
        return true;
    }
}

