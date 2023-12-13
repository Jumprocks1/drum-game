using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Browsers;
using DrumGame.Game.Browsers.BeatmapSelection;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace DrumGame.Game.Stores;

public class MetadataCache
{
    [JsonProperty(Required = Required.Always)]
    public int Version;
    [JsonProperty(Required = Required.Always)]
    public Dictionary<string, BeatmapMetadata> Maps;

    // parameterless constructor is used for JSON loading
    public static MetadataCache New() => new()
    {
        Version = BeatmapMetadata.Version,
        Maps = new()
    };
}

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
public class BeatmapMetadata
{
    public string Id;
    public const int Version = 4;
    public string Title;
    public string Artist;
    public string Mapper;
    public string Folder;
    public BeatmapDifficulty Difficulty;
    public string DifficultyString;
    public string Tags;
    public long WriteTime;
    public string Audio;
    public string Image;
    public string ImageUrl;
    public string SHA;
    public double Duration;
    public double BPM;
    public string BpmRange;
    [JsonIgnore] public bool HasAudio; // depends on if the user has the audio files or not, loaded during runtime (not cached)
    // this is loaded from replay information. If we eventually upgrade to database metadata, we should be able to store this in the database
    // we don't want to store it in .cache.json since that gets deleted occasionally
    [JsonIgnore] public long PlayTime = -1; // -1 for not loaded, 0 for never played, otherwise UtcTicks
    [JsonIgnore] public int Rating = int.MinValue; // MinValue for not loaded
    [JsonIgnore] public bool RatingLoaded => Rating != int.MinValue;
    // note, you can't search based on Difficulty right now
    // if we want to do that, we should create a BeatmapDifficulty => string mapping
    public string FilterString() => $"{Title} {Artist} {Mapper} {DifficultyString} {Tags}";
    public BeatmapMetadata() { } // used by Newtonsoft
    public void Update(Beatmap beatmap, long writeTime)
    {
        Id = beatmap.Id;
        Title = beatmap.Title ?? beatmap.Source.FilenameNoExt;
        Artist = beatmap.Artist;
        Mapper = beatmap.Mapper;
        Audio = beatmap.Audio;
        WriteTime = writeTime;
        DifficultyString = beatmap.DifficultyName ?? beatmap.Difficulty;
        Difficulty = beatmap.Difficulty switch
        {
            "Expert+" => BeatmapDifficulty.ExpertPlus,
            "Expert" => BeatmapDifficulty.Expert,
            "Insane" => BeatmapDifficulty.Insane,
            "Hard" => BeatmapDifficulty.Hard,
            "Normal" => BeatmapDifficulty.Normal,
            "Easy" => BeatmapDifficulty.Easy,
            _ => BeatmapDifficulty.Unknown
        };
        Tags = beatmap.Tags;
        Image = beatmap.Image;
        ImageUrl = beatmap.ImageUrl;
        Duration = beatmap.PlayableDuration;
        BPM = beatmap.MedianBPM;
        BpmRange = beatmap.BpmRange;
        if (beatmap.Source.MapStoragePath != null && beatmap.Source.MapStoragePath.StartsWith("$"))
        {
            // for .dtx files we go up 2 layers for this field
            // this is because the filename is not very useful (ie. mstr.dtx)
            // we also skip the first character to hide the $
            if (beatmap.Source.Filename.EndsWith(".dtx", true, CultureInfo.InvariantCulture))
                Folder = Path.GetDirectoryName(Path.GetDirectoryName(beatmap.Source.MapStoragePath[1..]));
        }
    }
    public BeatmapMetadata(Beatmap beatmap, long writeTime) // write time is separate since it isn't stored in the beatmap
    {
        Update(beatmap, writeTime);
    }
    public string[] SplitTags() => Tags?.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? new string[0];

}
public class MapStorage : NativeStorage, IDisposable
{
    const string cachePath = ".cache.json";
    string FullCachePath => GetFullPath(cachePath);
    bool dirty = false;
    private MetadataCache CachedMetadata;
    public MapLibraries MapLibraries => Util.ConfigManager.MapLibraries.Value;
    public List<MapLibrary> ValidLibraries => MapLibraries.ValidLibraries;
    MetadataCache LoadedMetadata
    {
        get
        {
            if (CachedMetadata == null) LoadMetadataCache();
            return CachedMetadata;
        }
    }
    public void PurgeCache()
    {
        CachedMetadata = MetadataCache.New();
        dirty = true;
    }
    public void LoadMetadataCache(bool force = false)
    {
        if (CachedMetadata != null && !force) return;
        try
        {
            // this only takes 15ms on my machine, it will take longer with more maps/metadata, but overall it's pretty fast
            // 95% of the time is spent in the deserialize method (file read is ~0.5ms)
            // if we ever wanted this to be faster, we would need our own file format
            using var file = File.OpenText(FullCachePath);
            var serializer = new JsonSerializer();
            CachedMetadata = (MetadataCache)serializer.Deserialize(file, typeof(MetadataCache));
            if (CachedMetadata.Version != BeatmapMetadata.Version)
            {
                Logger.Log("Beatmap cache out of date, purging", level: LogLevel.Important);
                PurgeCache();
            }
            else CheckWriteTimes();
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to load beatmap cache, purging");
            PurgeCache();
        }
    }
    // normally write time is only checked when we first load beatmap metadata
    public void CheckWriteTimes()
    {
        var writeTimes = GetWriteTimes();
        var remove = new List<string>();
        foreach (var map in CachedMetadata.Maps)
            if (!writeTimes.TryGetValue(map.Key, out var v) || v != map.Value.WriteTime)
                remove.Add(map.Key);
        if (remove.Count > 0)
        {
            dirty = true;
            foreach (var r in remove)
                CachedMetadata.Maps.Remove(r);
        }
    }
    public void ForceReloadMetadata(MapLibrary provider)
    {
        var metadata = GetCachedMetadata();
        var remove = new List<string>();
        foreach (var map in metadata)
            if (provider.IsInProvider(map.Key))
                remove.Add(map.Key);
        if (remove.Count > 0)
        {
            dirty = true;
            foreach (var r in remove)
                CachedMetadata.Maps.Remove(r);
        }
        MapLibraries.InvokeChanged(provider);
    }
    public Dictionary<string, BeatmapMetadata> GetCachedMetadata() // may not return everything if metadata is outdated
    {
        if (CachedMetadata == null) LoadMetadataCache();
        return CachedMetadata.Maps;
    }
    public IEnumerable<(string, BeatmapMetadata)> GetAllMetadata()
    {
        foreach (var file in GetMaps())
            yield return (file, GetMetadata(file));
    }
    public BeatmapMetadata GetMetadata(BeatmapSelectorMap map) => GetMetadata(map?.Filename);
    public BeatmapMetadata GetMetadata(string filename)
    {
        if (filename == null) return null;
        if (CachedMetadata == null) LoadMetadataCache();
        var o = CachedMetadata.Maps.GetValueOrDefault(filename);
        if (o == null)
        {
            CachedMetadata.Maps[filename] = o = new BeatmapMetadata(DeserializeMap(filename, skipNotes: true), GetWriteTime(filename));
            dirty = true;
        }
        return o;
    }
    public BeatmapMetadata GetMetadataFromId(string id)
    {
        if (CachedMetadata == null) LoadMetadataCache();
        return CachedMetadata.Maps.Values.FirstOrDefault(e => e.Id == id);
    }
    public void SaveMapCache()
    {
        if (!dirty || CachedMetadata == null) return;
        using var file = File.CreateText(FullCachePath);
        var serializer = new JsonSerializer { DefaultValueHandling = DefaultValueHandling.Ignore };
        serializer.Serialize(file, CachedMetadata);
        dirty = false;
    }
    bool playTimesLoaded = false;
    public void LoadPlayTimes()
    {
        if (playTimesLoaded) return;
        using (var context = Util.GetDbContext())
        {
            var dict = context.Beatmaps.ToDictionary(e => e.Id);
            foreach (var metadata in LoadedMetadata.Maps.Values)
                metadata.PlayTime = dict.GetValueOrDefault(metadata.Id)?.PlayTime ?? 0;
        }
        playTimesLoaded = true;
    }

    Task loadingRatings;
    public bool RatingsLoaded { get; private set; } // set after all ratings are safely loaded
    public Task LoadRatings() => loadingRatings ??= loadRatingsAsync();
    Task loadRatingsAsync() => Task.Run(() =>
    {
        using (var context = Util.GetDbContext())
        {
            var dict = context.Beatmaps.Select(e => new { e.Id, e.Rating }) // this helps performance a lot
                .Where(e => e.Rating != 0) // this doesn't seem to impact performance really
                .ToDictionary(e => e.Id, e => e.Rating);
            foreach (var metadata in LoadedMetadata.Maps.Values)
                metadata.Rating = dict.GetValueOrDefault(metadata.Id);
            RatingsLoaded = true;
        }
    });

    static HashSet<string> audio;
    public void LoadAudioFiles()
    {
        if (audio == null)
        {
            audio = Directory.GetFiles(GetFullPath("audio")).Select(e => Path.GetFileName(e)).ToHashSet();
            foreach (var metadata in CachedMetadata.Maps.Values)
            {
                var au = metadata.Audio;
                if (Path.DirectorySeparatorChar == '/' && au.Contains('\\'))
                    au = au.Replace('\\', '/');
                metadata.HasAudio = audio.Contains(Path.GetFileName(au));
            }
        }
    }

    public void Dispose() => SaveMapCache();
    public long GetWriteTime(string map) => Directory.GetLastWriteTimeUtc(GetFullPath(map)).Ticks;
    public void ReplaceMetadata(string map, Beatmap beatmap)
    {
        if (LoadedMetadata.Maps.TryGetValue(map, out var v))
            v.Update(beatmap, GetWriteTime(map));
        else
            LoadedMetadata.Maps[map] = new BeatmapMetadata(beatmap, GetWriteTime(map));
        dirty = true;
    }
    public readonly string AbsolutePath;
    public MapStorage(string path, GameHost host) : base(path, host)
    {
        Util.EnsureExists(path);
        AbsolutePath = path;
    }

    Dictionary<string, long> GetWriteTimes()
    {
        var res = new Dictionary<string, long>(CachedMetadata?.Maps.Count ?? 1024);
        foreach (var location in ValidLibraries)
            location.AddWriteTimes(res);
        return res;
    }

    public IEnumerable<string> GetMaps() // returns relative paths. Extra storages will start with $
    {
        var sources = ValidLibraries;
        if (sources.Count == 1) return sources[0].GetMaps();
        var res = Enumerable.Empty<string>();
        foreach (var location in sources)
            res = res.Concat(location.GetMaps());
        return res;
    }

    public override string GetFullPath(string path, bool _ = false)
    {
        if (path == null) return null;
        if (path.StartsWith("$"))
        {
            var slash = path.IndexOf('/');
            return Path.GetFullPath(path[(slash + 1)..], MapLibraries.PathMapping[path[1..slash]]);
        }
        return Path.GetFullPath(path, AbsolutePath);
    }
    public string RelativePath(string path) => path == null ? null : Path.GetRelativePath(AbsolutePath, path);

    public Beatmap LoadMap(BeatmapMetadata metadata) => LoadMapFromId(metadata.Id);
    public void Save(Beatmap beatmap) => beatmap.SaveToDisk(this);
    public Beatmap LoadMap(string path)
    {
        try
        {
            return BeatmapLoader.From(GetStream(path), GetFullPath(path), path);
        }
        catch (Exception ex)
        {
            var msg = $"Failed to load beatmap {path}";
            Logger.Error(ex, msg);
            Util.Palette.ShowMessage(msg);
        }
        return null;
    }
    public Beatmap LoadMapFromId(string mapId)
    {
        if (CachedMetadata == null) LoadMetadataCache();
        return LoadMap(CachedMetadata.Maps.FirstOrDefault(e => e.Value.Id == mapId).Key);
    }
    // use when we want the raw JSON instead of a loaded beatmap
    // absolute is used when loading a map from a random location. Currently this is just when someone drops in a .bjson file
    // skipNotes should be true whenever we don't need the notes and we don't intend to save the map
    // if we intend to modified + save the map, we need the notes so they don't get overwritten when we save the map
    public Beatmap DeserializeMap(string path, bool absolute = false, bool skipNotes = false)
    {
        var fullPath = absolute ? path : GetFullPath(path);
        var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Beatmap o;
        if (path.EndsWith(".dtx", true, CultureInfo.InvariantCulture))
        {
            if (!skipNotes) throw new NotImplementedException("Cannot load a DTX file for editing");
            try
            {
                o = DtxLoader.LoadMounted(stream, fullPath, true);
            }
            catch (Exception e)
            {
                o = Beatmap.Create();
                o.Description = $"Failed to load DTX file.\n{e}";
                o.Title = path;
                o.Tags = "dtx-failed-load";
                Logger.Error(e, $"Failed to load {path}");
            }
        }
        else
        {
            try
            {
                o = DeserializeBjson(stream, skipNotes);
            }
            catch (Exception e)
            {
                o = Beatmap.Create();
                o.Description = $"Failed to load BJson file.\n{e}";
                o.Title = path;
                o.Tags = "bjson-failed-load";
                Logger.Error(e, $"Failed to load {path}");
            }
        }
        o.Source = new BJsonSource(fullPath) { MapStoragePath = absolute ? null : path };
        return o;
    }
    public bool CanEdit(string filename) => filename.EndsWith(".bjson", true, CultureInfo.InvariantCulture);
    public bool CanEdit(BeatmapSelectorMap map) => CanEdit(map.Filename);
    // make sure to set Beatmap.Source after this
    public Beatmap DeserializeBjson(Stream stream, bool skipNotes)
    {
        using (stream)
        using (var sr = new StreamReader(stream))
        {
            var serializer = new JsonSerializer
            {
                ContractResolver = skipNotes ? BeatmapMetadataContractResolver.Default : BeatmapContractResolver.Default
            };
            return (Beatmap)serializer.Deserialize(sr, typeof(Beatmap)); // can't use generic because of sr
        }
    }


    public override void Delete(string path)
    {
        path = GetFullPath(path);
        var relativePath = RelativePath(path);
        Logger.Log($"deleting {path}", level: LogLevel.Important);

        if (CachedMetadata != null)
        {
            if (CachedMetadata.Maps.Remove(relativePath))
                dirty = true;
        }

        if (File.Exists(path))
            File.Delete(path);
    }

    // returns path to file relative to MapStorage
    public string StoreExistingFile(string file, Beatmap beatmap, string folder = null)
    {
        var alreadyStored = Util.RelativeOrNullPath(file, AbsolutePath);
        if (alreadyStored != null) return alreadyStored;

        var relativePath = Path.Join(folder, Path.GetFileName(file));
        var target = beatmap.FullAssetPath(relativePath);
        if (target != file)
        {
            try
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file, target);
                }
                catch (Exception e)
                {
                    var sourceInfo = new FileInfo(file);
                    var targetInfo = new FileInfo(target);
                    if (sourceInfo.LastWriteTimeUtc == targetInfo.LastWriteTimeUtc)
                        Logger.Log("Skipping copy, same write time", level: LogLevel.Important);
                    else
                    {
                        Logger.Error(e, $"Failed to copy to {target}, trying again with overwrite enabled");
                        File.Copy(file, target, true);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to copy to {target}");
                return null;
            }
        }
        return relativePath;
    }

    public List<(string, BeatmapMetadata)> GetMapSet(Beatmap beatmap)
    {
        var set = new List<(string, BeatmapMetadata)>();
        foreach (var (file, metadata) in GetCachedMetadata())
        {
            if (metadata.Artist == beatmap.Artist && metadata.Mapper == beatmap.Mapper
                && metadata.Audio == beatmap.Audio)
                set.Add((file, metadata));
        }
        return set;
    }
}
public class BeatmapMetadataContractResolver : CamelCasePropertyNamesContractResolver
{
    public static readonly BeatmapMetadataContractResolver Default = new BeatmapMetadataContractResolver();
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        if (type == typeof(Beatmap)) type = typeof(BJson);
        return base.CreateProperties(type, memberSerialization);
    }
    protected override JsonProperty CreateProperty(MemberInfo member,
                                     MemberSerialization memberSerialization)
    {
        JsonProperty property = base.CreateProperty(member, memberSerialization);
        if (property.DeclaringType == typeof(BJson) && property.PropertyName == "Notes")
        {
            property.ShouldSerialize = _ => false;
        }
        return property;
    }
}
