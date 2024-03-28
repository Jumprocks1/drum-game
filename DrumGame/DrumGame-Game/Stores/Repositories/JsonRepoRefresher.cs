using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Stores.Repositories;

public class JsonRepoRefresher : RepoRefresherBase
{
    public JsonRepoRefresher(RepositoryDefinition repo) : base(repo) { }
    public virtual JsonRepositoryBeatmap ParseMap(JObject e)
    {
        var diff = GetString(e, "difficultyString");
        var tags = GetString(e, "tags");
        var downloadUrl = GetString(e, Repo.DownloadUrlPath ?? "downloadUrl");
        return new JsonRepositoryBeatmap
        {
            Title = GetString(e, "title"),
            Artist = GetString(e, "artist"),
            Comments = $"Difficulty: {diff}\nTags: {tags}",
            Mapper = GetString(e, "mapper"),
            PublishedOn = GetDateTime(e, "creationTimeUtc"),
            DownloadUrl = (Repo.DownloadUrlPrefix ?? "") + downloadUrl,
            Url = Repo.UrlPrefix + downloadUrl
        };
    }
    public override async Task Refresh()
    {
        try
        {
            var stringData = await Download(Repo.Url);
            const string prefix = "/*O_o*/\ngoogle.visualization.Query.setResponse(";
            if (stringData.StartsWith(prefix))
            {
                // removes prefix + `);` at the end
                stringData = stringData[prefix.Length..^2];
            }
            var json = JToken.Parse(stringData);
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

            if (!string.IsNullOrWhiteSpace(Repo.SortPath))
                maps = maps.OrderBy(e => e.SelectToken(Repo.SortPath)?.ToString());

            foreach (var e in maps) refreshedMaps.Add(ParseMap(e));
            Cache.Maps = refreshedMaps
                .OrderByDescending(e => e.Index)
                .ThenByDescending(e => e.PublishedOn)
                .ThenByDescending(e => e.Comments).ToList();
            Cache.Refreshed = DateTimeOffset.UtcNow;
            Cache.Save();
            Dispose();
        }
        catch (Exception e)
        {
            Util.Palette.UserError("Refresh failed", e);
        }
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

