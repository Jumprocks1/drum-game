
namespace DrumGame.Game.Stores;

public enum BeatmapDifficulty
{
    Unknown,
    Easy,
    Normal,
    Hard,
    Insane,
    Expert,
    ExpertPlus
}

public static class BeatmapDifficultyExtensions
{
    public static string ToDifficultyString(this BeatmapDifficulty difficulty) => difficulty switch
    {
        BeatmapDifficulty.ExpertPlus => "Expert+",
        _ => difficulty.ToString()
    };
}