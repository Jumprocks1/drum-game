using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Formats;
using DrumGame.Game.Browsers.BeatmapSelection;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace DrumGame.Game.Stores;

// reason for loading map
public enum LoadMapIntent
{
    PlayOrEdit,
    MetadataPreview,
    QuickEdit // edit without playing
}
public class LoadMapParameters
{
    public LoadMapParameters(LoadMapIntent intent)
    {
        PrepareForPlay = intent == LoadMapIntent.PlayOrEdit;
        MetadataOnly = intent == LoadMapIntent.MetadataPreview;
    }
    public string MapStoragePath;
    public string FullPath;
    public string Difficulty; // optional, only used for song.ini for now
    public readonly bool PrepareForPlay;
    public readonly bool MetadataOnly;
}

public class MapStorage : NativeStorage, IDisposable
{
    const string cachePath = ".cache.json";
    string FullCachePath => GetFullPath(cachePath);
    bool dirty = false;
    private MetadataCache CachedMetadata; // nullable
    public MapLibraries MapLibraries => Util.ConfigManager.MapLibraries.Value;
    public List<MapLibrary> ValidLibraries => MapLibraries.ValidLibraries;
    public MapSetDictionary MapSets = new(); // should be adjusted in sync with CachedMetadata
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
        MapSets.Clear();
        CachedMetadata = MetadataCache.New();
        dirty = true;
    }
    public void LoadMetadataCache(bool force = false)
    {
        if (CachedMetadata != null && !force) return;
        try
        {
            // this only takes 25ms on my machine, it will take longer with more maps/metadata, but overall it's pretty fast
            // 95% of the time is spent in the deserialize method (file read is ~0.5ms)
            // if we ever wanted this to be faster, we would need our own file format
            using var file = File.OpenText(FullCachePath);
            var serializer = new JsonSerializer();
            MapSets.Clear();
            CachedMetadata = (MetadataCache)serializer.Deserialize(file, typeof(MetadataCache));
            if (CachedMetadata.Version != BeatmapMetadata.Version)
            {
                Logger.Log("Beatmap cache out of date, purging", level: LogLevel.Important);
                PurgeCache();
            }
            else CheckWriteTimes();
            foreach (var map in CachedMetadata.Maps)
                MapSets.Add(map.Key, map.Value); // 0.5ms on 1000+ maps
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
        foreach (var map in LoadedMetadata.Maps)
            if (!writeTimes.TryGetValue(map.Key, out var v) || v != map.Value.WriteTime)
                remove.Add(map.Key);
        RemoveCachedMetadata(remove);
    }
    void RemoveCachedMetadata(List<string> mapStoragePaths)
    {
        if (mapStoragePaths.Count > 0)
        {
            var metadata = LoadedMetadata;
            dirty = true;
            foreach (var e in mapStoragePaths)
            {
                if (metadata.Maps.Remove(e, out var meta))
                    MapSets.Remove(meta);
            }
        }
    }
    public void ForceReloadMetadata(MapLibrary provider)
    {
        var metadata = LoadedMetadata;
        var remove = new List<string>();
        foreach (var map in metadata.Maps)
            if (provider.IsInProvider(map.Key))
                remove.Add(map.Key);
        RemoveCachedMetadata(remove);
        MapLibraries.InvokeChanged(provider);
    }
    public Dictionary<string, BeatmapMetadata> GetCachedMetadata() => LoadedMetadata.Maps;
    public IEnumerable<(string, BeatmapMetadata)> GetAllMetadata()
    {
        foreach (var file in GetMaps())
            yield return (file, GetMetadata(file));
    }
    public BeatmapMetadata GetMetadata(BeatmapSelectorMap map) => GetMetadata(map?.MapStoragePath);
    public BeatmapMetadata GetMetadata(Beatmap map) => GetMetadata(map?.Source.MapStoragePath);
    public BeatmapMetadata GetMetadata(string mapStoragePath)
    {
        if (mapStoragePath == null) return null;
        var o = LoadedMetadata.Maps.GetValueOrDefault(mapStoragePath);
        o ??= MakeAndStoreMetadata(LoadForQuickMetadata(mapStoragePath), mapStoragePath);
        return o;
    }
    public BeatmapMetadata GetMetadataFromId(string id) => LoadedMetadata.Maps.Values.FirstOrDefault(e => e.Id == id);
    public void SaveMapCache()
    {
        if (!dirty || CachedMetadata == null) return;
        using var file = File.CreateText(FullCachePath);
        var serializer = new JsonSerializer { DefaultValueHandling = DefaultValueHandling.Ignore };
        serializer.Serialize(file, CachedMetadata);
        dirty = false;
    }
    bool playTimesLoaded = false;
    public void LoadPlayTimes() // this doesn't work for maps not in the cache
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

    Dictionary<string, int> playCounts;
    public int GetPlayCount(string mapId)
    {
        if (playCounts == null)
        {
            using var context = Util.GetDbContext();
            playCounts = context.Replays.GroupBy(e => e.MapId)
                 .Select(e => new { MapId = e.Key, Count = e.Count() })
                 .ToDictionary(e => e.MapId, e => e.Count);
        }
        return playCounts.TryGetValue(mapId, out var o) ? o : 0;
    }

    Task loadingRatings;
    public bool RatingsLoaded { get; private set; } // set after all ratings are safely loaded
    public Task LoadRatings() => loadingRatings ??= loadRatingsAsync();
    public void LoadRating(BeatmapMetadata metadata)
    {
        if (metadata.RatingLoaded) return;
        if (!RatingsLoaded && loadingRatings == null)
        {
            LoadRatings();
            return;
        }
        Task.Run(() =>
        {
            using var db = Util.GetDbContext();
            var dbMap = db.GetOrAddBeatmap(metadata.Id);
            metadata.Rating = dbMap.Rating;
        });
    }
    Task loadRatingsAsync() => Task.Run(() => // this doesn't work for maps not in the cache
    {
        using var context = Util.GetDbContext();
        var dict = context.Beatmaps.Select(e => new { e.Id, e.Rating }) // this helps query performance a lot
            .Where(e => e.Rating != 0) // this doesn't seem to impact performance really
            .ToDictionary(e => e.Id, e => e.Rating);
        foreach (var metadata in LoadedMetadata.Maps.Values)
            metadata.Rating = dict.GetValueOrDefault(metadata.Id);
        RatingsLoaded = true;
    });

    static HashSet<string> audio;
    public void LoadAudioFiles()
    {
        if (audio == null)
        {
            audio = Directory.GetFiles(GetFullPath("audio")).Select(e => Path.GetFileName(e)).ToHashSet();
            foreach (var metadata in LoadedMetadata.Maps.Values)
            {
                var au = metadata.Audio;
                if (Path.DirectorySeparatorChar == '/' && au.Contains('\\'))
                    au = au.Replace('\\', '/');
                metadata.HasAudio = audio.Contains(Path.GetFileName(au));
            }
        }
    }

    public void Dispose() => SaveMapCache();
    public long GetWriteTime(string mapStoragePath) => Directory.GetLastWriteTimeUtc(GetFullPath(mapStoragePath)).Ticks;
    public void ReplaceMetadata(string mapStoragePath, Beatmap beatmap)
    {
        if (string.IsNullOrWhiteSpace(mapStoragePath)) return;
        if (LoadedMetadata.Maps.TryGetValue(mapStoragePath, out var v))
        {
            var oldMapSetId = v.MapSetId;
            v.Update(beatmap, GetWriteTime(mapStoragePath));
            MapSets.MapSetIdChanged(mapStoragePath, v, oldMapSetId);
        }
        else MakeAndStoreMetadata(beatmap, mapStoragePath);
        dirty = true;
    }

    // beatmap doesn't have to be a fully loaded map
    // `LoadForQuickMetadata` is sufficient
    BeatmapMetadata MakeAndStoreMetadata(Beatmap beatmap, string mapStoragePath)
    {
        var o = new BeatmapMetadata(beatmap, GetWriteTime(mapStoragePath));
        LoadedMetadata.Maps[mapStoragePath] = o;
        if (RatingsLoaded && !o.RatingLoaded)
            // only issue with this is if we GetMetadata multiple times before this loads, it will schedule multiple loads
            // this shouldn't really matter, but it should be kept in mind
            // a workaround would be to assign int.MinValue + 1 to mean "Loading" to prevent duplicate loads
            LoadRating(o);
        MapSets.Add(mapStoragePath, o);
        dirty = true;
        return o;
    }

    public readonly string AbsolutePath;
    public MapStorage(string path, GameHost host) : base(path, host)
    {
        AbsolutePath = Path.GetFullPath(path);
        Util.EnsureExists(AbsolutePath);
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
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (path[0] == '$')
        {
            var slash = path.IndexOf('/');
            return Path.GetFullPath(path[(slash + 1)..], MapLibraries.PathMapping[path[1..slash]]);
        }
        return Path.GetFullPath(path, AbsolutePath);
    }
    public bool Contains(string path)
    {
        var absolutePath = GetFullPath(path);
        foreach (var source in ValidLibraries)
        {
            if (source.Contains(absolutePath))
                return true;
        }
        return false;
    }
    // this is not good anymore
    // it doesn't properly consider library paths
    public string RelativePath(string path) => path == null ? null : Path.GetRelativePath(AbsolutePath, path);
    public Beatmap LoadMap(BeatmapMetadata metadata) => LoadMapFromId(metadata.Id);
    public void Save(Beatmap beatmap) => beatmap.SaveToDisk(this);
    public Beatmap LoadMapFromId(string mapId) => LoadMapForPlay(LoadedMetadata.Maps.FirstOrDefault(e => e.Value.Id == mapId).Key);
    public Beatmap LoadMap(string mapStoragePath, LoadMapIntent intent) => LoadMap(new LoadMapParameters(intent)
    {
        MapStoragePath = mapStoragePath
    });
    public Beatmap LoadMapForPlay(string mapStoragePath) => LoadMap(mapStoragePath, LoadMapIntent.PlayOrEdit);
    public Beatmap LoadMap(LoadMapParameters parameters)
    {
        try
        {
            parameters.FullPath = GetFullPath(parameters.MapStoragePath);
            using var stream = GetStream(parameters.MapStoragePath) ?? throw new FileNotFoundException(parameters.FullPath);
            foreach (var format in BeatmapFormat.Formats)
            {
                if (format.CanReadFile(parameters.FullPath))
                    return format.Load(stream, parameters);
            }
        }
        catch (Exception ex)
        {
            var msg = $"Failed to load beatmap {parameters.MapStoragePath}";
            Logger.Error(ex, msg);
            Util.Palette.ShowMessage(msg);
        }
        return null;
    }
    Beatmap LoadMapForMetadata(string mapStoragePath, bool savingPlanned)
    {
        var fullPath = GetFullPath(mapStoragePath);
        // not sure why this uses File.Open instead of GetStream
        // I think I like File.Open better, but above we use GetStream
        var intent = savingPlanned ? LoadMapIntent.QuickEdit : LoadMapIntent.MetadataPreview;
        var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        foreach (var format in BeatmapFormat.Formats)
        {
            if (format.CanReadFile(mapStoragePath))
            {
                if (!format.CanSave && savingPlanned)
                    throw new NotImplementedException($"Cannot load a {format.Name} file for editing");
                return format.TryLoad(stream, mapStoragePath, fullPath, intent);
            }
        }

        var failed = Beatmap.Create();
        failed.Description = "File not recognized";
        failed.Title = fullPath;
        Logger.Log($"File not recognized: {fullPath}", level: LogLevel.Error);
        return failed;
    }
    // Used for quick edits, typically done on the selection screen
    public Beatmap LoadForQuickEdit(string mapStoragePath) => LoadMapForMetadata(mapStoragePath, true);
    public Beatmap LoadForQuickMetadata(string mapStoragePath) => LoadMapForMetadata(mapStoragePath, false);
    public bool CanEdit(BeatmapSelectorMap map) => BJsonFormat.Instance.CanReadFile(map.MapStoragePath);

    public override void Delete(string path)
    {
        path = GetFullPath(path);
        var relativePath = RelativePath(path);
        Logger.Log($"deleting {path}", level: LogLevel.Important);

        if (CachedMetadata != null)
        {
            if (CachedMetadata.Maps.Remove(relativePath, out var meta))
            {
                dirty = true;
                MapSets.Remove(meta);
            }
        }

        if (File.Exists(path))
            File.Delete(path);
    }

    // returns path to file relative to copyToFolder
    public static string StoreOrHash(string file, string copyToFolder, string folder = null)
    {
        var fileName = Path.GetFileName(file);
        var relativePath = Path.Join(folder, fileName);
        var target = Path.GetFullPath(relativePath, copyToFolder);
        if (target != file)
        {
            try
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file, target);
                }
                catch (Exception)
                {
                    var sourceInfo = new FileInfo(file);
                    var targetInfo = new FileInfo(target);
                    if (sourceInfo.LastWriteTimeUtc == targetInfo.LastWriteTimeUtc && sourceInfo.Length == targetInfo.Length)
                        Logger.Log("Skipping copy, same write time and length", level: LogLevel.Important);
                    else
                    {
                        var hash = Util.MD5(File.OpenRead(file)).ToLowerInvariant();
                        if (sourceInfo.Length == targetInfo.Length && hash.Equals(Util.MD5(File.OpenRead(target)), StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.Log("Skipping copy, same length and MD5 hash", level: LogLevel.Important);
                        }
                        else
                        {
                            relativePath = Path.Join(folder, hash + "-" + fileName);
                            target = Path.GetFullPath(relativePath, copyToFolder);
                            if (File.Exists(target))
                                Logger.Log("Skipping copy, hashed file already exists", level: LogLevel.Important);
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(target));
                                File.Copy(file, target);
                            }
                        }
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

    // returns path to file relative to Beatmap.Source.Directory
    public string StoreOrHash(string file, Beatmap beatmap, string folder = null) => StoreOrHash(file, beatmap.Source.Directory, folder);

    public IReadOnlyList<MapSetEntry> GetMapSet(Beatmap beatmap) => MapSets[beatmap];
}