using System;
using System.ComponentModel;
using DrumGame.Game.Channels;
using DrumGame.Game.Commands;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using osu.Framework.Graphics;

namespace DrumGame.Game.Beatmaps.Data;

// these are similar to chip configurations in DTX
// we could have some special preset redirects based on channel
// ie if you assign preset 1 to a snare, it gets redirected to 2, but if it's on a bass hit it goes to preset 3
public class NotePreset
{
    // this is used as the dictionary key and is also stored in the individual notes with the preset
    // in theory long names take up more storage, but in reality I don't think it matters
    [JsonIgnore] public string Key; // ignored because it is used as dictionary key
    string _name;
    public string Name
    {
        get => _name; set
        {
            _name = value;
            if (Registered) Register(); // reregister with new name
        }
    } // typically we can just use the key
    [JsonIgnore] public string NameOrKey => Name ?? Key;
    public string Description;
    public string Sample; // sample path

    [Description("Sends a MIDI note off event after this time (in seconds).\nSoundfont must be configured to have decay for this to work.")]
    public double? ChokeDelay;

    [DefaultValue(1f)]
    public float Volume = 1; // 1 is default, can go above 1 in most cases

    // Not required. Only used as a default/suggestions
    // If this is set, we should run some checks on all notes using it
    public DrumChannel Channel;

    // Not required. Only used as a default/suggestions
    public NoteModifiers Modifiers;

    // L/R, -1 to 1, 0 is center, null is unset which usually defaults back to 0
    public float? Pan;

    [DefaultValue(1f)]
    public float Size = 1; // defaults to 1. Size in mania, may also be used as alpha instead of size

    public Colour4 Color;

    // make sure to update this whenever the Name changes
    [JsonIgnore] public CommandInfo CommandInfo { get; private set; }

    // This probably has an issue where if they try to change it through the keybind interface it will create a weird override in the ini file that doesn't do anything
    public KeyCombo Keybind;

    [JsonIgnore] public bool Registered => CommandInfo != null;
    public void Register() // safe to call multiple times
    {
        if (Registered) Unregister();
        CommandInfo = new CommandInfo(Command.InsertPresetNote, CommandInfoName) { Parameter = this };
        if (Keybind != default) CommandInfo.Bindings.Add(Keybind);
        Util.CommandController.RegisterCommandInfo(CommandInfo);
    }
    string CommandInfoName => $"Insert Preset {NameOrKey}";
    public void Unregister()
    {
        if (!Registered) throw new Exception($"Note preset not registered");
        Util.CommandController.RemoveCommandInfo(CommandInfo);
        CommandInfo = null;
    }

    public override string ToString() => NameOrKey;

    public HitObjectData GetData() => new(Channel, Modifiers, this);

    // make sure to replace Key/Name
    public NotePreset Clone()
    {
        var clone = (NotePreset)MemberwiseClone();
        clone.CommandInfo = null;
        return clone;
    }
}