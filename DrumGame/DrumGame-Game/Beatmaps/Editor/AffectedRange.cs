using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Display;

namespace DrumGame.Game.Beatmaps.Editor;

public readonly struct AffectedRange
{
    public readonly int Start;
    public readonly int End; // exclusive
    public AffectedRange(int start, int end)
    {
        Start = start; End = end;
    }
    public AffectedRange(int tick) : this(tick, tick + 1) { }
    public static implicit operator AffectedRange(bool e) => new(int.MinValue, e ? int.MaxValue : int.MinValue);
    public static implicit operator AffectedRange(int e) => new(e);
    public bool Contains(int tick) => tick >= Start && tick < End;
    public bool Contains(ITickTime tick) => tick.Time >= Start && tick.Time < End;
    public bool HasChange => End != int.MinValue;
    public bool Everything => End == int.MaxValue;
    public static AffectedRange FromSelection(BeatSelection selection, Beatmap beatmap) =>
        selection != null && selection.HasVolume ?
            new AffectedRange(beatmap.TickFromBeatSlow(selection.Left), beatmap.TickFromBeatSlow(selection.Right)) :
            new AffectedRange(beatmap.TickFromBeatSlow(selection.Start));
    public static AffectedRange FromSelectionOrEverything(BeatSelection selection, Beatmap beatmap) =>
        selection != null && selection.HasVolume ?
            new AffectedRange(beatmap.TickFromBeatSlow(selection.Left), beatmap.TickFromBeatSlow(selection.Right)) :
            new AffectedRange(int.MinValue, int.MaxValue);
    public string ToString(int tickRate) => Everything ? "all notes" : $"notes between beats {(double)Start / tickRate}-{(double)End / tickRate}";
}
