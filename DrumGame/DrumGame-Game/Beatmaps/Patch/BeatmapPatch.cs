
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DrumGame.Game.Beatmaps.Patch;

public static class BeatmapPatch
{
    // make sure to call .Export() before
    public static JObject DiffAgainstDisk(Beatmap beatmap)
    {
        var originalBeatmap = Util.MapStorage.LoadMapForPlay(beatmap.Source.MapStoragePath);
        originalBeatmap.Export();
        return Diff(originalBeatmap, beatmap);
    }
    // this assumes both a and b are exported
    public static JObject Diff(Beatmap a, Beatmap b)
    {
        var serializer = JsonSerializer.Create(Beatmap.SerializerSettings);
        return Diff(JObject.FromObject(a, serializer), JObject.FromObject(b, serializer));
    }

    public static JObject Diff(JObject original, JObject current)
    {
        var o = new JObject();
        foreach (var (key, currentValue) in current)
        {
            if (original.TryGetValue(key, out var originalValue))
            {
                if (!JToken.DeepEquals(originalValue, currentValue))
                {
                    if (originalValue is JObject oVJ && currentValue is JObject cVJ)
                        o.Add(key, Diff(oVJ, cVJ));
                    else o.Add(key, currentValue);
                }
            }
            else o.Add(key, currentValue);
        }
        // null doesn't work for many keys, so this doesn't really work
        // foreach (var (key, _) in original)
        // {
        //     if (!current.ContainsKey(key))
        //         o.Add(key, null);
        // }
        return o;
    }

    public static Beatmap ApplyPatch(Beatmap beatmap, JObject patch)
    {
        var serializer = JsonSerializer.Create(Beatmap.SerializerSettings);
        beatmap.Export();
        var jo = JObject.FromObject(beatmap, serializer);
        jo.Merge(patch, new JsonMergeSettings
        {
            MergeArrayHandling = MergeArrayHandling.Replace,
            MergeNullValueHandling = MergeNullValueHandling.Merge
        });
        return jo.ToObject<Beatmap>(serializer);
    }
}