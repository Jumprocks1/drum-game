using System;
using System.Globalization;
using System.IO;
using DrumGame.Game.Beatmaps.Loaders;

namespace DrumGame.Game.Beatmaps.Formats;


public class DtxFormat : BeatmapFormat
{
    public static readonly DtxFormat Instance = new();
    DtxFormat() { }

    public override string Name => "DTX";
    public override string Tag => "dtx";
    public override bool CanReadFile(string fullSourcePath) => fullSourcePath.EndsWith(".dtx", StringComparison.OrdinalIgnoreCase);
    protected override Beatmap LoadInternal(Stream stream, string fullPath, bool metadataOnly, bool prepareForPlay)
        => DtxLoader.LoadMounted(stream, fullPath, metadataOnly);

}