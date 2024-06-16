using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Browsers.BeatmapSelection;

// this can't be a record since we store these in a HashSet
// records do not provide stable hash functions
public class BeatmapSelectorMap : ISearchable<BeatmapSelectorMap>
{
    public string MapStoragePath;
    public int Position; // stores the current position in FilterMaps (-1 if not in the current filter)
    public BeatmapSelectorMap(string mapStoragePath)
    {
        MapStoragePath = mapStoragePath;
    }
    public string FilterString { get; set; }
    public BeatmapMetadata Metadata;
    public BeatmapMetadata LoadedMetadata
    {
        get
        {
            if (Metadata == null) LoadFilter();
            return Metadata;
        }
    }
    public string FullAssetPath(string assetPath) // mirrors Beatmap.FullAssetPath
    {
        if (assetPath == null) return null;
        if (Path.DirectorySeparatorChar == '/' && assetPath.Contains('\\'))
            assetPath = assetPath.Replace('\\', '/');
        return Util.SafeFullPath(assetPath, Path.GetDirectoryName(Util.MapStorage.GetFullPath(MapStoragePath)));
    }
    public void LoadFilter()
    {
        Metadata = Util.DrumGame.MapStorage.GetMetadata(MapStoragePath);
        FilterString = Metadata.FilterString();
    }

    public static FilterFieldInfo<BeatmapSelectorMap>[] Fields { get; } = [
        new("title", "Filters by song title\nExamples:\n<code>title=unravel</>\n<code>title^</> - sorts by title"),
        new("artist", "Filters by song artist\nExamples:\n<code>artist=babymetal</>\n<code>artist^</> - sorts by artist"),
        new("mapper", "Filters by map author\nExample: <code>mapper=jumprocks</>"),
        new("difficulty",
            "Filters by map difficulty\nExample: <code>difficulty=expert</>\n\nAlso supports numeric difficulties (based on color).\n"
            + "Unknown=0, <easy>Easy</>=1, <normal>Normal</>=2, <hard>Hard</>=3, <insane>Insane</>=4, <expert>Expert</>=5, <expertplus>ExpertPlus</>=6\n"
            + "Examples:\n<code>difficulty>=2</>\n<code>d^</> - sorts by difficulty"),
        new("tags", "Filters by map tags\nExample: <code>tags=dtx-import</>"),
        new("writetime", "Filters by the file's write time. Useful for showing newly added maps.\nExamples:\n"
            + "<code>writetime>1d</> - shows maps modified within the last 24 hours\n"
            + "<code>w>12-1-2023</> - shows maps modified on/after December 1st 2023\n"
            + "<code>w^</> - sorts by write time (with the newest maps at the bottom of the map list)"),
        new("playtime", "Filters by most recent play time. Uses the score database.\nExample: <code>play\\<30d</> - shows maps that don't have scores in the last 30 days."),
        new("audio", "Filters for maps based on if they have audio or not. Currently only works with main library maps.\nExample: <code>audio=0</> - shows maps with missing audio"),
        new("rating", "Filters by map rating. To change a maps rating, click the up/down arrows on the selection card.\nExample: <code>rating>=2</> - shows maps with a rating of 2 or higher"),
        new("collection", "Filters based on if a map is in a collection.\nTo see the available collections, click the collections dropdown at the top of the map selector.\n"
        + "Examples:\n<code>col!=fav</> - excludes maps in the favorites collection."),
        new("folder", "Filters by the folder a map was loaded from. Only set for maps outside of the main library."),
        new("imageurl", "Example: <code>imageurl=i.scdn.co</>"),
        new("image"),
        new("random", "Example: <code>random^</> - sorts maps in a random order"),
        new("mapstoragepath", "Filters by the file/path name.")
    ];
    public static IEnumerable<BeatmapSelectorMap> ApplyFilter(IEnumerable<BeatmapSelectorMap> exp, FilterOperator<BeatmapSelectorMap> op,
        FilterFieldInfo<BeatmapSelectorMap> fieldInfo, string value)
    {
        if (fieldInfo.Name == "collection")
        {
            var storage = Util.DrumGame.CollectionStorage;
            var collection = storage.GetCollectionByQuery(value);
            if (collection == null) return Enumerable.Empty<BeatmapSelectorMap>();
            else
            {
                if (op.Identifier == "!=")
                {
                    var context = new CollectionRule.CollectionContext(exp, storage);
                    context.Apply(CollectionRule.Operation.Not, collection);
                    return context.FilteredMaps;
                }
                else
                    return collection.Apply(exp, storage);
            }
        }
        else if (fieldInfo.Name == "random")
        {
            if (op.Identifier == "^" || op.Identifier == "^^")
                return exp.Shuffled();
            return exp;
        }
        return ISearchable<BeatmapSelectorMap>.ApplyFilterBase(exp, op, fieldInfo, value);
    }
    public static void LoadField(FilterFieldInfo<BeatmapSelectorMap> field)
    {
        var name = field.Name;
        if (name == "playtime") Util.MapStorage.LoadPlayTimes();
        else if (name == "audio") Util.MapStorage.LoadAudioFiles();
        else if (name == "rating") Util.MapStorage.LoadRatings().Wait();
    }

    public static FilterAccessor GetAccessor(FilterFieldInfo<BeatmapSelectorMap> fieldInfo)
    {
        var fieldName = fieldInfo.Name;
        if (fieldName == "audio") fieldName = nameof(BeatmapMetadata.HasAudio);

        var field = typeof(BeatmapMetadata).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (field != null)
        {
            var parameter = Expression.Parameter(typeof(BeatmapSelectorMap));
            var metadata = Expression.Field(parameter, "Metadata");
            var exp = Expression.Lambda(Expression.Field(metadata, field), parameter);
            var acc = new FilterAccessor(exp)
            {
                Time = field.Name.Contains("Time")
            };

            if (fieldName == "difficulty")
            {
                acc.EnumStringAccessor = FilterFieldInfo<BeatmapSelectorMap>.GetDefaultAccessor(nameof(BeatmapMetadata.DifficultyString));
            }

            return acc;
        }
        return null;
    }
}