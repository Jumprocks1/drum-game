using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DrumGame.Game.API;

public class YouTubeAPI : OAuth<YouTubeAPI>, IOAuth<YouTubeAPI>
{
    public record VideosResponse
    {
        public List<VideoResponse> Items; public string NextPageToken;
    }
    public record VideoResponse
    {
        public string Id;
        public VideoSnippet Snippet;
    }
    public record VideoSnippet
    {
        public DateTime PublishedAt;
        public string ChannelId;
        public string Title;
        public string Description;
    }

    public static string TokenFile => "youtube-token";
    public static string HttpPrefix => "http://localhost:8888/";
    public static string RedirectUri => $"{HttpPrefix}callback";
    public static string ClientId => "890167227778-d0selfnr21sd8l3knrbhsekmdk8m8928.apps.googleusercontent.com";
    public static string TokenUrl => "https://oauth2.googleapis.com/token";
    public static string AuthCodeUrl => "https://accounts.google.com/o/oauth2/v2/auth";
    public static string ApiBase => "https://youtube.googleapis.com/youtube/v3/";

    public static string Scope => "https://www.googleapis.com/auth/youtube.readonly";
    // no idea why this is required
    // I know it's terrible to dump the client secret here, but there is no other option.
    // See https://stackoverflow.com/questions/60724690/using-google-oidc-with-code-flow-and-pkce
    // Google's docs literally say to put the client secret in the mobile/desktop app:
    //    https://developers.google.com/identity/protocols/oauth2/native-app#exchange-authorization-code
    public static string DummyClientSecret => "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";

    public static async Task<string> LookupChannel(string videoId) // most consistent way to get a channel ID is from a video
    {
        var url = $"videos?part=snippet&id={UrlEncode(videoId)}";

        var res = await Get<VideosResponse>(url);
        var video = res.Items[0];

        return video.Snippet.ChannelId;
    }
    public static string ChannelUploadsPlaylist(string channelId)
    {
        var chars = channelId.ToCharArray();
        if (chars[0] == 'U' && chars[1] == 'C')
        {
            chars[1] = 'U';
            return new string(chars);
        }
        throw new Exception("Invalid channel ID");
    }

    public static Task<VideosResponse> LookupVideos(string playlistId, string nextPage = null)
    {
        var url = $"playlistItems?part=snippet&maxResults=50&playlistId={playlistId}";
        if (nextPage != null)
            url += $"&pageToken={nextPage}";
        return Get<VideosResponse>(url);
    }
}