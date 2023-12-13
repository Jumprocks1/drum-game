using System;
using System.Collections.Generic;
using System.IO;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using osu.Framework.Logging;

namespace DrumGame.Game.Stores.Repositories;

public class RepositoryCache
{
    public RepositoryCache() { } // for Newtonsoft
    public static RepositoryCache FromFile(string absolutePath)
    {
        using var stream = File.OpenRead(absolutePath);
        using var sr = new StreamReader(stream);
        using var jsonTextReader = new JsonTextReader(sr);
        var serializer = new JsonSerializer();
        var res = serializer.Deserialize<RepositoryCache>(jsonTextReader);
        res.Source = absolutePath;
        return res;
    }

    public RepositoryCache(string source) // use this when the file doesn't exist
    {
        Source = source;
        Loaded = DateTimeOffset.UtcNow;
        Refreshed = DateTimeOffset.UtcNow;
        Maps = new();
    }

    public void Save()
    {
        if (Source == null) throw new Exception("Missing source.");
        using var stream = File.Open(Source, FileMode.Create);
        using var writer = new StreamWriter(stream);
        Logger.Log($"Saving repository cache to {Source}", level: LogLevel.Important);
        var serializer = new JsonSerializer
        {
            Formatting = Formatting.Indented,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };
        serializer.Serialize(writer, this);
    }

    [JsonIgnore] public string Source;

    public List<JsonRepositoryBeatmap> Maps;
    public DateTimeOffset Loaded; // initial load only
    public DateTimeOffset Refreshed; // latest refresh
}

public class JsonRepositoryBeatmap : ISearchable<JsonRepositoryBeatmap>
{
    [JsonIgnore] public string FullName => $"{Artist} - {Title}";
    [JsonIgnore] public bool CanDirectDownload => DownloadUrl != null && (DownloadUrl.EndsWith(".zip") || DownloadUrl.EndsWith(".7z") || DownloadUrl.EndsWith(".rar") || DownloadUrl.EndsWith(".bjson"));
    public string Title;
    public string Artist;
    public string Mapper;
    public string Comments;
    public long Index;
    public List<string> Difficulties { get; set; }
    public long Version { get; set; }
    public string Url { get; set; }
    public string DownloadUrl { get; set; }
    public string Image { get; set; }
    public string PreviewUrl { get; set; }
    public string GameplayUrl { get; set; }
    public DateTimeOffset? PublishedOn { get; set; }
    public DateTimeOffset? UpdatedOn { get; set; }
    public string Id { get; set; }

    [JsonIgnore]
    public bool HasSpecificDownload =>
        DownloadUrl != null && !DownloadUrl.StartsWith("https://drive.google.com/drive/u/0/folders/");
    [JsonIgnore] public string DownloadIdentifier => HasSpecificDownload ? DownloadUrl : FullName; // used for `downloaded.txt` list
    [JsonIgnore] public string NonNullId => Id ?? Url ?? DownloadUrl ?? $"{Artist} - {Title} - {Comments}";

    [JsonIgnore] string _filterString;
    [JsonIgnore] public string FilterString => _filterString ??= $"{Title} {Artist} {Mapper} {Comments}";

    [JsonIgnore] public bool Downloaded => DownloadedCache.Contains(this);

    public static FilterFieldInfo[] Fields { get; } = [
        "title", "artist", "mapper", "comments", "url",
        new("downloaded", "Example: <code>downloaded=0</> - only show maps that are not yet marked as downloaded."),
        "downloadurl", "index", "publishedon", "updatedon"
    ];

    public static void LoadField(string field) { }

    public static FilterAccessor GetAccessor(string field) => null;
}