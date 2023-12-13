using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Stores.Repositories;

public abstract class RepoRefresherBase : IDisposable
{
    protected readonly RepositoryDefinition Repo;
    protected readonly RepositoryCache Cache;
    HttpClient _httpClient;
    public HttpClient HttpClient => _httpClient ??= new();
    protected Dictionary<string, int> LoadedMaps = new();
    public const int PageSize = 50;

    public virtual void Dispose()
    {
        _httpClient?.Dispose();
    }

    string _strippedContent;
    string _content;
    protected string Content
    {
        get => _content; set
        {
            _content = value;
            _strippedContent = null;
        }
    }
    protected string XmlStrippedContent => _strippedContent ??= StripXml(_content);

    Dictionary<string, Regex> RegexCache = new();
    Regex GetRegex(string source)
    {
        if (RegexCache.TryGetValue(source, out var o)) return o;
        RegexCache[source] = o = new Regex(source);
        return o;
    }

    public bool Match(JsonRepositoryBeatmap map, RegexDefinition[] regex, out Match match)
    {
        if (regex == null)
        {
            match = null;
            return false;
        }
        foreach (var r in regex)
        {
            var target = r.Target switch
            {
                RegexDefinition.RegexTarget.Title => map.Title,
                RegexDefinition.RegexTarget.Url => map.Url,
                RegexDefinition.RegexTarget.XmlStrippedContent => XmlStrippedContent,
                _ => Content
            };
            if (target == null) continue;
            var reg = GetRegex(r.Expr);
            match = reg.Match(target);
            if (match.Success)
                return true;
        }
        match = null;
        return false;
    }

    public virtual Task<List<JsonRepositoryBeatmap>> DownloadPage(int page, bool force) => throw new NotImplementedException();

    public bool CanDownloadPage =>
        GetType().GetMethod(nameof(DownloadPage), BindingFlags.Instance | BindingFlags.Public).DeclaringType != typeof(RepoRefresherBase);

    public async Task RefreshPage(int page)
    {
        AddMapsToCache(await DownloadPage(page, true), true);
        Cache.Save();
        Dispose();
    }
    public virtual Task Refresh()
    {
        if (CanDownloadPage)
        {
            return PagedRefresh(e => DownloadPage(e, false));
        }
        throw new NotImplementedException();
    }
    public bool AlreadyContains(JsonRepositoryBeatmap newBeatmap) => LoadedMaps.ContainsKey(newBeatmap.NonNullId);
    protected async Task PagedRefresh(Func<int, Task<List<JsonRepositoryBeatmap>>> downloadPage)
    {
        var page = 0;
        var refreshedMaps = new List<JsonRepositoryBeatmap>();
        while (true)
        {
            var download = await downloadPage(page);
            if (download == null || download.Count == 0) break;
            refreshedMaps.AddRange(download);
            if (download.Any(AlreadyContains)) break;
            await Task.Delay(1000);
            page += 1;
        }
        AddMapsToCache(refreshedMaps);
        Cache.Refreshed = DateTimeOffset.UtcNow;
        Cache.Save();
        Dispose();
    }
    protected void AddMapsToCache(List<JsonRepositoryBeatmap> maps, bool sort = false)
    {
        if (maps == null) return;
        var addMaps = new List<JsonRepositoryBeatmap>(); // these are completely new, so we add them to the start of the cache list
        foreach (var map in maps)
        {
            if (LoadedMaps.TryGetValue(map.NonNullId, out var o))
                Cache.Maps[o] = map; // update cache
            else
                addMaps.Add(map);
        }
        if (sort)
        {
            Cache.Maps.AddRange(addMaps);
            Cache.Maps = Cache.Maps.OrderByDescending(e => e.Index).ToList();
        }
        else
        {
            Cache.Maps.InsertRange(0, addMaps);
        }
    }
    public async Task<string> Download(string url, bool cache = false)
    {
        if (cache)
        {
            var fileName = Util.MD5(url) + ".cache";
            var cachePath = Util.Resources.GetDirectory("repositories/cache").FullName;
            var path = cachePath + "/" + fileName;
            if (File.Exists(path))
                return File.ReadAllText(path);

            var resp = await HttpClient.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var res = await resp.Content.ReadAsStringAsync();
            File.WriteAllText(path, res);
            return res;
        }
        var response = await HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
    public RepoRefresherBase(RepositoryDefinition repo)
    {

        Repo = repo;
        Cache = Repo.GetCache();
        for (var i = 0; i < Cache.Maps.Count; i++)
        {
            var map = Cache.Maps[i];
            LoadedMaps[map.NonNullId] = i;
        }
    }

    protected static string CleanXml(string s)
    {
        s = new Regex(@"\n|\t").Replace(s, "");
        return s.Trim();
    }

    protected static string StripXml(string s)
    {
        s = new Regex("<br[^>]*>").Replace(s, "\n");
        s = new Regex("</p>").Replace(s, "\n");
        s = new Regex("<[^>]+>|&nbsp;").Replace(s, " ");
        s = new Regex(@"\s*\n\s*").Replace(s, "\n");
        s = new Regex(" +").Replace(s, " ");
        s = new Regex("&amp;").Replace(s, "&");
        return s.Trim();
    }

    protected string PagedUrl(int page) => Repo.PagedUrl.Replace("{PAGE}", page.ToString());
    public string GetFirstGroup(string source, string regex) => GetFirstGroup(source, GetRegex(regex));
    public IEnumerable<string> ForeachFirstGroup(string source, string regex) => ForeachFirstGroup(source, GetRegex(regex));
    public IEnumerable<string> ForeachFirstGroup(string source, Regex regex)
    {
        foreach (Match match in regex.Matches(source))
            yield return match.Groups[1].Value.Trim();
    }
    public string GetFirstGroup(string source, Regex regex)
    {
        var match = regex.Match(source);
        if (!match.Success) return null;
        return match.Groups[1].Value.Trim();
    }
}

