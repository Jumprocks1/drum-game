using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using osu.Framework.Logging;

namespace DrumGame.Game.Stores;

// Only use with BindableJson
public class MapLibraries : IChangedEvent
{
    public List<MapLibrary> Sources; // name used in Json, avoid changing

    // this will always get triggered, just with an optional map provider parameter
    public event Action<MapLibrary> ChangedMapLibrary;
    public event Action Changed;

    [JsonIgnore] public List<MapLibrary> ValidLibraries;
    [JsonIgnore] public Dictionary<string, string> PathMapping;

    public void InvokeChanged(MapLibrary provider)
    {
        ChangedMapLibrary?.Invoke(provider);
        Changed?.Invoke();
    }

    public void Remove(MapLibrary provider)
    {
        if (provider.IsMain) throw new Exception("Cannot remove main map provider");
        provider.Disabled = true;
        Sources.Remove(provider);
        ValidLibraries.Remove(provider);
        PathMapping.Remove(provider.Name);
        InvokeChanged(provider);
    }
    public void Add(MapLibrary provider)
    {
        if (string.IsNullOrWhiteSpace(provider.Path)) throw new Exception("Path required when adding library");
        if (provider.ValidateAndLog())
        {
            if (Sources.Any(e => e.Name == provider.Name))
                Util.Palette.ShowMessage("A provider with that name already exists");
            else
            {
                Sources.Add(provider);
                ValidLibraries.Add(provider);
                PathMapping.Add(provider.Name, provider.AbsolutePath);
                InvokeChanged(provider);
            }
        }
    }
    public void Disable(MapLibrary provider)
    {
        provider.Disabled = true;
        InvokeChanged(provider);
    }
    public void Enable(MapLibrary provider)
    {
        provider.Disabled = false;
        InvokeChanged(provider);
    }
    public void InitAfterResources() // called after resources so we can handle relative paths
    {
        Sources ??= new();
        PathMapping ??= new();

        ValidLibraries = new(Sources.Count + 1);

        var foundMain = false;
        foreach (var source in Sources)
        {
            // should probably still add the ones that fail
            // currently they don't end up showing in the library view if they're invalid, which is a bit sad
            if (source.ValidateAndLog())
            {
                ValidLibraries.Add(source);
                if (source.IsMain)
                    foundMain = true;
                else
                    PathMapping.Add(source.Name, source.AbsolutePath);
            }
        }
        if (!foundMain)
            ValidLibraries.Insert(0, MapLibrary.Main);
    }
}


public class MapLibrary
{
    public string Name;
    [JsonIgnore] public string FriendlyName => Name ?? (IsMain ? "Main" : "");
    public override string ToString() => FriendlyName;
    public string Path;

    public bool IsInProvider(string mapStoragePath)
    {
        if (IsMain) return !mapStoragePath.StartsWith('$');
        return mapStoragePath.StartsWith(Prefix());
    }
    [JsonIgnore] public bool IsMain => Path == null;

    public int RecursiveDepth;
    public bool ScanBjson;
    public bool ScanDtx;
    public bool ScanSongIni;
    public bool Disabled;

    string _absolutePath;
    [JsonIgnore] public string AbsolutePath => _absolutePath ??= (Path == null ? null : Util.Resources.GetAbsolutePath(Path));

    public static MapLibrary Main => new();

    public string Prefix() => $"${Name}/";
    public bool Exists() => IsMain || Directory.Exists(AbsolutePath);
    public bool ValidateAndLog()
    {
        if (!IsMain && string.IsNullOrWhiteSpace(Name))
        {
            Logger.Log($"Missing name for {AbsolutePath}", level: LogLevel.Important);
            return false;
        }
        if (!Exists())
        {
            Logger.Log($"Directory not found: {AbsolutePath} for {this}", level: LogLevel.Important);
            return false;
        }
        return true;
    }

    public void AddWriteTimes(Dictionary<string, long> dict)
    {
        if (Disabled) return;
        if (IsMain)
        {
            var mainFiles = new DirectoryInfo(Util.MapStorage.AbsolutePath).GetFiles("*.bjson");
            foreach (var file in mainFiles)
                dict.Add(file.Name, file.LastWriteTimeUtc.Ticks);
            return;
        }
        var directory = new DirectoryInfo(AbsolutePath);
        var enumerationOptions = GetEnumerationOptions();
        // this is pretty dangerous, but I think it should work
        var rootPathLength = AbsolutePath.Length + 1; // add 1 for the slash
        var prefix = Prefix();
        if (ScanBjson)
            foreach (var file in directory.GetFiles("*.bjson", enumerationOptions))
                dict.Add($"{prefix}{file.FullName[rootPathLength..]}", file.LastWriteTimeUtc.Ticks);

        // note, this will not account for set.def write time
        // a proper solution would be to set the LastWriteTime to be Max(set.def,dtx)
        // this is tricky though since we won't know the contents of the set.def file

        // I think a "good-enough" solution would be to just check set.def write time on individual files when needed
        // This would just be when you hover the card in the selector
        if (ScanDtx)
            foreach (var file in directory.GetFiles("*.dtx", enumerationOptions))
                dict.Add($"{prefix}{file.FullName[rootPathLength..]}", file.LastWriteTimeUtc.Ticks);

        if (ScanSongIni)
            foreach (var file in directory.GetFiles("song.ini", enumerationOptions))
                dict.Add($"{prefix}{file.FullName[rootPathLength..]}", file.LastWriteTimeUtc.Ticks);
    }
    EnumerationOptions GetEnumerationOptions() => new()
    {
        MaxRecursionDepth = RecursiveDepth,
        MatchCasing = MatchCasing.CaseInsensitive,
        RecurseSubdirectories = RecursiveDepth != 0
    };
    public IEnumerable<string> GetMaps()
    {
        if (Disabled) return Enumerable.Empty<string>();
        if (IsMain)
        {
            var path = Util.MapStorage.AbsolutePath;
            var pathLength = path.Length + 1; // add 1 for the slash
            return Directory.GetFiles(path, "*.bjson").Select(e => e[pathLength..]);
        }
        var res = Enumerable.Empty<string>();
        var enumerationOptions = GetEnumerationOptions();
        var rootPathLength = AbsolutePath.Length + 1; // add 1 for the slash
        var prefix = Prefix();
        if (ScanBjson)
            res = res.Concat(Directory.GetFiles(AbsolutePath, "*.bjson", enumerationOptions).Select(e => $"{prefix}{e[rootPathLength..]}"));
        if (ScanDtx)
            res = res.Concat(Directory.GetFiles(AbsolutePath, "*.dtx", enumerationOptions).Select(e => $"{prefix}{e[rootPathLength..]}"));
        if (ScanSongIni)
            res = res.Concat(Directory.GetFiles(AbsolutePath, "song.ini", enumerationOptions).Select(e => $"{prefix}{e[rootPathLength..]}"));
        return res;
    }

    public (int?, int?, int?) CountMaps() // works even when disabled
    {
        try
        {
            if (IsMain)
                return (Directory.GetFiles(Util.MapStorage.AbsolutePath, "*.bjson").Length, null, null);
            var enumerationOptions = GetEnumerationOptions();
            int? bjsonCount = null;
            int? dtxCount = null;
            int? songIniCount = null;
            if (ScanBjson)
                bjsonCount = Directory.GetFiles(AbsolutePath, "*.bjson", enumerationOptions).Length;
            if (ScanDtx)
                dtxCount = Directory.GetFiles(AbsolutePath, "*.dtx", enumerationOptions).Length;
            if (ScanSongIni)
                songIniCount = Directory.GetFiles(AbsolutePath, "song.ini", enumerationOptions).Length;
            return (bjsonCount, dtxCount, songIniCount);
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Error loading {Name}");
            return (null, null, null);
        }
    }
}
