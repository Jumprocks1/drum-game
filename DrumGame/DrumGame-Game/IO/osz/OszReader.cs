using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace DrumGame.Game.IO.Osz;

public class OszReader : IDisposable
{
    public readonly Stream Stream;
    public readonly ZipArchive Archive;
    public OszReader(Stream stream)
    {
        Stream = stream;
        Archive = new ZipArchive(stream);
    }

    public void Dispose()
    {
        Archive.Dispose();
        Stream.Dispose();
    }

    public IEnumerable<string> ListOsuFiles() => Archive.Entries.Select(e => e.FullName).Where(e => e.EndsWith(".osu"));
    public OsuFile ReadOsu(string osuFile) => new OsuFile(Archive.GetEntry(osuFile).Open());
}

