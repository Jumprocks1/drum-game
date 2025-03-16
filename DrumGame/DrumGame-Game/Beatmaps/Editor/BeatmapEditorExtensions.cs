using System;
using DrumGame.Game.Beatmaps.Display;
using osu.Framework.Extensions.EnumExtensions;

namespace DrumGame.Game.Beatmaps.Editor;

public partial class BeatmapEditor
{
    public bool Editing => Mode.HasFlagFast(BeatmapPlayerMode.Edit);
    public int TickStride => BeatSnap.HasValue ? (int)(Beatmap.TickRate / BeatSnap.Value + 0.5) : Beatmap.TickRate;
    public int CurrentMeasure => Track.CurrentMeasure;
    public (int, int) GetCurrentRange()
    {
        var sel = Display.Selection;
        if (sel != null && sel.IsComplete)
        {
            // use slow here because these could be negative
            return (Beatmap.TickFromBeatSlow(sel.Left), Beatmap.TickFromBeatSlow(sel.Right));
        }
        else
        {
            var measure = CurrentMeasure;
            return (Beatmap.TickFromMeasure(measure), Beatmap.TickFromMeasure(measure + 1));
        }
    }
    public BeatSelection GetSelectionOrCursor(bool clone = true)
    {
        var sel = Display.Selection;
        if (sel != null && sel.IsComplete)
        {
            return clone ? sel.Clone() : sel;
        }
        else
        {
            var target = SnapTarget;
            return new BeatSelection(target) { End = target };
        }
    }
    public BeatSelection GetSelectionOrNull(bool clone = true)
    {
        var sel = Display.Selection;
        if (sel != null && sel.IsComplete)
        {
            return clone ? sel.Clone() : sel;
        }
        else return null;
    }

    public ViewTarget ViewTargetFromAffectedRange(AffectedRange range)
    {
        if (!range.HasChange || range.Everything) return null;
        var a = Beatmap.BeatFromTick(range.Start);
        var b = Beatmap.BeatFromTick(range.End - 1);
        return new ViewTarget(Math.Min(a, b), Math.Max(a, b));
    }
}
