using DrumGame.Game.Beatmaps;
namespace DrumGame.Game.Interfaces;
public interface IBeatmapIcon : IHasUrl
{
    public static abstract IBeatmapIcon TryConstruct(Beatmap beatmap, float size);
}