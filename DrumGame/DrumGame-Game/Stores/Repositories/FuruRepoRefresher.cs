using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.ProjectModel;

namespace DrumGame.Game.Stores.Repositories;

public class FuruRepoRefresher(RepositoryDefinition repo) : JsonRepoRefresher(repo)
{
    public override JsonRepositoryBeatmap ParseMap(JObject e)
    {
        // column 8 + 7x for difficulty, 5 maximum total difficulties
        var difficulties = new List<string>();
        var c = e.GetValue("c");
        for (var i = 0; i < 5; i++)
        {
            var cell = c[8 + 7 * i];
            if (cell.Type == JTokenType.Object)
            {
                var diff = cell.GetValue<string>("v");
                if (!string.IsNullOrWhiteSpace(diff))
                    difficulties.Add(diff);
            }
        }
        return new JsonRepositoryBeatmap
        {
            Title = GetString(e, Repo.TitlePath),
            Artist = GetString(e, Repo.ArtistPath),
            Index = long.Parse(c[1].GetValue<string>("v").Split(' ')[0]),
            Comments = GetString(e, "c[5].v"),
            DownloadUrl = (Repo.DownloadUrlPrefix ?? "") + GetString(e, Repo.DownloadUrlPath),
            Url = (Repo.UrlPrefix ?? "") + Uri.EscapeDataString(GetString(e, Repo.PreviewUrlPath)),
            Difficulties = difficulties
        };
    }
}