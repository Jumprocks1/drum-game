using System;
using System.IO;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Stores;
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

    public Beatmap LoadExternal(string fullPath, LoadMapIntent intent)
    {
        using var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Load(stream, null, fullPath, intent);
    }
    // this should always be wrapped in a try-catch
    // mapStoragePath can be null
    public Beatmap Load(Stream stream, string mapStoragePath, string fullPath, LoadMapIntent intent)
        => Load(stream, new LoadMapParameters(intent)
        {
            MapStoragePath = mapStoragePath,
            FullPath = fullPath
        });
    public Beatmap Load(Stream stream, LoadMapParameters parameters)
    {
        if (parameters.PrepareForPlay && parameters.MetadataOnly) throw new Exception("Cannot prepare for play with metadata only");
        var o = LoadInternal(stream, parameters);
        o.DisableSaving |= parameters.MetadataOnly; // if we only loaded metadata, prevent saving
        if (parameters.PrepareForPlay)
        {
            o.LoadMissingDefaults();
        }
        o.Source = new BJsonSource(parameters.FullPath, this)
        {
            MapStoragePath = parameters.MapStoragePath
        };
        return o;
    }
    // only called by `Load`
    protected abstract Beatmap LoadInternal(Stream stream, LoadMapParameters parameters);

    public Beatmap TryLoad(Stream stream, string mapStoragePath, string fullPath, LoadMapIntent intent)
    {
        Beatmap o;
        try
        {
            o = Load(stream, mapStoragePath, fullPath, intent);
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