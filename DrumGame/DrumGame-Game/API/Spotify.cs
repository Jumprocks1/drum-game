using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using DrumGame.Game.Stores;
using Newtonsoft.Json;
using osu.Framework.Logging;

namespace DrumGame.Game.API;

// an instance of this implies we are authorized
// static methods are for when we don't know if we are authorized
// we should probably move away from instances all together, this would probably simplify some things
public class Spotify : OAuth<Spotify>, IOAuth<Spotify>
{
    public static string TokenFile => "spotify-token";
    public static string HttpPrefix => "http://localhost:8888/"; // must match what we have in Spotify's database
    public static string RedirectUri => $"{HttpPrefix}spotify-callback";
    public static string ClientId => "a2ea2c5f2e854a1a8bb7129ed0aa11bb";
    public static string TokenUrl => "https://accounts.spotify.com/api/token";
    public static string AuthCodeUrl => "https://accounts.spotify.com/authorize";
    public static string ApiBase => "https://api.spotify.com/v1/";

    public static async Task<string> GetAlbumImage(string id)
    {
        return (await Get<AlbumResponse>($"albums/{id}")).BestImage.Url;
    }
    public static async Task<string> GetTrackImage(string id)
    {
        return (await Get<TrackResponse>($"tracks/{id}")).Album.BestImage.Url;
    }
    public static async Task<TrackResponse> GetTrack(string id) => await Get<TrackResponse>($"tracks/{id}");

    public static async Task<List<TrackResponse>> Search(BeatmapMetadata metadata)
    {
        var search = $"track:\"{metadata.Title}\" artist:\"{metadata.Artist}\"";
        var url = $"search?type=track&q={UrlEncode(search)}";
        var resp = await Get<SearchResponse>(url);
        if (resp.Tracks.Items.Count == 0)
        {
            // less strict search
            search = $"{metadata.Title} {metadata.Artist}";
            resp = await Get<SearchResponse>($"search?type=track&q={UrlEncode(search)}");
        }
        return resp.Tracks.Items;
    }

    public enum SpotifyResource
    {
        Track,
        Album,
        Artist
    }
    public record SpotifyReference
    {
        public SpotifyResource Resource;
        public string Id;
        public string Url => $"https://open.spotify.com/{Resource.ToString().ToLowerInvariant()}/{Id}";
        public string Uri => $"spotify:{Resource.ToString().ToLowerInvariant()}:{Id}";
        public string ShortString => Resource == SpotifyResource.Track ? Id : Uri;

        // goal is to match all of the following:
        // https://open.spotify.com/track/0k4d41XiQAnPWfCxtGySqe
        // https://open.spotify.com/album/0k4d41XiQAnPWfCxtGySqe
        // https://open.spotify.com/artist/0k4d41XiQAnPWfCxtGySqe
        // spotify:track:0k4d41XiQAnPWfCxtGySqe
        // spotify:album:0k4d41XiQAnPWfCxtGySqe
        // spotify:artist:0k4d41XiQAnPWfCxtGySqe
        // see: https://developer.spotify.com/documentation/web-api/#spotify-uris-and-ids
        // ID is base-62, it should be 22 characters, but we will just assume 15 or greater
        const string idRegex = @"(?<id>[A-Za-z0-9]{15,})"; // note that both of these create capture groups
        const string resRegex = @"(?<res>track|album|artist)";
        public static Regex Regex => new Regex($"^{idRegex}$|^https?://open.spotify.com/{resRegex}/{idRegex}|^spotify:{resRegex}:{idRegex}$");
        public SpotifyReference(SpotifyResource resource, string id)
        {
            Resource = resource;
            Id = id;
        }
        public static SpotifyReference From(Match match)
        {
            if (!match.Success) return null;
            var id = match.Groups["id"].Value;
            var res = match.Groups["res"];
            if (!res.Success) // default to track if no resource type
                return new SpotifyReference(SpotifyResource.Track, id);
            return new SpotifyReference(Enum.Parse<SpotifyResource>(res.ValueSpan, true), id);
        }
        public static SpotifyReference From(string s) => From(Regex.Match(s));
    }

    public class TrackResponse
    {
        public AlbumResponse Album;
        public List<ArtistResponse> Artists;
        public string Name;
        public string Id;
        public string WebUrl => $"https://open.spotify.com/album/{Album.Id}?highlight=spotify:track:{Id}";
    }
    public record TrackSearchResponse
    {
        public List<TrackResponse> Items;
    }
    public class SearchResponse
    {
        public TrackSearchResponse Tracks;
    }
    public class ArtistResponse
    {
        public string Name;
        public override string ToString() => Name;
    }
    public class AlbumResponse
    {
        public List<ImageResponse> Images;
        public string album_type;
        public string Id;
        public bool Compilation => album_type == "compilation";
        public ImageResponse BestImage => Images.MaxBy(e => e.Height);
        // should always be 300
        public ImageResponse GoodImage => Images.Where(e => e.Height >= 200).MinBy(e => e.Height) ?? BestImage;
        public ImageResponse SmallImage => Images.MinBy(e => e.Height);
        public string Name;
    }

    public class ImageResponse
    {
        public int Height;
        public int Width;
        public string Url;
    }
}
