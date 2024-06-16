using System;
using System.Collections.Generic;
using System.IO;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace DrumGame.Game.Beatmaps.Formats;


public class BJsonFormat : BeatmapFormat
{
    public static readonly BJsonFormat Instance = new();
    BJsonFormat() { }

    public override string Name => "BJson";
    public override string Tag => "bjson";
    public override bool CanSave => true;

    public override bool CanReadFile(string fullSourcePath) => fullSourcePath.EndsWith(".bjson", StringComparison.OrdinalIgnoreCase);
    protected override Beatmap LoadInternal(Stream stream, string fullPath, bool metadataOnly, bool prepareForPlay)
    {
        using (stream)
        using (var sr = new StreamReader(stream))
        {
            var serializer = new JsonSerializer
            {
                ContractResolver = metadataOnly ? BeatmapMetadataContractResolver.Default : BeatmapContractResolver.Default
            };
            var o = (Beatmap)serializer.Deserialize(sr, typeof(Beatmap)); // can't use generic because of sr

            if (prepareForPlay)
            {
                o.Notes ??= new();
                o.Init();
            }
            return o;
        }
    }
}

public class BeatmapContractResolver : CamelCasePropertyNamesContractResolver
{
    public static readonly BeatmapContractResolver Default = new();
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        if (type == typeof(Beatmap)) type = typeof(BJson);
        return base.CreateProperties(type, memberSerialization);
    }
}

public static class BJsonLoadHelpers
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
}
