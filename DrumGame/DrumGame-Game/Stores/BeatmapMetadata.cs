using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DrumGame.Game.Stores;

public class MetadataCache
{
    [JsonProperty(Required = Required.Always)]
    public int Version;
    [JsonProperty(Required = Required.Always)]
    public Dictionary<string, BeatmapMetadata> Maps;

    // parameterless constructor is used for JSON loading
    public static MetadataCache New() => new()
    {
        Version = BeatmapMetadata.Version,
        Maps = new()
    };
}

public class BeatmapMetadata
{
    public const int Version = 6;
    public string Id;
    public string Title;
    public string RomanTitle;
    public string RomanArtist;
    public string Artist;
    public string Mapper;
    public string Folder;
    public BeatmapDifficulty Difficulty;
    public string DifficultyString;
    public string Tags;
    public long WriteTime;
    public string Audio;
    public string Image;
    public string ImageUrl;
    [JsonIgnore] public string SHA; // not used yet
    public double Duration;
    public double BPM;
    public string BpmRange;
    public string MapSetId;
    public string PreviewAudio;
    public long CreationTime;
    [JsonIgnore] public int PlayCount => Util.MapStorage.GetPlayCount(Id);
    [JsonIgnore] public bool HasAudio; // depends on if the user has the audio files or not, loaded during runtime (not cached)
    // this is loaded from replay information. If we eventually upgrade to database metadata, we should be able to store this in the database
    // we don't want to store it in .cache.json since that gets deleted occasionally
    [JsonIgnore] public long PlayTime = -1; // -1 for not loaded, 0 for never played, otherwise UtcTicks
    [JsonIgnore] public int Rating = int.MinValue; // MinValue for not loaded, this is typically modified from a background thread
    [JsonIgnore] public bool RatingLoaded => Rating != int.MinValue;
    string _dtxLevel;
    [JsonIgnore] public string DtxLevel => _dtxLevel ??= Beatmap.FormatDtxLevel(GetDtxLevel()) ?? ""; // return empty string instead of null for caching
    // note, you can't search based on Difficulty right now
    // if we want to do that, we should create a BeatmapDifficulty => string mapping
    public string FilterString() => $"{Title} {Artist} {Mapper} {DifficultyString} {Tags} {RomanTitle} {RomanArtist}";
    public BeatmapMetadata() { } // used by Newtonsoft
    public void Update(Beatmap beatmap)
    {
        _dtxLevel = null; // reset cache if needed
        Id = beatmap.Id;
        MapSetId = beatmap.MapSetIdNonNull;
        Title = beatmap.Title ?? beatmap.Source.FilenameNoExt;
        Artist = beatmap.Artist;
        Mapper = beatmap.Mapper;
        Audio = beatmap.Audio;
        RomanTitle = beatmap.RomanTitle;
        RomanArtist = beatmap.RomanArtist;
        CreationTime = beatmap.CreationTimeUtc?.Ticks ?? 0;
        var mapStoragePath = beatmap.Source?.MapStoragePath;
        if (mapStoragePath != null)
        {
            WriteTime = Util.MapStorage.GetWriteTime(mapStoragePath);
            if (CreationTime == 0)
                CreationTime = Util.MapStorage.GetCreationTime(mapStoragePath);
        }
        DifficultyString = beatmap.DifficultyName ?? beatmap.Difficulty.ToDifficultyString();
        Difficulty = beatmap.Difficulty;
        Tags = beatmap.Tags;
        Image = beatmap.Image;
        ImageUrl = beatmap.ImageUrl;
        Duration = beatmap.PlayableDuration;
        BPM = beatmap.MedianBPM;
        BpmRange = beatmap.BpmRange;
        PreviewAudio = beatmap.PreviewAudio;
        if (beatmap.Source.MapStoragePath != null && beatmap.Source.MapStoragePath.StartsWith('$'))
        {
            // for .dtx files we go up 2 layers for this field
            // this is because the filename is not very useful (ie. mstr.dtx)
            // we also skip the first character to hide the $
            // It may seem weird splitting the logic like this, but in reality it works well
            Folder = Path.GetDirectoryName(beatmap.Source.MapStoragePath[1..]);
            if (beatmap.Source.Filename.EndsWith(".dtx", true, CultureInfo.InvariantCulture))
            {
                var secondParent = Path.GetDirectoryName(Folder);
                if (!string.IsNullOrWhiteSpace(secondParent))
                    Folder = secondParent;
            }
        }
    }
    public BeatmapMetadata(Beatmap beatmap)
    {
        Update(beatmap);
    }
    public string[] SplitTags() => Tags?.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];

    string GetDtxLevel() => Beatmap.GetDtxLevel(SplitTags());
}
