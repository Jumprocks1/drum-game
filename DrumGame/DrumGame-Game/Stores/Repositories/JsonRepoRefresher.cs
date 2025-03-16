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
        var comments = "";

        var diff = GetString(e, "difficultyString");
        if (diff != null)
            comments += $"Difficulty: {diff}\n";

        var tags = GetString(e, "tags");
        if (tags != null)
            comments += $"Tags: {tags}\n";

        var bpm = GetString(e, "bpm");
        if (bpm != null)
            comments += $"BPM: {bpm}\n";


        var downloadUrl = GetString(e, Repo.DownloadUrlPath, "downloadUrl", "download_url");
        var res = new JsonRepositoryBeatmap
        {
            Title = GetString(e, "title"),
            Artist = GetString(e, "artist"),
            Comments = comments.Trim(),
            Mapper = GetString(e, "mapper") ?? Repo.Mapper,
            PublishedOn = GetDateTime(e, "creationTimeUtc"),
            DownloadUrl = (Repo.DownloadUrlPrefix ?? "") + downloadUrl,
            Url = Repo.UrlPrefix + downloadUrl,
            Index = GetInt(e, Repo.IndexPath, "indexPath", "id") ?? default,
            UpdatedOn = GetDateTime(e, "publish_date")
        };

        if (diff == null && GetToken(e, "dtx_files") is JArray arr)
            res.Difficulties = arr.Select(e => e["level"].ToString()).ToList();

        return res;
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

            var mapsToken = Repo.MapsPath == null ? json : json.SelectToken(Repo.MapsPath);
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
                .ThenByDescending(e => e.Comments)
                .ThenByDescending(e => e.FullName)
                .ToList();
            Cache.Refreshed = DateTimeOffset.UtcNow;
            Cache.Save();
            Dispose();
        }
        catch (Exception e)
        {
            Util.Palette.UserError("Refresh failed", e);
        }
    }

    public JToken GetToken(JObject obj, params string[] paths)
    {
        foreach (var path in paths)
        {
            if (path == null) continue;
            var token = obj.SelectToken(path) ?? obj.SelectToken(char.ToUpper(path[0]) + path[1..]);
            if (token != null)
                return token;
        }
        return null;
    }

    public int? GetInt(JObject obj, params string[] paths) => int.TryParse(GetToken(obj, paths)?.ToString(), out var o) ? o : null;
    public string GetString(JObject obj, params string[] paths) => GetToken(obj, paths)?.ToString();
    public DateTime? GetDateTime(JObject obj, params string[] paths)
    {
        var s = GetString(obj, paths);
        if (s == null) return null;
        return DateTime.Parse(s);
    }
}

