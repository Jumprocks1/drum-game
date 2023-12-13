using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Beatmaps.Display;
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
        public FilterOperation GetOperation() => Operation switch
        {
            Operation.Delete => new DeleteOperation(),
            Operation.LeftSticking => new LeftStickingOperation(),
            Operation.RightSticking => new RightStickingOperation(),
            _ => throw new NotSupportedException(Operation.ToString())
        };
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
            return true;
        }
    }
    public enum Modifier { Accent, Ghost, Roll };
    public enum Sticking { Left, Right };
    public enum Operation { Delete, LeftSticking, RightSticking };
    public enum Target { Stride, All, Selection, SelectionOrAll, StrideOrAll };
}

public abstract class FilterOperation
{
    public abstract bool ApplyStride(Beatmap beatmap, List<int> hits);
}

public class DeleteOperation : FilterOperation
{
    public override bool ApplyStride(Beatmap beatmap, List<int> hits)
    {
        var HitObjects = beatmap.HitObjects;
        for (var i = hits.Count - 1; i >= 0; i--) // reverse order to keep indices in original list
            HitObjects.RemoveAt(hits[i]);
        return true;
    }
}
public class LeftStickingOperation : FilterOperation
{
    public override bool ApplyStride(Beatmap beatmap, List<int> hits)
    {
        var HitObjects = beatmap.HitObjects;
        foreach (var hit in hits)
            HitObjects[hit] = HitObjects[hit].With(HitObjects[hit].Modifiers & ~NoteModifiers.Right | NoteModifiers.Left);
        return true;
    }
}
public class RightStickingOperation : FilterOperation
{
    public override bool ApplyStride(Beatmap beatmap, List<int> hits)
    {
        var HitObjects = beatmap.HitObjects;
        foreach (var hit in hits)
            HitObjects[hit] = HitObjects[hit].With(HitObjects[hit].Modifiers & ~NoteModifiers.Left | NoteModifiers.Right);
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
        if (action.Target == BeatmapFilter.Target.Stride)
        {
            var op = action.GetOperation();
            SelectionStrideCommand((selection, stride) =>
            {
                var hitObjects = Beatmap.HitObjects;
                var hitsEnum = Beatmap.GetHitObjectsAt(selection, stride);
                var condition = action.Condition;
                if (condition != null) hitsEnum = hitsEnum.Where(e => condition.Matches(hitObjects[e]));
                var hits = hitsEnum.ToList();
                if (hits.Count == 0) return false;
                if (op.ApplyStride(Beatmap, hits))
                    return AffectedRange.FromSelection(selection, Beatmap);
                return false;
            }, "");
        }
        else throw new NotSupportedException(action.Target.ToString());
    }
}

