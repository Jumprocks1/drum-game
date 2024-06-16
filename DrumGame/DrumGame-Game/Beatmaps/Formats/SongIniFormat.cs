using System;
using System.IO;
using DrumGame.Game.Beatmaps.Loaders;

namespace DrumGame.Game.Beatmaps.Formats;


public class SongIniFormat : BeatmapFormat
{
    public static readonly SongIniFormat Instance = new();
    SongIniFormat() { }

    public override string Name => "Song.ini";
    public override string Tag => "song-ini";
    public override bool CanReadFile(string fullSourcePath) =>
        Path.GetFileName(fullSourcePath).Equals("song.ini", StringComparison.OrdinalIgnoreCase);
    protected override Beatmap LoadInternal(Stream stream, string fullPath, bool metadataOnly, bool prepareForPlay)
        => SongIniLoader.LoadMounted(stream, fullPath, metadataOnly);
}