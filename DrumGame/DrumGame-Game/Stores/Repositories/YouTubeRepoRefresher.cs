using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DrumGame.Game.API;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Logging;

namespace DrumGame.Game.Stores.Repositories;

public class YouTubeRepoRefresher : RepoRefresherBase
{
    // for this, we expect a repo url like https://www.youtube.com/playlist?list=UU2AA4_V2LxQiOemKARZzTmw
    public YouTubeRepoRefresher(RepositoryDefinition repo) : base(repo) { }

    // we use this to track if we can safely download the next page
    // the problem is that we can't request a specific page from the YouTube API, so we have to make sure we track the next token
    // we only want to use that next token if the page != 0 and the page matches nextPage
    int nextPage = 0;
    string nextPageToken;

    public string PlaylistId
    {
        get
        {
            var regex = new Regex("UU[0-9a-zA-Z_-]{22}");
            var playlistId = regex.Match(Repo.Url).Groups[0].Value;
            return playlistId;
        }
    }

    public override async Task<List<JsonRepositoryBeatmap>> DownloadPage(int page, bool _)
    {
        var maps = new List<JsonRepositoryBeatmap>();

        string pageToken;
        if (page == 0)
            pageToken = null;
        else if (page == nextPage)
            pageToken = nextPageToken;
        else throw new Exception($"Cannot download page {page} from YouTube repository");

        var playlistId = PlaylistId;

        var videos = await YouTubeAPI.LookupVideos(playlistId, pageToken);
        nextPageToken = videos.NextPageToken;

        // Example for くだむの
        // "snippet": {
        //   "publishedAt": "2023-03-19T11:49:53Z",
        //   "channelId": "UC2AA4_V2LxQiOemKARZzTmw",
        //   "title": "【DTXMania】細胞プロミネンス",
        //   "description": "うた：アース・スター ドリーム　\n作詞：やしきん　\n作曲：eba　\n編曲：eba　\n譜面：https://1drv.ms/u/s!As94udVIq2nnnyNGGowU6gJfVKBV?e=3TvR6s",
        //   "channelTitle": "くだむのDTXおきばちゃんねる"
        // }

        foreach (var video in videos.Items)
        {
            var newMap = new JsonRepositoryBeatmap();
            Content = video.Snippet.Description;

            // if (Match(newMap, Repo.TitleRegex, out var titleMatch))
            //     newMap.Title = titleMatch.Groups[1].Value;
            // else
            //     newMap.Title = GetT(map, "title");

            // if (Match(newMap, Repo.DownloadUrlRegex, out var linkMatch))
            //     newMap.DownloadUrl = linkMatch.Groups[1].Value;
            // else
            // {
            //     Logger.Log($"Missing download link for {GetT(map, "title")}", level: LogLevel.Important);
            //     continue;
            // }

            // if (Match(newMap, Repo.ArtistRegex, out var artistMatch))
            //     newMap.Artist = artistMatch.Groups[1].Value;
            // else
            // {
            //     if (!SetArtistAndTitle(newMap, map))
            //     {
            //         Logger.Log($"Missing artist for {newMap.Title}", level: LogLevel.Important);
            //         continue;
            //     }
            // }


            // if (Match(newMap, Repo.IndexRegex, out var indexMatch)
            //     && int.TryParse(indexMatch.Groups[1].Value, out var i))
            // {
            //     newMap.Index = i;
            // }

            newMap.Comments = video.Snippet.Description;
            if (newMap.Comments.Length > 300)
                newMap.Comments = newMap.Comments.Substring(0, 300);
            newMap.PublishedOn = new DateTimeOffset(video.Snippet.PublishedAt).ToUniversalTime();
            // newMap.UpdatedOn = DateTimeOffset.Parse(GetT(map, "updated")).ToUniversalTime();
            // foreach (var t in map["link"])
            // {
            //     if (t.Value<string>("rel") == "alternate")
            //         newMap.Url = t.Value<string>("href");
            // }

            newMap.Title = newMap.Title.Trim();
            newMap.Mapper = Repo.Mapper;
            // newMap.Artist = newMap.Artist?.Trim();

            // var checkStr = newMap.Artist + " - ";
            // if (newMap.Title.StartsWith(checkStr))
            //     newMap.Title = newMap.Title[checkStr.Length..];

            // newMap.DownloadUrl = newMap.DownloadUrl?.Trim();
            // maps.Add(newMap);
        }

        return maps;
    }
}

