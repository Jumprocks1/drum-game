using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using osu.Framework.Logging;

namespace DrumGame.Game.Stores.Repositories;

public class PpfRepoRefresher : RepoRefresherBase
{
    readonly HttpClient Client = new();
    public PpfRepoRefresher(RepositoryDefinition repo) : base(repo) { }

    string PageUrl(int page) => $"{Repo.Url}/getRight.php?p={page + 1}";
    public override async Task<List<JsonRepositoryBeatmap>> DownloadPage(int page, bool _)
    {
        var url = PageUrl(page);
        Logger.Log($"Downloading {url}", level: LogLevel.Important);

        var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync();
        text = CleanXml(text);

        var spl = Split(text);
        var maps = new List<JsonRepositoryBeatmap>();

        foreach (var match in spl)
        {
            var newMap = new JsonRepositoryBeatmap
            {
                DownloadUrl = $"{Repo.Url}/upload/{match.Groups[1].Value}"
            };
            var innerText = match.Groups[2].Value;
            var secondaryMatch = new Regex("<a href=\"(.*?)\">.*?<span class=\"list-title\">(.*?)</span><span class=\"list-artist\">(.*?)</span>").Match(innerText);
            newMap.Url = $"{Repo.Url}/{secondaryMatch.Groups[1].Value}";
            newMap.Title = secondaryMatch.Groups[2].Value;
            newMap.Artist = secondaryMatch.Groups[3].Value;
            newMap.Mapper = Repo.GetMapper(new Regex(@"\?simfile=(.+?)\/").Match(newMap.Url).Groups[1].Value);

            if (Match(newMap, Repo.IndexRegex, out var indexMatch)
                && int.TryParse(indexMatch.Groups[1].Value, out var i))
                newMap.Index = i;

            maps.Add(newMap);
        }

        return maps;
    }


    static IEnumerable<Match> Split(string xml) => new Regex(@"<li><input.*? value=""(.+?)"">(.+?)</li>").Matches(xml);
}

public class QudamRepoRefresher : RepoRefresherBase
{
    public QudamRepoRefresher(RepositoryDefinition repo) : base(repo) { }

    async Task<JsonRepositoryBeatmap> DownloadEntry(string url)
    {
        Logger.Log($"Downloading {url}", level: LogLevel.Important);
        const string startTag = "<article class=\"main-body\">";
        const string endTag = "</article>";


        var response = await HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync();

        var articleStart = text.IndexOf(startTag) + startTag.Length;
        var articleEnd = text.IndexOf(endTag, articleStart);
        var article = text[articleStart..articleEnd];

        var map = new JsonRepositoryBeatmap();

        var title = GetFirstGroup(article, "<h1 id=\"entry-title\">(.+?)</h1>");
        map.Url = url;
        map.DownloadUrl = GetFirstGroup(article, "<div class=\"inner-contents\">[^s^S]*?<a href=\"(.+?)\"");
        map.PublishedOn = DateTimeOffset.Parse(GetFirstGroup(article, "<span data-newdate=\"(.+?)\"></span>"));
        map.UpdatedOn = DateTimeOffset.Parse(GetFirstGroup(article, "<time id=\"modified-datetime\" datetime=\"(.+?)\">"));
        map.Mapper = Repo.Mapper;
        var youtubeId = GetFirstGroup(article, "src=\"https://www.youtube.com/embed/(.+?)\"");
        map.PreviewUrl = $"https://www.youtube.com/watch?v={youtubeId}";
        var titleSpl = title.Split("曲目");
        if (titleSpl.Length != 2) titleSpl = title.Split("曲");
        if (titleSpl.Length != 2)
        {
            Logger.Log($"Non-DTX post: {url}", level: LogLevel.Important);
            return null;
        }
        map.Index = int.Parse(titleSpl[0]);
        map.Title = titleSpl[1];

        if (map.Title[0] == '「' && map.Title[^1] == '」')
            map.Title = map.Title[1..^1];

        var innerContent = GetFirstGroup(article, "<div class=\"inner-contents\">([\\s\\S]+?)<div class=\"fc2button-clap\"");
        innerContent = StripXml(innerContent);

        var singer = GetFirstGroup(innerContent, "(?:うた|歌手)：?(.+)\n");
        if (singer == "初音ミク") singer = null; // don't want hatsune miku for artist
        var lyrics = GetFirstGroup(innerContent, "作詞：?(.+)\n");
        var comp = GetFirstGroup(innerContent, "作曲：?(.+)\n");
        var arrangement = GetFirstGroup(innerContent, "編曲：(.+)\n");

        map.Artist = singer ?? comp ?? lyrics ?? arrangement;
        map.Comments = innerContent;
        return map;
    }

    public override async Task<List<JsonRepositoryBeatmap>> DownloadPage(int page, bool force)
    {
        var url = PagedUrl(page);
        Logger.Log($"Downloading {url}", level: LogLevel.Important);

        var text = await Download(url);

        var maps = new List<JsonRepositoryBeatmap>();

        // <a class="grid-anchor" href="http://qudamnet.blog41.fc2.com/blog-entry-1708.html">
        var reg = new Regex("<a class=\"grid-anchor\" href=\"(http://qudamnet.blog41.fc2.com/[^\"]+)\">");
        foreach (var match in reg.Matches(text).Cast<Match>())
        {
            var entryUrl = match.Groups[1].Value;
            try
            {
                var map = await DownloadEntry(entryUrl);
                if (!force && AlreadyContains(map)) return maps; // stop early to save network
                if (map != null)
                    maps.Add(map);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Error while downloading {entryUrl}");
            }
            await Task.Delay(100);
        }

        return maps;
    }
}



public class FurukonRequestRefresher : RepoRefresherBase
{
    public FurukonRequestRefresher(RepositoryDefinition repo) : base(repo) { }
    public override async Task Refresh()
    {

        var text = await Download(Repo.Url);

        const string tableOpen = "<table class=\"table table-bordered table-sm table-striped\" id=\"songlist\">";

        var table = GetFirstGroup(text, $"{tableOpen}([\\s\\S]+?)</table>");

        var refreshedMaps = new List<JsonRepositoryBeatmap>();
        var cellMatch = new Regex("<td>(.*?)</td>");
        foreach (var row in ForeachFirstGroup(table, "<tr>(.+?)</tr>"))
        {
            var cells = ForeachFirstGroup(row, cellMatch).ToList();
            // columns: copy code, folder, title, artist, comment, duration
            var comments = $"Folder: {cells[3]}\nLength: {cells[5]}";
            if (!string.IsNullOrWhiteSpace(cells[4]))
                comments += $"\n{cells[4].Trim()}";
            var title = StripXml(cells[1]).TrimEnd('⭐'); // remove star request text
            refreshedMaps.Add(new JsonRepositoryBeatmap
            {
                Comments = comments,
                Title = title,
                Artist = cells[2],
            });
        }
        Cache.Maps = new(); // clear maps
        LoadedMaps.Clear();
        AddMapsToCache(refreshedMaps);
        Cache.Refreshed = DateTimeOffset.UtcNow;
        Cache.Save();
        Dispose();
    }
}
