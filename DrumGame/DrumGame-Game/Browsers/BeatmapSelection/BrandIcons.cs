using DrumGame.Game.API;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Interfaces;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Browsers.BeatmapSelection;

public class YouTubeIcon : SpriteIcon, IBeatmapIcon
{
    string VideoID;
    public YouTubeIcon(string videoId)
    {
        VideoID = videoId;
        Icon = FontAwesome.Brands.Youtube;
        Colour = Colour4.Red;
    }
    public string Url => $"https://youtube.com/watch?v={VideoID}";

    public static IBeatmapIcon TryConstruct(Beatmap beatmap, float size)
    {
        if (beatmap.YouTubeID != null)
            return new YouTubeIcon(beatmap.YouTubeID) { Width = size, Height = size };
        else return null;
    }
}

public class OtotoyIcon : SpriteText, IBeatmapIcon
{
    long AlbumID;
    public OtotoyIcon(long albumId, float height)
    {
        AlbumID = albumId;
        Text = "O";
        Width = height;
        Height = height;
        Font = FrameworkFont.Regular.With(size: height);
    }
    public string Url => $"https://ototoy.jp/_/default/p/{AlbumID}";

    public static IBeatmapIcon TryConstruct(Beatmap beatmap, float size)
    {
        if (beatmap.OtotoyAlbumID is long albumId)
            return new OtotoyIcon(albumId, size);
        else return null;
    }
}

public class SoundCloudIcon : SpriteIcon, IHasUrl
{
    string URI;
    public SoundCloudIcon(string uri)
    {
        URI = uri;
        Icon = FontAwesome.Brands.Soundcloud;
        Colour = new Colour4(242, 110, 35, 255);
    }
    public string Url => $"https://soundcloud.com/{URI}";
}

public class AmazonIcon : SpriteIcon, IBeatmapIcon
{
    string ASIN;
    public AmazonIcon(string asin)
    {
        ASIN = asin;
        Icon = FontAwesome.Brands.Amazon;
    }
    public string Url => $"https://amazon.com/dp/{ASIN}";


    public static IBeatmapIcon TryConstruct(Beatmap beatmap, float size)
    {
        if (beatmap.AmazonASIN != null)
            return new AmazonIcon(beatmap.AmazonASIN) { Width = size, Height = size };
        else return null;
    }
}
public class BandcampIcon : SpriteIcon, IBeatmapIcon
{
    string Artist;
    string Track;
    public BandcampIcon(string artist, string track = null)
    {
        Artist = artist;
        Track = track;
        Icon = FontAwesome.Brands.Bandcamp;
        Colour = new Colour4(29, 160, 195, 255);
    }
    public string Url => $"https://{Artist}.bandcamp.com{(Track == null ? "" : $"/track/{Track}")}";

    public static IBeatmapIcon TryConstruct(Beatmap beatmap, float size)
    {
        if (beatmap.BandcampArtist != null)
            return new BandcampIcon(beatmap.BandcampArtist, beatmap.BandcampTrack) { Width = size, Height = size };
        else return null;
    }
}

public class SpotifyIcon : SpriteIcon, IBeatmapIcon
{
    Beatmap Beatmap;
    public SpotifyIcon(Beatmap beatmap)
    {
        Beatmap = beatmap;
        Icon = FontAwesome.Brands.Spotify;
        Colour = new Colour4(30, 215, 96, 255); // from Spotify's official design guidelines
    }
    public string Url => Spotify.SpotifyReference.From(Beatmap.Spotify).Url;

    public static IBeatmapIcon TryConstruct(Beatmap beatmap, float size)
    {
        if (beatmap.Spotify != null)
            return new SpotifyIcon(beatmap) { Width = size, Height = size };
        else return null;
    }
}