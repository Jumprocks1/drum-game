using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Build;


public class BeatmapMetadata
{
    public string Id;
    public string Title;
    public string Artist;
    public string Mapper;
    public object Difficulty; // this comes in as a string, but we convert it to an int
    public string DifficultyName; // this is the custom name of the difficulty (if it exists)
    public string DifficultyString; // this is the name displayed on the map selection screen
    public string Tags;
    public long WriteTime;
    public static BeatmapMetadata From(FileInfo fileInfo)
    {
        var res = JsonSerializer.Deserialize<BeatmapMetadata>(File.ReadAllText(fileInfo.FullName),
            new JsonSerializerOptions
            {
                IncludeFields = true,
                PropertyNameCaseInsensitive = true
            });

        var diff = res.Difficulty?.ToString();
        res.DifficultyString = res.DifficultyName ?? diff;
        res.Difficulty = diff switch
        {
            "Expert+" => 6,
            "Expert" => 5,
            "Insane" => 4,
            "Hard" => 3,
            "Normal" => 2,
            "Easy" => 1,
            _ => 0
        };
        res.DifficultyName = null; // don't need to serialize this

        res.WriteTime = fileInfo.LastWriteTimeUtc.Ticks;
        return res;
    }
}
public static class Program
{
    public static void Main()
    {
        if (!Directory.Exists("../dist/maps"))
        {
            // copy maps from resources folder in repo
            throw new NotImplementedException();
        }
        var maps = new DirectoryInfo("../dist/maps").GetFiles("*.bjson");

        var dict = maps.ToDictionary(e => e.Name, e => BeatmapMetadata.From(e));

        var res = new
        {
            Version = 3,
            Maps = dict
        };

        File.WriteAllText("../dist/maps.json", JsonSerializer.Serialize(res, new JsonSerializerOptions
        {
            IncludeFields = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // allow Japanese UTF8 characters
        }));
    }
}
