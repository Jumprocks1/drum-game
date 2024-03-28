using System;
using System.Collections.Generic;
using System.IO;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace DrumGame.Game.Beatmaps.Loaders;

public static class BeatmapLoader
{
    public static void LoadTempo(Beatmap beatmap, bool free = false)
    {
        var bpmToken = beatmap.BPM;
        var tempos = new List<TempoChange>();
        beatmap.TempoChanges = tempos;
        if (bpmToken != null)
        {
            if (bpmToken is JArray array)
            {
                foreach (var token in array)
                {
                    var bpm = (double)token["bpm"];
                    var time = (double)token["time"];
                    tempos.Add(new TempoChange(beatmap.TickFromBeat(time), new Tempo { BPM = bpm }));
                }
            }
            else
            {
                var bpm = (double)bpmToken;
                tempos.Add(new TempoChange(0, new Tempo { BPM = bpm }));
            }
        }
        // free up memory
        if (free)
            beatmap.BPM = null;
    }
    public static void LoadMeasures(Beatmap beatmap, bool free = false)
    {
        beatmap.MeasureChanges = Util.ListFromToken(beatmap.MeasureConfig, token =>
            new MeasureChange(token.TryGetValue("time", out var val) ?
                beatmap.TickFromBeat((double)val) : 0, token.Value<double>("beats")));
        if (free)
            beatmap.MeasureConfig = null;
    }
    public static Beatmap From(Stream stream, string fullSourcePath, string mapStoragePath)
    {
        if (stream == null) throw new FileNotFoundException(fullSourcePath);
        using (stream)
        {
            Beatmap o;
            if (fullSourcePath.EndsWith(".dtx"))
            {
                o = DtxLoader.LoadMounted(stream, fullSourcePath, false);
            }
            else
            {
                using var sr = new StreamReader(stream);
                using var jsonTextReader = new JsonTextReader(sr);
                var serializer = new JsonSerializer
                {
                    ContractResolver = BeatmapContractResolver.Default
                };
                o = serializer.Deserialize<Beatmap>(jsonTextReader);
                o.Notes ??= new();
                o.Bookmarks ??= new();
                o.Annotations ??= new();
                o.Init();
            }
            o.Bookmarks ??= new();
            o.Annotations ??= new();
            o.Source = new BJsonSource(fullSourcePath) { MapStoragePath = mapStoragePath };
            return o;
        }
    }
}
public class BeatmapContractResolver : CamelCasePropertyNamesContractResolver
{
    public static readonly BeatmapContractResolver Default = new BeatmapContractResolver();
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        if (type == typeof(Beatmap)) type = typeof(BJson);
        return base.CreateProperties(type, memberSerialization);
    }
}

