using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DrumGame.Game.Stores.Repositories;

public class JsonRepoRefresher : RepoRefresherBase
{
    public JsonRepoRefresher(RepositoryDefinition repo) : base(repo) { }
    public override async Task Refresh()
    {
        var json = JToken.Parse(await Download(Repo.Url));
        var refreshedMaps = new List<JsonRepositoryBeatmap>();

        IEnumerable<JObject> maps;

        var mapsToken = json.SelectToken(Repo.MapsPath);
        if (mapsToken is JArray ja) maps = mapsToken.Cast<JObject>();
        else if (mapsToken is JObject jo)
        {
            var e = (IEnumerable<KeyValuePair<string, JToken>>)jo;
            maps = e.Select(e =>
            {
                var token = (JObject)e.Value;
                token.Add("$key", e.Key);
                return token;
            });
        }
        else throw new Exception($"Unexpected token: {mapsToken}");

        foreach (var e in maps)
        {
            var diff = GetString(e, "difficultyString");
            var tags = GetString(e, "tags");
            var downloadUrl = GetString(e, "downloadUrl") ?? GetString(e, Repo.DownloadUrlPath);
            refreshedMaps.Add(new JsonRepositoryBeatmap
            {
                Title = GetString(e, "title"),
                Artist = GetString(e, "artist"),
                Comments = $"Difficulty: {diff}\nTags: {tags}",
                Mapper = GetString(e, "mapper"),
                PublishedOn = GetDateTime(e, "creationTimeUtc"),
                DownloadUrl = Repo.DownloadUrlPrefix + downloadUrl,
                Url = Repo.UrlPrefix + downloadUrl
            });
        }

        Cache.Maps = refreshedMaps.OrderByDescending(e => e.PublishedOn).ToList();
        Cache.Refreshed = DateTimeOffset.UtcNow;
        Cache.Save();
        Dispose();
    }

    public string GetString(JObject obj, string path)
    {
        var token = obj.SelectToken(path) ?? obj.SelectToken(char.ToUpper(path[0]) + path[1..]);
        return token?.ToString();
    }
    public DateTime? GetDateTime(JObject obj, string path)
    {
        var s = GetString(obj, path);
        if (s == null) return null;
        return DateTime.Parse(s);
    }
}

