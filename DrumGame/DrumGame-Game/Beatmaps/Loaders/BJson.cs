using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using DrumGame.Game.Channels;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Extensions.EnumExtensions;

namespace DrumGame.Game.Beatmaps.Loaders;

public class BJsonNote
{
    public double Time { get; set; } // in quarter notes/beats. Will be multiplied by TickRate and cast to integer
    public string Channel { get; set; }
    public string Modifier { get; set; }
    public string Sticking { get; set; }
    public double? Duration;
    public NoteModifiers GetModifiers() => Modifier switch
    {
        "accent" => NoteModifiers.Accented,
        "ghost" => NoteModifiers.Ghost,
        _ => NoteModifiers.None
    } | Sticking switch
    {
        "left" => NoteModifiers.Left,
        "right" => NoteModifiers.Right,
        _ => NoteModifiers.None
    };
    public static string ModifierString(NoteModifiers modifier) => modifier.HasFlagFast(NoteModifiers.Accented) ?
        "accent" : modifier.HasFlagFast(NoteModifiers.Ghost) ? "ghost" : null;
    public static string StickingString(NoteModifiers modifier) => modifier.HasFlagFast(NoteModifiers.Left) ?
        "left" : modifier.HasFlagFast(NoteModifiers.Right) ? "right" : null;
    static readonly Dictionary<string, DrumChannel> ChannelMapping = new()
    {
        { "bass", DrumChannel.BassDrum },
        { "hihat", DrumChannel.ClosedHiHat },
        { "snare", DrumChannel.Snare },
        { "crash", DrumChannel.Crash },
        { "half-hihat", DrumChannel.HalfOpenHiHat },
        { "open-hihat", DrumChannel.OpenHiHat },
        { "ride", DrumChannel.Ride },
        { "ride-bell", DrumChannel.RideBell },
        { "sidestick", DrumChannel.SideStick },
        { "high-tom", DrumChannel.SmallTom },
        { "mid-tom", DrumChannel.MediumTom },
        { "low-tom", DrumChannel.LargeTom },
        { "hihat-pedal", DrumChannel.HiHatPedal },
        { "splash", DrumChannel.Splash },
        { "china", DrumChannel.China },
        { "rim", DrumChannel.Rim },
    };
    static readonly Lazy<Dictionary<DrumChannel, string>> InverseMap = new(() =>
        ChannelMapping.ToDictionary(g => g.Value, g => g.Key));
    public static DrumChannel GetDrumChannel(string channel) => ChannelMapping.TryGetValue(channel, out var dc) ? dc
        : Enum.TryParse<DrumChannel>(channel, out var dc2) ? dc2 : DrumChannel.Snare;
    public DrumChannel GetDrumChannel() => GetDrumChannel(Channel);
    public static string GetChannelString(DrumChannel channel) => InverseMap.Value.GetValueOrDefault(channel) ?? channel.ToString();
}
public class DrumChannelConverter : JsonConverter<DrumChannel>
{
    public override void WriteJson(JsonWriter writer, DrumChannel value, JsonSerializer serializer)
        => writer.WriteValue(BJsonNote.GetChannelString(value));

    public override DrumChannel ReadJson(JsonReader reader, Type objectType, DrumChannel existingValue, bool hasExistingValue, JsonSerializer serializer)
        => BJsonNote.GetDrumChannel((string)reader.Value);
}
public class BJsonSource
{
    public string FullAssetPath(string asset)
    {
        if (asset == null) return null;
        if (Path.DirectorySeparatorChar == '/' && asset.Contains('\\'))
            asset = asset.Replace('\\', '/');
        return Util.SafeFullPath(asset, Directory);
    }
    public string Directory { get; set; } // this is the location where the BJson was loaded from. Useful for relative Audio/Midi paths
    string _absolutePath;
    public string AbsolutePath // make sure to also set MapStoragePath
    {
        get => _absolutePath; set
        {
            _absolutePath = value;
            Directory = Path.GetDirectoryName(value);
        }
    }
    public string MapStoragePath;
    public readonly string OriginalAbsolutePath;
    public string FilenameNoExt => Path.GetFileNameWithoutExtension(AbsolutePath);
    public string FilenameWithExt => Path.GetFileName(AbsolutePath);
    public BJsonSource(string absolutePath)
    {
        AbsolutePath = OriginalAbsolutePath = absolutePath;
    }
    public string Filename => AbsolutePath; // avoid using this, we should remove once we get to 0 references
    public string Extension => Path.GetExtension(AbsolutePath);
    public bool BJson => AbsolutePath.EndsWith(".bjson", true, CultureInfo.InvariantCulture);
}
public class Bookmark : IBeatTime
{
    public readonly double Time;
    public readonly string Title;
    public Bookmark(double time, string title)
    {
        Time = time;
        Title = title;
    }
    double IBeatTime.Time => Time;
    public Bookmark With(double time) => new(time, Title);
}
public class Annotation : IBeatTime
{
    public readonly double Time;
    public readonly string Text;
    public Annotation(double time, string text)
    {
        Time = time;
        Text = text;
    }
    double IBeatTime.Time => Time;
}
// this is serialized with NullValueHandling ignore
// for non-null defaults, we have to set DefaultValueHandling
public abstract class BJson
{
    // Ticks per quarter note
    public const int DefaultTickRate = 14400;

    public const double DefaultRelativeVolume = 0.3;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
    [DefaultValue(DefaultTickRate)]
    public int TickRate { get; set; }
    public string Description { get; set; }
    [JsonProperty(Order = 1)] // move to end since this is like 99% of the map
    public List<BJsonNote> Notes;
    public List<Bookmark> Bookmarks;
    public List<Annotation> Annotations;
    public double? RelativeVolume { get; set; }
    public string Audio { get; set; }
    public string DrumOnlyAudio { get; set; }
    public string PreviewAudio { get; set; }
    public string Video { get; set; }
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public double VideoOffset { get; set; }
    public string Image { get; set; }
    public string ImageUrl { get; set; }
    public string Id { get; set; }
    [JsonProperty("offset")]
    public virtual double StartOffset { get; set; }
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public virtual double LeadIn { get; set; }
    public float? SpacingMultiplier { get; set; }
    public JToken BPM { get; set; }
    public JToken MeasureConfig { get; set; }
    public string Title { get; set; }
    public string RomanTitle { get; set; } // typically romaji
    public string GetRomanTitle() => RomanTitle ?? Title;
    public string Artist { get; set; }
    public string RomanArtist { get; set; } // typically romaji
    public string GetRomanArtist() => RomanArtist ?? Artist;
    public string Mapper { get; set; }
    public string Difficulty { get; set; }
    public string DifficultyName { get; set; }
    public string Tags { get; set; }
    public double? PreviewTime { get; set; }
    public string Spotify { get; set; }
    public string YouTubeID { get; set; }
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    // we may eventually want to make this nullable
    // in the event that we want an offset of 0, it's nice to explicity say "hey, I tried to set this, but it really is truly 0"
    public double YouTubeOffset { get; set; } // this is additional offset applied to correct the offset for YouTube audio
    public long? OtotoyAlbumID { get; set; }
    public string AmazonASIN { get; set; }
    public string BandcampArtist { get; set; }
    public string BandcampTrack { get; set; }
    public string MapSourceUrl { get; set; }
    public string SongSource { get; set; }
    public string Album { get; set; }
    public double PlayableDuration { get; set; }
    public double MedianBPM { get; set; }
    public DateTime? CreationTimeUtc { get; set; }
    public List<string> Links { get; set; }

    public void AddTags(string tags)
    {
        if (string.IsNullOrWhiteSpace(Tags))
        {
            Tags = tags;
            return;
        }
        var split = SplitTags();
        foreach (var tag in SplitTags(tags))
        {
            if (Array.IndexOf(split, tag) >= 0) continue;
            Tags += " " + tag;
        }
    }
    public string[] SplitTags() => SplitTags(Tags);
    public static string[] SplitTags(string tags) => tags?.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? new string[0];
}

