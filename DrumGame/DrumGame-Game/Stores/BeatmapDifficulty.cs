using System;
using Newtonsoft.Json;

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
        BeatmapDifficulty.Unknown => null,
        _ => difficulty.ToString()
    };

    public static BeatmapDifficulty Parse(string difficulty) => difficulty switch
    {
        "Expert+" => BeatmapDifficulty.ExpertPlus,
        _ => Enum.TryParse<BeatmapDifficulty>(difficulty, out var o) ? o : BeatmapDifficulty.Unknown
    };
}


public class BeatmapDifficultyConverter : JsonConverter<BeatmapDifficulty>
{
    public override void WriteJson(JsonWriter writer, BeatmapDifficulty value, JsonSerializer serializer)
        => writer.WriteValue(value.ToDifficultyString());
    public override BeatmapDifficulty ReadJson(JsonReader reader, Type objectType, BeatmapDifficulty existingValue, bool hasExistingValue, JsonSerializer serializer)
        => BeatmapDifficultyExtensions.Parse((string)reader.Value);
}
