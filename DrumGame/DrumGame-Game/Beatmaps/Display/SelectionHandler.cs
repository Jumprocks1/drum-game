using System;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Commands;
using DrumGame.Game.Notation;

namespace DrumGame.Game.Beatmaps.Display;

public partial class MusicNotationBeatmapDisplay
{
    SelectionOverlay SelectionOverlay;
    public void ClearSelection()
    {
        selectionPending = false;
        Selection = null;
        SelectionOverlay?.Update(0, 0, 0);
    }
    public void StartSelection(double start)
    {
        if (Player is BeatmapEditor ed)
        {
            Selection = new BeatSelection(start);
            SelectionOverlay?.Update(0, 0, 0);
        }
    }
    public void SetSelection(double start, double end)
    {
        if (Player is BeatmapEditor ed)
        {
            selectionPending = false;
            if (start == end)
            {
                ClearSelection();
                return;
            }
            if (Selection == null || Selection.Start != start) Selection = new BeatSelection(start);
            UpdateSelection(end);
        }
    }
    public void UpdateSelection(double end)
    {
        if (Player is BeatmapEditor ed)
        {
            end = Math.Max(0, end);
            Selection.End = end == Selection.Start ? null : end;
            if (SelectionOverlay == null) NoteContainer.Add(SelectionOverlay = new SelectionOverlay(this));
            SelectionOverlay.Update(Beatmap.TickFromBeatSlow(Selection.Start), Beatmap.TickFromBeatSlow(end), ed.TickStride);
        }
    }
    public void ExpandSelectionTo(double endBeat, bool seek = true)
    {
        if (Player is BeatmapEditor ed)
        {
            if (Selection != null)
            {
                UpdateSelection(endBeat);
            }
            else
            {
                SetSelection(ed.SnapTarget, endBeat);
            }
            if (seek) Track.SeekToBeat(endBeat);
        }
    }

    public double SelectionEnd => Selection?.End ?? Track.CurrentBeat;

    [CommandHandler]
    public void SelectToEnd()
    {
        var c = Beatmap.HitObjects.Count;
        if (c > 0) ExpandSelectionTo((double)Beatmap.HitObjects[c - 1].Time / Beatmap.TickRate + 0.25);
    }
    [CommandHandler] public void SelectAll() => SetSelection(0, Beatmap.QuarterNotes);
    [CommandHandler] public void SelectRight() => ExpandSelectionTo(Track.NextHitOrBeat(SelectionEnd, true));
    [CommandHandler] public void SelectLeft() => ExpandSelectionTo(Track.NextHitOrBeat(SelectionEnd, false));
    [CommandHandler]
    public bool SelectXBeats(CommandContext context)
    {
        context.GetNumber(e => ExpandSelectionTo(SelectionEnd + e), "Select Beats", "Beat count");
        return true;
    }
}

public class BeatSelection
{
    public BeatSelection(double start)
    {
        Start = Math.Max(0, start);
    }
    public readonly double Start;
    public double? End;
    public double Left => Math.Min(Start, End.Value);
    public double Right => Math.Max(Start, End.Value);
    public bool IsComplete => End.HasValue;
    public bool HasVolume => End.HasValue && End != Start;
    public override string ToString() => Start == End ? Start.ToString() : $"{Start}-{End}";
    public string RangeString => Start == End ? $"at {Start}" : $"in range {Start}-{End}";
    public BeatSelection Clone() => new(Start)
    {
        End = End
    };

    public bool Contains(int tickRate, ITickTime tick) => Contains((double)tick.Time / tickRate);
    public bool Contains(int tickRate, int tick) => Contains((double)tick / tickRate);
    public bool Contains(double beat) => beat >= Left && beat < Right;
}
