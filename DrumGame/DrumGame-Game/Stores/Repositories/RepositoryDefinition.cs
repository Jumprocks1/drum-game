using System.Collections.Generic;
using System.IO;
using System.Linq;
using DrumGame.Game.Utils;
using Newtonsoft.Json;

namespace DrumGame.Game.Stores.Repositories;

public class RegexDefinition
{
    public enum RegexTarget
    {
        Content,
        Title,
        Url,
        XmlStrippedContent,
    }
    public RegexTarget Target;
    public string Expr;
}

public class RepositoryDefinition
{
    public string Url { get; set; }
    public string PagedUrl { get; set; }
    public string MapsPath { get; set; }
    public string DownloadUrlPrefix { get; set; }
    public string UrlPrefix { get; set; }
    public string DownloadUrlPath { get; set; }
    public string Title { get; set; }
    public string Type { get; set; }
    public RegexDefinition[] DownloadUrlRegex { get; set; }
    public RegexDefinition[] ArtistRegex { get; set; }
    public RegexDefinition[] TitleRegex { get; set; }
    public RegexDefinition[] IndexRegex { get; set; }
    public double Order { get; set; }

    public Dictionary<string, string> Mappers { get; set; }
    public string Mapper { get; set; }

    public string GetMapper(JsonRepositoryBeatmap map) => GetMapper(map.Mapper);
    public string GetMapper(string mapper) // returns convert mapper name based on mapper field in repo definition
    {
        if (mapper == null) return null;
        return Mappers?.GetValueOrDefault(mapper) ?? mapper;
    }

    [JsonIgnore] public string Source;
    [JsonIgnore] public RepositoryCache Cache;

    [JsonIgnore] public int? Count => Cache?.Maps.Count;

    [JsonIgnore] string CachePath => $"{(Util.Resources.GetDirectory("repositories/cache").FullName)}/{Path.GetFileName(Source)}";

    public void TryLoadCache()
    {
        if (Cache != null) return;
        var cachePath = CachePath;
        if (File.Exists(cachePath))
            Cache = RepositoryCache.FromFile(cachePath);
    }

    public RepositoryCache GetCache()
    {
        TryLoadCache();
        if (Cache == null) Cache = new RepositoryCache(CachePath);
        return Cache;
    }

    public IEnumerable<JsonRepositoryBeatmap> Filtered(string search) => GenericFilterer<JsonRepositoryBeatmap>.Filter(Cache.Maps, search);

    public bool CanRefresh => Type == "blogspot" || Type == "xml" || Type == "youtube" || Type == "json";

    public RepoRefresherBase GetRefresher()
    {
        if (Title == "Hapadona")
            return new HapadonaRepoRefresher(this);
        if (Title.StartsWith("PPF"))
            return new PpfRepoRefresher(this);
        if (Type == "blogspot")
            return new BlogspotRepoRefresher(this);
        if (Title.StartsWith("Furukon"))
            return new FurukonRequestRefresher(this);
        else if (Type == "xml")
            return new QudamRepoRefresher(this);
        else if (Type == "youtube")
            return new YouTubeRepoRefresher(this);
        else if (Type == "json")
            return new JsonRepoRefresher(this);
        return null;
    }

    public void Refresh()
    {
        var oldViewer = Util.Palette.GetModal<RepositoryViewer>();
        var refresher = GetRefresher();
        var task = refresher.Refresh();
        task.ContinueWith(_ =>
        {
            Util.Host.UpdateThread.Scheduler.Add(() =>
            {
                if (oldViewer == null || !oldViewer.IsAlive)
                    Util.Palette.PushNew<RepositoryViewer>();
                else oldViewer.UpdateSearch();
                Util.Palette.ShowMessage($"Refresh of {Title} complete");
            });
        });
    }
    public void RefreshPage(int page)
    {
        var refresher = GetRefresher();
        var task = refresher.RefreshPage(page);
        task.ContinueWith(_ =>
        {
            Util.Host.UpdateThread.Scheduler.Add(() =>
            {
                Util.Palette.PushNew<RepositoryViewer>();
                Util.Palette.ShowMessage($"Refresh of {Title} complete");
            });
        });
    }
}