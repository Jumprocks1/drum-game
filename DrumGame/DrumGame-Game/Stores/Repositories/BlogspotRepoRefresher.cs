using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Logging;

namespace DrumGame.Game.Stores.Repositories;

public class HapadonaRepoRefresher : BlogspotRepoRefresher
{
    public HapadonaRepoRefresher(RepositoryDefinition repo) : base(repo) { }
    protected override bool SetArtistAndTitle(JsonRepositoryBeatmap newMap, JToken map)
    {
        var blogTitle = GetT(map, "title");
        var split = blogTitle.Split(" - ");
        if (split.Length == 2)
        {
            newMap.Artist = split[0];
            newMap.Title = split[1];
            return true;
        }
        split = blogTitle.Split(" / ");
        if (split.Length == 2)
        {
            newMap.Artist = split[0];
            newMap.Title = split[1];
            return true;
        }

        var bracketStart = blogTitle.IndexOf("[");
        var bracketEnd = blogTitle.LastIndexOf("]");
        if (bracketStart != -1 && bracketEnd != -1)
        {
            newMap.Title = blogTitle.Substring(0, bracketStart);
            newMap.Artist = blogTitle[(bracketStart + 1)..bracketEnd];
        }
        else
        {
            newMap.Title = blogTitle;
            newMap.Artist = "unknown";
        }
        return true;
    }
}

public class BlogspotRepoRefresher : RepoRefresherBase
{
    public BlogspotRepoRefresher(RepositoryDefinition repo) : base(repo) { }

    string PageUrl(int page) => $"{Repo.Url}/feeds/posts/default?alt=json&start-index={page * PageSize + 1}&max-results={PageSize}";

    protected virtual bool SetArtistAndTitle(JsonRepositoryBeatmap newMap, JToken map) => false;


    public override async Task<List<JsonRepositoryBeatmap>> DownloadPage(int page, bool _)
    {
        var url = PageUrl(page);
        Logger.Log($"Downloading {url}", level: LogLevel.Important);

        var json = await Download(url);
        var feed = (JObject)JsonConvert.DeserializeObject<JObject>(json).GetValue("feed");

        var maps = new List<JsonRepositoryBeatmap>();
        var entry = feed.GetValue("entry");
        if (entry == null) return maps;

        foreach (var map in feed.GetValue("entry"))
        {
            var newMap = new JsonRepositoryBeatmap();
            Content = GetT(map, "content");

            if (Match(newMap, Repo.TitleRegex, out var titleMatch))
                newMap.Title = titleMatch.Groups[1].Value;
            else
                newMap.Title = GetT(map, "title");

            if (Match(newMap, Repo.DownloadUrlRegex, out var linkMatch))
                newMap.DownloadUrl = linkMatch.Groups[1].Value;
            else
            {
                Logger.Log($"Missing download link for {GetT(map, "title")}", level: LogLevel.Important);
                continue;
            }

            if (Match(newMap, Repo.ArtistRegex, out var artistMatch))
                newMap.Artist = artistMatch.Groups[1].Value;
            else
            {
                if (!SetArtistAndTitle(newMap, map))
                {
                    Logger.Log($"Missing artist for {newMap.Title}", level: LogLevel.Important);
                    continue;
                }
            }


            if (Match(newMap, Repo.IndexRegex, out var indexMatch)
                && int.TryParse(indexMatch.Groups[1].Value, out var i))
            {
                newMap.Index = i;
            }

            newMap.Comments = XmlStrippedContent;
            if (newMap.Comments.Length > 300)
                newMap.Comments = newMap.Comments.Substring(0, 300);
            newMap.PublishedOn = DateTimeOffset.Parse(GetT(map, "published")).ToUniversalTime();
            newMap.UpdatedOn = DateTimeOffset.Parse(GetT(map, "updated")).ToUniversalTime();
            foreach (var t in map["link"])
            {
                if (t.Value<string>("rel") == "alternate")
                    newMap.Url = t.Value<string>("href");
            }

            newMap.Title = newMap.Title.Trim();
            newMap.Mapper = GetT(((JArray)map["author"])[0], "name");
            newMap.Artist = newMap.Artist?.Trim();

            var checkStr = newMap.Artist + " - ";
            if (newMap.Title.StartsWith(checkStr))
                newMap.Title = newMap.Title[checkStr.Length..];

            newMap.DownloadUrl = newMap.DownloadUrl?.Trim();
            maps.Add(newMap);
        }

        return maps;
    }

    protected static string GetT(JToken token, string key) => ((JObject)((JObject)token)[key])["$t"].Value<string>();
}

