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
    public string ImageUrl;
    public string BpmString;
    public string Date;
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
public class DtxMaps
{
    public class DtxMap
    {
        public string Filename;
        public string Url;
        public string Image;
        public string Date;
        public double BPM;
        public double[] Difficulties;
    }
    public List<DtxMap> Maps;
}
public static class Program
{
    static JsonSerializerOptions WriteOptions => new JsonSerializerOptions
    {
        IncludeFields = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // allow Japanese UTF8 characters
    };
    public static void Main()
    {
        if (!Directory.Exists("../dist/maps"))
        {
            var outputDir = Directory.CreateDirectory("../dist/maps");
            // move maps from resources folder in repo
            foreach (var map in new DirectoryInfo("../../resources/maps").GetFiles("*.bjson"))
                File.Move(map.FullName, Path.Join(outputDir.FullName, map.Name));
        }
        var maps = new DirectoryInfo("../dist/maps").GetFiles("*.bjson");

        var dict = maps.ToDictionary(e => e.Name, e => BeatmapMetadata.From(e));

        var res = new
        {
            Version = 3,
            Maps = dict
        };

        File.WriteAllText("../dist/maps.json", JsonSerializer.Serialize(res, WriteOptions));
        BuildDtxList(dict);
    }

    static void BuildDtxList(Dictionary<string, BeatmapMetadata> drumGame) // Warning: This method mutates metadata
    {
        var dtx = JsonSerializer.Deserialize<DtxMaps>(File.ReadAllText("../src/dtx.json"), new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        });

        var index = File.ReadAllText("../dist/404.html");
        var repl = @"<meta charset=""utf-8""/>";

        var dtxMaps = new Dictionary<string, BeatmapMetadata>();

        foreach (var map in dtx.Maps)
        {
            var metadata = drumGame[map.Filename];
            dtxMaps[map.Filename] = metadata;
            metadata.BpmString = map.BPM.ToString();
            metadata.ImageUrl = map.Image;
            metadata.Date = map.Date;
            var diffString = string.Join(" / ", map.Difficulties.Select(e => $"{e:0.00}"));
            metadata.DifficultyString = diffString;
            var extraTags = $@"
<meta property=""og:title"" content=""{metadata.Artist} - {metadata.Title}"" />
<meta property=""og:description"" content=""{map.BPM} BPM - {diffString}"" />
<meta property=""og:image"" content=""{map.Image}"" />
            ";
            var mapHtml = index.Replace(repl, repl + extraTags);
            var dir = Path.Join("../dist/", "dtx", map.Url);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Join(dir, "index.html"), mapHtml);
        }
        File.WriteAllText("../dist/dtx-maps.json", JsonSerializer.Serialize(new
        {
            Version = 3,
            Maps = dtxMaps
        }, WriteOptions));
    }
}
