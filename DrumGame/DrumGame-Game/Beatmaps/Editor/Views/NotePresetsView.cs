using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Channels;
using DrumGame.Game.Components;
using DrumGame.Game.Containers;
using DrumGame.Game.Modals;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;

namespace DrumGame.Game.Beatmaps.Editor.Views;

public class NotePresetsView : RequestModal
{
    class NotePresetUsage
    {
        List<HitObject> HitObjects = new();
        public void Add(HitObject hitObject)
        {
            HitObjects.Add(hitObject);
            if (DefaultChannel == null) DefaultChannel = hitObject.Channel;
            else if (DefaultChannel != hitObject.Channel)
            {
                DefaultChannel = DrumChannel.None;
            }
        }
        public int Count => HitObjects.Count;

        public DrumChannel? DefaultChannel; // null implies unset, None implies 2 different channels found
    }
    readonly BeatmapEditor Editor;
    Beatmap Beatmap => Editor.Beatmap;
    Dictionary<NotePreset, NotePresetUsage> _usages;
    Dictionary<NotePreset, NotePresetUsage> Usages
    {
        get
        {
            if (_usages != null) return _usages;
            _usages = new();
            foreach (var hit in Beatmap.HitObjects)
            {
                var preset = hit.Data.Preset;
                if (preset == null) continue;
                if (!Usages.TryGetValue(preset, out var o))
                {
                    o = new();
                    Usages[preset] = o;
                }
                o.Add(hit);
            }
            return _usages;
        }
    }

    int usageCount(NotePreset preset) => Usages.TryGetValue(preset, out var o) ? o.Count : 0;

    void RequestChangeChannel(NotePreset preset)
    {
        var currentChannel = DrumChannel.None;
        if (Usages.TryGetValue(preset, out var o))
        {
            if (o.DefaultChannel is DrumChannel dc && dc != DrumChannel.None)
                currentChannel = dc;
        }
        Util.Palette.Request(new RequestConfig
        {
            Title = $"Changing Channel for {preset}",
            Description = "This will change all notes that use this preset",
            Field = new EnumFieldConfig<DrumChannel>
            {
                Label = "Channel",
                DefaultValue = currentChannel,
                OnCommit = v =>
                {
                    preset.Channel = v;
                    if (v != DrumChannel.None)
                    {
                        Editor.PushChange(new NoteBeatmapChange(() =>
                        {
                            for (var i = 0; i < Beatmap.HitObjects.Count; i++)
                            {
                                if (Beatmap.HitObjects[i].Preset == preset)
                                    Beatmap.HitObjects[i] = Beatmap.HitObjects[i].With(v);
                            }
                            Beatmap.RemoveDuplicates();
                        }, $"change {preset} channel to {v}"));
                    }
                    Table.UpdateRow(preset);
                }
            },
        });
    }
    void RequestChangeModifiers(NotePreset preset)
    {
        Util.Palette.Request(new RequestConfig
        {
            Title = $"Changing Modifiers for {preset}",
            Description = "This will change all notes that use this preset",
            Field = new EnumFieldConfig<NoteModifiers>
            {
                Values = [
                    NoteModifiers.None,
                    NoteModifiers.Accented,
                    NoteModifiers.Ghost,
                    NoteModifiers.Left,
                    NoteModifiers.Right
                ],
                Label = "Modifiers",
                DefaultValue = NoteModifiers.None,
                OnCommit = v =>
                {
                    preset.Modifiers = v;
                    Editor.PushChange(new NoteBeatmapChange(() =>
                    {
                        for (var i = 0; i < Beatmap.HitObjects.Count; i++)
                        {
                            if (Beatmap.HitObjects[i].Preset == preset)
                                Beatmap.HitObjects[i] = Beatmap.HitObjects[i].With(v);
                        }
                    }, $"change {preset} modifiers to {v}"));
                    Table.UpdateRow(preset);
                }
            },
        });
    }
    TableView<NotePreset> Table;
    protected override void Dispose(bool isDisposing)
    {
        Beatmap.NotePresets.PresetAdded -= PresetAdded;
        Beatmap.NotePresets.PresetRemoved -= PresetRemoved;
        Beatmap.NotePresets.PresetChanged -= PresetChanged;
        base.Dispose(isDisposing);
    }
    void PresetAdded(NotePreset preset) => Table.InvalidateRows();
    void PresetRemoved(NotePreset preset) => Table.InvalidateRows();
    void PresetChanged(NotePreset preset, string _) => Table.UpdateRow(preset);
    [BackgroundDependencyLoader]
    private void load()
    {
        AddFooterButtonSpaced(new DrumButton
        {
            Text = "New Preset",
            AutoSize = true,
            Action = () =>
            {
                var next = 1;
                while (Beatmap.NotePresets.ContainsKey(next.ToString()))
                    next++;
                var newPreset = new NotePreset { Key = next.ToString() };
                Editor.PushChange(new PresetBeatmapChange(
                    e => e.Beatmap.NotePresets.Add(newPreset),
                    e => e.Beatmap.NotePresets.RemovePreset(next.ToString()),
                    $"add preset {next}"
                ));
            }
        });
        AddFooterButtonSpaced(new DrumButton
        {
            Text = "Remove 0 Usages",
            AutoSize = true,
            Action = () =>
            {
                var remove = new HashSet<NotePreset>();
                foreach (var (key, value) in Beatmap.NotePresets)
                    if (usageCount(value) == 0)
                        remove.Add(value);

                Editor.PushChange(new PresetBeatmapChange(editor =>
                {
                    foreach (var e in remove)
                        Editor.Beatmap.NotePresets.Remove(e);
                }, editor =>
                {
                    foreach (var e in remove)
                        Editor.Beatmap.NotePresets.Add(e);
                }, "remove 0 usage presets"));
            }
        });
        Beatmap.NotePresets.PresetAdded += PresetAdded;
        Beatmap.NotePresets.PresetRemoved += PresetRemoved;
        Beatmap.NotePresets.PresetChanged += PresetChanged;
    }
    public NotePresetsView(BeatmapEditor editor) : base(new RequestConfig
    {
        Title = "Note Presets",
        Width = 0
    })
    {
        Editor = editor;
        // TODO changes need to be pushed to the undo stack
        // When pushed, we have to check all notes that use the preset and reload those measures
        var presets = Beatmap.NotePresets.Values;
        Add(Table = new TableView<NotePreset>(new()
        {
            MinWidth = 700,
            BuildContextMenu = builder => builder
                .Add("Seek to Next Usage", e =>
                {
                    var currentTick = (int)Math.Ceiling((Editor.Track.CurrentBeat + Beatmap.BeatEpsilon) * Beatmap.TickRate);
                    foreach (var hit in Beatmap.HitObjects)
                    {
                        if (hit.Preset == e && hit.Time > currentTick)
                        {
                            Editor.Track.Seek(Beatmap.MillisecondsFromTick(hit.Time));
                            return;
                        }
                    }
                    // not found, so go to first instance
                    foreach (var hit in Beatmap.HitObjects)
                    {
                        if (hit.Preset == e)
                        {
                            Editor.Track.Seek(Beatmap.MillisecondsFromTick(hit.Time));
                            return;
                        }
                    }
                })
                .Add("Clone", e =>
                {
                    var newPreset = e.Clone();
                    newPreset.Key = Util.CloneName(e.Key, e => !Beatmap.NotePresets.ContainsKey(e));
                    newPreset.Name = Util.CloneName(e.Name, e => !Beatmap.NotePresets.Values.Any(p => p.Name == e));
                    Editor.PushChange(new PresetBeatmapChange(
                        e => e.Beatmap.NotePresets.Add(newPreset),
                        e => e.Beatmap.NotePresets.Remove(newPreset),
                        $"clone preset {e.Key} => {newPreset.Key}"
                    ));
                }).Color(DrumColors.BrightGreen),
            OnCellChange = (preset, column, _) =>
            {
                if (Editor != null)
                {
                    var filtered = Beatmap.HitObjects.Where(e => e.Preset == preset);
                    var first = filtered.FirstOrDefault();
                    if (first != null)
                    {
                        var last = filtered.LastOrDefault();
                        Editor.Display.ReloadNoteRange(new AffectedRange(first.Time, last.Time + 1));
                    }
                }
            },
            Columns = ColumnBuilder.New<NotePreset>()
                .Add(nameof(NotePreset.Key))
                .Editable((e, column, _) =>
                {
                    Util.Palette.RequestString($"Changing Key For {e.Key}", "Key", e.Key, key =>
                    {
                        if (Beatmap.NotePresets.ContainsKey(key))
                        {
                            Util.Palette.UserError($"A preset with key {key} already exists");
                        }
                        else
                        {
                            var oldKey = e.Key;
                            Editor.PushChange(new PresetBeatmapChange(
                                editor => editor.Beatmap.NotePresets.ChangeKey(e, key),
                                editor => editor.Beatmap.NotePresets.ChangeKey(e, oldKey),
                                $"change preset key to {key}"
                            ));
                        }
                    });
                })
                .Add(nameof(NotePreset.Sample)).Hide(presets.All(e => string.IsNullOrWhiteSpace(e.Sample))).BasicEdit()
                .Add(nameof(NotePreset.Name)).Hide(presets.All(e => string.IsNullOrWhiteSpace(e.Name))).BasicEdit()
                .Add(nameof(NotePreset.Description)).Hide().BasicEdit()
                .Add(new TableViewColumn<NotePreset>()
                {
                    Header = "Usages",
                    GetValue = e => usageCount(e).ToString()
                })
                .Add(new TableViewColumn<NotePreset>()
                {
                    Header = "Channel",
                    GetValue = e =>
                    {
                        if (Usages.TryGetValue(e, out var o))
                        {
                            if (o.DefaultChannel is DrumChannel dc)
                                if (dc == DrumChannel.None)
                                    return "Multiple";
                                else
                                    return (e.Channel == DrumChannel.None || e.Channel == dc) ? dc.ToString() : "Multiple";
                            return null;
                        }
                        return e.Channel == DrumChannel.None ? null : e.Channel.ToString();
                    }
                }).Editable(RequestChangeChannel)
                .Add(nameof(NotePreset.Modifiers)).Editable(RequestChangeModifiers)
                .Add(nameof(NotePreset.Pan)).Hide()
                .Add(nameof(NotePreset.Volume)).BasicEdit()
                .Add(nameof(NotePreset.Size)).BasicEdit()
                .Add("Hotkey", e => e.Keybind.ToString()).Hide().Editable((e, column, table) =>
                {
                    RequestModal req = null;
                    req = Util.Palette.Request(new RequestConfig
                    {
                        Title = $"Setting {column.Header} for {e}",
                        Field = new KeyComboFieldConfig(column.Header, e.Keybind)
                        {
                            OnCommit = key =>
                            {
                                e.Keybind = key;
                                e.Register();
                            }
                        },
                        Footer = new DrumButton
                        {
                            Text = "Remove Hotkey",
                            AutoSize = true,
                            Action = () =>
                            {
                                e.Keybind = default;
                                e.Register();
                                req.Close();
                            }
                        }
                    });
                })
                .Build()
        }, () => Beatmap.NotePresets.Values.OrderBy(e => e.Key).ToList()));
    }
}