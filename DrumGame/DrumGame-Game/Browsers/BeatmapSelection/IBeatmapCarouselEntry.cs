namespace DrumGame.Game.Browsers.BeatmapSelection;

public interface IBeatmapCarouselEntry
{
    public BeatmapSelectorMap PrimaryMap { get; }
    public BeatmapSelectorMap MapAtIndex(int index);
    public BeatmapSelectorMap this[int index] => MapAtIndex(index);
}