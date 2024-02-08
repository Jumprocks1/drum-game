using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DrumGame.Game.Browsers;
using DrumGame.Game.Browsers.BeatmapSelection;
using DrumGame.Game.Components.Basic.Autocomplete;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace DrumGame.Game.Stores;

[JsonConverter(typeof(CollectionRuleConverter))]
public abstract class CollectionRule
{
    [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
    public enum Operation
    {
        And,
        Or,
        Not
    }
    public Operation Op;
    public class CollectionContext
    {
        public List<BeatmapSelectorMap> Maps; // this is the list of maps that will be used for Or operations
        public IEnumerable<BeatmapSelectorMap> FilteredMaps;
        public CollectionStorage Storage;

        public CollectionContext(IEnumerable<BeatmapSelectorMap> maps, CollectionStorage storage)
        {
            var list = maps.AsList();
            Maps = list;
            FilteredMaps = list;
            Storage = storage;
        }

        public void Apply(Operation op, Collection collection)
        {
            if (op == Operation.And)
            {
                FilteredMaps = collection.Apply(FilteredMaps, Storage);
            }
            else if (op == Operation.Or)
            {
                var filtered = collection.Apply(Maps, Storage).AsHashSet();
                filtered.UnionWith(FilteredMaps);
                FilteredMaps = filtered;
            }
            else if (op == Operation.Not)
            {
                var filtered = collection.Apply(Maps, Storage).AsHashSet();
                FilteredMaps = FilteredMaps.Where(e => !filtered.Contains(e));
            }
        }
    }
    public virtual void Apply(CollectionContext context)
    {
        if (Op == Operation.And) ApplyAnd(context);
        else if (Op == Operation.Or) ApplyOr(context);
        else if (Op == Operation.Not) ApplyNot(context);
    }
    public virtual void ApplyAnd(CollectionContext context) => context.FilteredMaps = Filter(context.FilteredMaps, context);
    public virtual void ApplyOr(CollectionContext context)
    {
        var set = context.FilteredMaps.AsHashSet();
        set.UnionWith(Filter(context.Maps, context));
        context.FilteredMaps = set;
    }
    public virtual void ApplyNot(CollectionContext context) =>
        context.FilteredMaps = context.FilteredMaps.Where(e => !Filter(e));
    public virtual IEnumerable<BeatmapSelectorMap> Filter(IEnumerable<BeatmapSelectorMap> maps, CollectionContext _) => maps.Where(Filter);
    public abstract bool Filter(BeatmapSelectorMap map);
}
[JsonConverter(typeof(DisabledConverter))]
public class CollectionRuleQuery : CollectionRule
{
    public CollectionRuleQuery() { }
    public CollectionRuleQuery(string query, Operation operation = Operation.And)
    {
        Query = query;
        Op = operation;
    }
    public string Query;
    public override bool Filter(BeatmapSelectorMap map)
        => throw new NotImplementedException();
    public override IEnumerable<BeatmapSelectorMap> Filter(IEnumerable<BeatmapSelectorMap> maps, CollectionContext context)
        => GenericFilterer<BeatmapSelectorMap>.Filter(maps, Query);
    // could optimize
    public override void ApplyNot(CollectionContext context)
    {
        var set = context.FilteredMaps.AsHashSet();
        set.ExceptWith(Filter(context.Maps, context));
        context.FilteredMaps = set;
    }
}
[JsonConverter(typeof(DisabledConverter))]
public class CollectionRuleList : CollectionRule
{
    [JsonProperty("query")]
    public HashSet<string> List;
    public override bool Filter(BeatmapSelectorMap map) => List.Contains(map.MapStoragePath);
    public override void ApplyAnd(CollectionContext context)
    {
        if (List.Count > 0) base.ApplyAnd(context);
        else context.FilteredMaps = Enumerable.Empty<BeatmapSelectorMap>();
    }
    public override void ApplyNot(CollectionContext context)
    {
        if (List.Count > 0) base.ApplyNot(context);
    }
}
[JsonConverter(typeof(DisabledConverter))]
public class CollectionRuleRef : CollectionRule
{
    public class Q { public string Ref; }
    public Q Query;
    public CollectionRuleRef() { } // for Newtonsoft
    public CollectionRuleRef(string collection)
    {
        Query = new Q { Ref = collection };
    }
    [JsonIgnore] public string Ref => Query.Ref;
    public override void Apply(CollectionContext context) =>
        context.Apply(Op, context.Storage.GetCollection(Ref));
    public override bool Filter(BeatmapSelectorMap map) => throw new NotImplementedException();
}
[JsonConverter(typeof(DisabledConverter))]
public class CollectionRuleCollection : CollectionRule
{
    public Collection Collection;
    public override void Apply(CollectionContext context) => context.Apply(Op, Collection);
    public override bool Filter(BeatmapSelectorMap map) => throw new NotImplementedException();
}


public class CollectionRuleConverter : JsonConverter<CollectionRule>
{
    public override CollectionRule ReadJson(JsonReader reader, Type objectType, CollectionRule existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var jobj = JObject.ReadFrom(reader);
        var query = jobj["query"];
        var queryType = query.Type;
        if (queryType == JTokenType.String)
            return jobj.ToObject<CollectionRuleQuery>();
        else if (queryType == JTokenType.Array)
            return jobj.ToObject<CollectionRuleList>();
        else
        {
            if (query["ref"] != null) return jobj.ToObject<CollectionRuleRef>();
            else return jobj.ToObject<CollectionRuleCollection>();
        }
    }

    public override void WriteJson(JsonWriter writer, CollectionRule value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}

public class DisabledConverter : JsonConverter
{
    public override bool CanRead => false;
    public override bool CanWrite => false;
    public override bool CanConvert(Type objectType) => throw new NotImplementedException();
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) =>
        throw new NotImplementedException();
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
        throw new NotImplementedException();
}

public class Collection : IFilterable
{
    public Collection() { }
    public Collection(string name, string source = null)
    {
        Name = name;
        Source = source ?? name.ToFilename(".json");
    }
    [JsonIgnore] public string Source;
    [JsonProperty(Order = int.MinValue)] public string Name { get; set; }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string Description;
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public bool Locked;
    public List<CollectionRule> Rules;
    public IEnumerable<BeatmapSelectorMap> Apply(IEnumerable<BeatmapSelectorMap> maps, CollectionStorage storage)
    {
        var context = new CollectionRule.CollectionContext(maps, storage);
        foreach (var rule in Rules) rule.Apply(context);
        return context.FilteredMaps;
    }
    public bool Contains(BeatmapSelectorMap map, CollectionStorage storage) =>
        Apply(new List<BeatmapSelectorMap> { map }, storage).Count() > 0;

    public bool Simplify()
    {
        // just remove some rules that don't do anything
        var changed = false;
        for (var i = Rules.Count - 1; i >= 0; i--)
        {
            var rule = Rules[i];
            if (rule is CollectionRuleList list && list.List.Count == 0 &&
                (rule.Op == CollectionRule.Operation.Or || rule.Op == CollectionRuleList.Operation.Not))
            {
                Rules.RemoveAt(i);
                changed = true;
            }
        }
        return changed;
    }

    public static Collection NewEmpty(string name, string filename = null) => new Collection(name, filename)
    {
        Rules = new List<CollectionRule> { new CollectionRuleList { List = new(), Op = CollectionRule.Operation.And } }
    };
}


public class CollectionStorage : NativeStorage, IDisposable
{
    // should probably do some fancy thread-safe saving of these after like 5s
    public List<Collection> DirtyCollections = new();
    public Dictionary<string, Collection> LoadedCollections;
    public MapStorage MapStorage; // used for loading extra properties that we may not have access to currently

    public CollectionStorage(string path, MapStorage mapStorage, GameHost host = null) : base(path, host)
    {
        MapStorage = mapStorage;
        if (!Directory.Exists(path)) CreateDefaultCollections(path);
    }

    void CreateDefaultCollections(string path)
    {
        Logger.Log("Creating default collections", level: LogLevel.Important);
        Directory.CreateDirectory(path);
        Save(Collection.NewEmpty("Favorites"));
        Save(new Collection("Sort By Difficulty")
        {
            Rules = new List<CollectionRule> { new CollectionRuleQuery("diff^") }
        });
        Save(new Collection("Recently Added")
        {
            Rules = new List<CollectionRule> { new CollectionRuleQuery("writeTime>1d") }
        });
    }

    public IEnumerable<string> GetCollections() => GetFiles(".", "*.json");
    public IEnumerable<Collection> GetAllCollections() => GetCollections().Select(e => GetCollection(e));

    public Collection GetCollectionByQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        query = query.ToLowerInvariant();
        var list = GetCollections().AsArray();
        for (var i = 0; i < list.Length; i++) list[i] = list[i].ToLowerInvariant();
        foreach (var s in list)
            if (query == s || query == Path.GetFileNameWithoutExtension(s)) return GetCollection(s);
        foreach (var s in list)
            if (s.StartsWith(query)) return GetCollection(s);
        foreach (var s in list)
            if (s.Contains(query)) return GetCollection(s);
        return null;
    }

    public Collection GetCollection(string collection)
    {
        if (collection == null) return null;
        LoadedCollections ??= new();
        if (!LoadedCollections.TryGetValue(collection, out var c))
        {
            using var stream = GetStream(collection, mode: FileMode.Open);
            if (stream == null) return null;
            using var sr = new StreamReader(stream);
            using var jsonTextReader = new JsonTextReader(sr);
            var serializer = new JsonSerializer();
            c = serializer.Deserialize<Collection>(jsonTextReader);
            c.Source = collection;
            LoadedCollections[collection] = c;
        }
        return c;
    }

    public string GetName(string collection) => GetCollection(collection)?.Name ?? "Default";

    private static int _last = 0;
    public static void Debounce(int ms, Action action)
    {
        var current = Interlocked.Increment(ref _last);
        Task.Delay(ms).ContinueWith(task =>
        {
            if (current == _last) action();
            task.Dispose();
        });
    }

    public void Dirty(Collection collection)
    {
        collection.Simplify();
        lock (DirtyCollections)
        {
            if (!DirtyCollections.Contains(collection)) DirtyCollections.Add(collection);
            Debounce(500, SaveAll);
        }
    }
    public void Save(Collection collection)
    {
        if (collection == null) return;
        using var stream = GetStream(collection.Source, FileAccess.Write, FileMode.Create);
        using var writer = new StreamWriter(stream);
        var s = stream as FileStream;
        Logger.Log($"Saving collection to {collection.Source}", level: LogLevel.Important);
        var serializer = new JsonSerializer
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
        serializer.Serialize(writer, collection);
    }

    public void SaveAll()
    {
        Collection[] copy;
        lock (DirtyCollections)
        {
            if (DirtyCollections.Count == 0) return;
            copy = DirtyCollections.ToArray();
            DirtyCollections.Clear();
        }
        foreach (var c in copy) Save(c);
    }

    bool disposed = false;

    public void Dispose()
    {
        // DANGER: might be some problems if we dispose during a pending save
        if (disposed) return;
        disposed = true;
        SaveAll();
    }
}