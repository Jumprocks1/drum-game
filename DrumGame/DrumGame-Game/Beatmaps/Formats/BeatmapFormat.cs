using System;
using System.IO;
using DrumGame.Game.Beatmaps.Loaders;
using osu.Framework.Logging;

namespace DrumGame.Game.Beatmaps.Formats;

public abstract class BeatmapFormat
{
    // use when we need to iterate over all possible formats
    public readonly static BeatmapFormat[] Formats = [
        BJsonFormat.Instance,
        DtxFormat.Instance,
        SongIniFormat.Instance
    ];
    public abstract bool CanReadFile(string fullSourcePath);
    public abstract string Name { get; }
    public abstract string Tag { get; }
    public string MountTag => $"{Tag}-mount";
    public string ConvertTag => $"{Tag}-convert";
    public virtual bool CanSave => false;

    public Beatmap LoadExternal(string fullPath, bool metadataOnly, bool prepareForPlay)
    {
        using var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Load(stream, null, fullPath, metadataOnly, prepareForPlay);
    }
    // this should always be wrapped in a try-catch
    // mapStoragePath can be null
    public Beatmap Load(Stream stream, string mapStoragePath, string fullPath, bool metadataOnly, bool prepareForPlay)
    {
        if (prepareForPlay && metadataOnly) throw new Exception("Cannot prepare for play with metadata only");
        var o = LoadInternal(stream, fullPath, metadataOnly, prepareForPlay);
        o.DisableSaving |= metadataOnly; // if we only loaded metadata, prevent saving
        if (prepareForPlay)
        {
            o.LoadMissingDefaults();
        }
        o.Source = new BJsonSource(fullPath, this)
        {
            MapStoragePath = mapStoragePath
        };
        return o;
    }
    // only called by `Load`
    protected abstract Beatmap LoadInternal(Stream stream, string fullPath, bool metadataOnly, bool prepareForPlay);

    public Beatmap TryLoad(Stream stream, string mapStoragePath, string fullPath, bool metadataOnly, bool prepareForPlay)
    {
        Beatmap o;
        try
        {
            o = Load(stream, mapStoragePath, fullPath, metadataOnly, prepareForPlay);
        }
        catch (Exception e)
        {
            o = Beatmap.Create();
            o.Description = $"Failed to load {Name} file.\n{e}";
            o.Title = fullPath;
            o.Tags = $"{Tag}-failed-load";
            o.DisableSaving = true;
            o.Source = new BJsonSource(fullPath, this)
            {
                MapStoragePath = mapStoragePath
            };
            Logger.Error(e, $"Failed to load {fullPath}");
        }
        return o;
    }
}