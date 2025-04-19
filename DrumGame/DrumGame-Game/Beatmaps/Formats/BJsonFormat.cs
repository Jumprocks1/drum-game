using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
    protected override Beatmap LoadInternal(Stream stream, LoadMapParameters parameters)
    {
        using (stream)
        using (var sr = new StreamReader(stream))
        {
            var settings = Beatmap.SerializerSettings;
            if (parameters.MetadataOnly)
                settings.ContractResolver = BeatmapMetadataContractResolver.Default;
            var serializer = JsonSerializer.Create(settings);
            var o = (Beatmap)serializer.Deserialize(sr, typeof(Beatmap)); // can't use generic because of sr

            if (parameters.PrepareForPlay)
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
    public BeatmapContractResolver()
    {
        NamingStrategy.ProcessDictionaryKeys = false;
    }
    public static readonly BeatmapContractResolver Default = new();
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        if (type == typeof(Beatmap)) type = typeof(BJson);
        return base.CreateProperties(type, memberSerialization);
    }
}

public class BeatmapMetadataContractResolver : BeatmapContractResolver
{
    public new static readonly BeatmapMetadataContractResolver Default = new();
    protected override JsonProperty CreateProperty(MemberInfo member,
                                     MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);
        if (property.DeclaringType == typeof(BJson) && property.PropertyName == "Notes")
        {
            property.ShouldSerialize = _ => false;
        }
        return property;
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
