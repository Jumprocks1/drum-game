using System;
using System.Collections.Generic;
using DrumGame.Game.Beatmaps.Data;

namespace DrumGame.Game.Beatmaps.Loaders;

// TODO might be better to wrap dict instead of inherit
public class NotePresets : Dictionary<string, NotePreset>
{
    public event Action<NotePreset, string> PresetChanged;
    public event Action<NotePreset> PresetAdded;
    public event Action<NotePreset> PresetRemoved;
    public NotePreset Get(string key) => this.GetValueOrDefault(key);
    public void TriggerPresetChanged(NotePreset preset, string property)
    {
        PresetChanged?.Invoke(preset, property);
    }
    public void AddInternal(NotePreset preset) // skips events
    {
        Add(preset.Key, preset);
    }
    public void Add(NotePreset preset)
    {
        Add(preset.Key, preset);
        preset.Register();
        PresetAdded?.Invoke(preset);
    }
    public void Register()
    {
        foreach (var preset in this.Values)
            preset.Register();
    }
    public void RemovePreset(string key)
    {
        if (Remove(key, out var preset))
        {
            PresetRemoved?.Invoke(preset);
            preset.Unregister();
        }
    }

    public void ChangeKey(NotePreset preset, string key)
    {
        RemoveInternal(preset);
        preset.Key = key;
        AddInternal(preset);
        TriggerPresetChanged(preset, nameof(NotePreset.Key));
    }

    public void Remove(NotePreset e) => RemovePreset(e.Key);
    public void RemoveInternal(string key) => Remove(key);
    public void RemoveInternal(NotePreset preset) => RemoveInternal(preset.Key);
}