using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Formats;
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
public class BeatmapMetadata
{
    public string Id;
    public const int Version = 5;
    public string Title;
    public string RomanTitle;
    public string RomanArtist;
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
    public string MapSetId;
    [JsonIgnore] public bool HasAudio; // depends on if the user has the audio files or not, loaded during runtime (not cached)
    // this is loaded from replay information. If we eventually upgrade to database metadata, we should be able to store this in the database
    // we don't want to store it in .cache.json since that gets deleted occasionally
    [JsonIgnore] public long PlayTime = -1; // -1 for not loaded, 0 for never played, otherwise UtcTicks
    [JsonIgnore] public int Rating = int.MinValue; // MinValue for not loaded, this is typically modified from a background thread
    [JsonIgnore] public bool RatingLoaded => Rating != int.MinValue;
    string _dtxLevel;
    [JsonIgnore] public string DtxLevel => _dtxLevel ??= Beatmap.FormatDtxLevel(GetDtxLevel()) ?? ""; // return empty string instead of null for caching
    // note, you can't search based on Difficulty right now
    // if we want to do that, we should create a BeatmapDifficulty => string mapping
    public string FilterString() => $"{Title} {Artist} {Mapper} {DifficultyString} {Tags} {RomanTitle} {RomanArtist}";
    public BeatmapMetadata() { } // used by Newtonsoft
    public void Update(Beatmap beatmap, long writeTime)
    {
        _dtxLevel = null; // reset cache if needed
        Id = beatmap.Id;
        MapSetId = beatmap.MapSetIdNonNull;
        Title = beatmap.Title ?? beatmap.Source.FilenameNoExt;
        Artist = beatmap.Artist;
        Mapper = beatmap.Mapper;
        Audio = beatmap.Audio;
        RomanTitle = beatmap.RomanTitle;
        RomanArtist = beatmap.RomanArtist;
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
        if (beatmap.Source.MapStoragePath != null && beatmap.Source.MapStoragePath.StartsWith('$'))
        {
            // for .dtx files we go up 2 layers for this field
            // this is because the filename is not very useful (ie. mstr.dtx)
            // we also skip the first character to hide the $
            // It may seem weird splitting the logic like this, but in reality it works well
            Folder = Path.GetDirectoryName(beatmap.Source.MapStoragePath[1..]);
            if (beatmap.Source.Filename.EndsWith(".dtx", true, CultureInfo.InvariantCulture))
            {
                var secondParent = Path.GetDirectoryName(Folder);
                if (!string.IsNullOrWhiteSpace(secondParent))
                    Folder = secondParent;
            }
        }
    }
    public BeatmapMetadata(Beatmap beatmap, long writeTime) // write time is separate since it isn't stored in the beatmap
    {
        Update(beatmap, writeTime);
    }
    public string[] SplitTags() => Tags?.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];

    string GetDtxLevel() => Beatmap.GetDtxLevel(SplitTags());
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
        if (remove.Count > 0)
        {
            dirty = true;
            foreach (var r in remove)
            {
                if (CachedMetadata.Maps.Remove(r, out var meta))
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
        if (remove.Count > 0)
        {
            dirty = true;
            foreach (var r in remove)
                metadata.Maps.Remove(r);
        }
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
        if (o == null)
        {
            LoadedMetadata.Maps[mapStoragePath] = o = new BeatmapMetadata(LoadForQuickMetadata(mapStoragePath), GetWriteTime(mapStoragePath));
            if (RatingsLoaded && !o.RatingLoaded)
                // only issue with this is if we GetMetadata multiple times before this loads, it will schedule multiple loads
                // this shouldn't really matter, but it should be kept in mind
                // a workaround would be to assign int.MinValue + 1 to mean "Loading" to prevent duplicate loads
                LoadRating(o);
            MapSets.Add(mapStoragePath, o);
            dirty = true;
        }
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
        else
            LoadedMetadata.Maps[mapStoragePath] = new BeatmapMetadata(beatmap, GetWriteTime(mapStoragePath));
        dirty = true;
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
    // this is not good anymore
    // it doesn't properly consider library paths
    public string RelativePath(string path) => path == null ? null : Path.GetRelativePath(AbsolutePath, path);

    public Beatmap LoadMap(BeatmapMetadata metadata) => LoadMapFromId(metadata.Id);
    public void Save(Beatmap beatmap) => beatmap.SaveToDisk(this);
    public Beatmap LoadMapFromId(string mapId) => LoadMapForPlay(LoadedMetadata.Maps.FirstOrDefault(e => e.Value.Id == mapId).Key);
    public Beatmap LoadMapForPlay(string mapStoragePath)
    {
        try
        {
            var fullPath = GetFullPath(mapStoragePath);
            using var stream = GetStream(mapStoragePath) ?? throw new FileNotFoundException(fullPath);
            foreach (var format in BeatmapFormat.Formats)
            {
                if (format.CanReadFile(fullPath))
                    return format.Load(stream, mapStoragePath, fullPath, false, true);
            }
        }
        catch (Exception ex)
        {
            var msg = $"Failed to load beatmap {mapStoragePath}";
            Logger.Error(ex, msg);
            Util.Palette.ShowMessage(msg);
        }
        return null;
    }
    Beatmap LoadMapForMetadata(string mapStoragePath, bool savingPlanned)
    {
        var fullPath = GetFullPath(mapStoragePath);
        var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var metadataOnly = !savingPlanned; // don't need to load notes if we are just reading metadata without saving

        foreach (var format in BeatmapFormat.Formats)
        {
            if (format.CanReadFile(mapStoragePath))
            {
                if (!format.CanSave && savingPlanned)
                    throw new NotImplementedException($"Cannot load a {format.Name} file for editing");
                return format.TryLoad(stream, mapStoragePath, fullPath, metadataOnly, false);
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

    public IReadOnlyList<MapSetEntry> GetMapSet(Beatmap beatmap) => MapSets[beatmap];
}