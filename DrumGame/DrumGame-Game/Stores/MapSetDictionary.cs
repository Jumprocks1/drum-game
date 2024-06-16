using System;
using System.Collections.Generic;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Browsers.BeatmapSelection;

namespace DrumGame.Game.Stores;


public struct MapSetEntry
{
    public string MapStoragePath;
    public BeatmapMetadata Metadata;
    public MapSetEntry(string mapStoragePath, BeatmapMetadata metadata)
    {
        MapStoragePath = mapStoragePath;
        Metadata = metadata;
    }

    public readonly void Deconstruct(out string mapStoragePath, out BeatmapMetadata metadata)
    {
        mapStoragePath = MapStoragePath;
        metadata = Metadata;
    }
}

// meant to behave similar to Dictionary<string, List<BeatmapMetadata>>
// seems to cost <200KB memory on my machine, seems worth
public class MapSetDictionary
{
    Dictionary<string, List<MapSetEntry>> Dictionary = new();

    public void Clear() => Dictionary.Clear();

    public IReadOnlyList<MapSetEntry> this[string mapSetId] => mapSetId == null ? null : Dictionary.GetValueOrDefault(mapSetId);
    public IReadOnlyList<MapSetEntry> this[BeatmapMetadata metadata] => this[metadata?.MapSetId];
    public IReadOnlyList<MapSetEntry> this[BeatmapSelectorMap map] => this[map?.LoadedMetadata?.MapSetId];
    public IReadOnlyList<MapSetEntry> this[Beatmap map] => this[map.MapSetIdNonNull];

    public void MapSetIdChanged(string mapStoragePath, BeatmapMetadata metadata, string oldId)
    {
        if (metadata.MapSetId == oldId) return;
        if (Dictionary.TryGetValue(oldId, out var oldSet))
            oldSet.RemoveAll(e => e.Metadata == metadata);
        Add(mapStoragePath, metadata);
    }
    public void Remove(BeatmapMetadata metadata)
    {
        if (Dictionary.TryGetValue(metadata.MapSetId, out var oldSet))
            oldSet.RemoveAll(e => e.Metadata == metadata);
    }

    public void Add(string mapStoragePath, BeatmapMetadata metadata)
    {
        var e = new MapSetEntry(mapStoragePath, metadata);
        if (Dictionary.TryGetValue(metadata.MapSetId, out var mapSet))
            mapSet.Add(e);
        else
            Dictionary[metadata.MapSetId] = [e];
    }
}