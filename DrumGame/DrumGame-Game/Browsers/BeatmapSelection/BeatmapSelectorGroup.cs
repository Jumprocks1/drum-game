using System.Collections.Generic;

namespace DrumGame.Game.Browsers.BeatmapSelection;

public class BeatmapSelectorGroup : IBeatmapCarouselEntry
{
    public List<BeatmapSelectorMap> Maps;
    public BeatmapSelectorMap PrimaryMap => Maps[^1];
    public BeatmapSelectorMap MapAtIndex(int index) => index < Maps.Count ? Maps[index] : PrimaryMap;
}