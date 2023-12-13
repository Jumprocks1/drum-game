using System;
using System.Collections.Generic;
using System.IO;
using DrumGame.Game.Utils;
using Newtonsoft.Json;

namespace DrumGame.Game.Beatmaps.Editor;

public static class FilterManager
{
    public static long WriteTime = -1;
    static List<BeatmapFilter> Filters;
    static void LoadFilters()
    {
        // if (Filters != null) return;
        var fileName = Util.Resources.GetAbsolutePath("filters/filters.json");
        var writeTime = File.GetLastWriteTimeUtc(fileName).Ticks;
        if (writeTime == WriteTime) return;

        WriteTime = writeTime;
        Filters = JsonConvert.DeserializeObject<List<BeatmapFilter>>(File.ReadAllText(fileName));
    }
    public static List<BeatmapFilter> GetFilterList()
    {
        LoadFilters();
        return Filters;
    }
    public static BeatmapFilter GetFilter(string name)
    {
        LoadFilters();
        foreach (var f in Filters)
            if (name.Equals(f.Name, StringComparison.InvariantCultureIgnoreCase)) return f;
        foreach (var f in Filters)
            if (f.Name.StartsWith(name, StringComparison.InvariantCultureIgnoreCase)) return f;
        foreach (var f in Filters)
            if (f.Name.Contains(name, StringComparison.InvariantCultureIgnoreCase)) return f;
        return null;
    }
}