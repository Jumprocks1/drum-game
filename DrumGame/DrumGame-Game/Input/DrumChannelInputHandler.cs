using System;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using DrumGame.Game.Commands;
using DrumGame.Game.Midi;
using DrumGame.Game.Stores;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Input;

public class DrumChannelInputHandler : IDisposable
{
    public delegate void DrumTriggerHandler(DrumChannelEvent ev);
    public readonly DrumTriggerHandler OnTrigger; // this gets called on the update thread
    public readonly TrackClock Track;
    public readonly DrumGameConfigManager ConfigManager;
    public double MidiInputOffset => ConfigManager.MidiInputOffset.Value;
    public bool ConsumeInputs = true;
    public DrumChannelInputHandler(DrumTriggerHandler trigger, TrackClock track)
    {
        OnTrigger = trigger;
        Track = track;
        ConfigManager = Util.ConfigManager;
        Util.CommandController.RegisterHandler(Command.ToggleDrumChannel, ToggleDrumChannel);
    }

    public KeyboardMapping Mapping = Util.ConfigManager.KeyboardMapping.Value;
    public bool Handle(KeyDownEvent ev)
    {
        var channel = Mapping.GetChannel(ev);
        if (channel != DrumChannel.None)
        {
            var offset = ConfigManager.KeyboardInputOffset.Value;
            OnTrigger(new DrumChannelEvent(Track.AbsoluteTime - offset * Track.EffectiveRate, channel));
            return ConsumeInputs;
        }
        return false;
    }

    public bool ToggleDrumChannel(CommandContext context)
    {
        if (context.TryGetParameter<DrumChannel>(out var channel) && channel != DrumChannel.None)
        {
            var offset = context.Midi ? MidiInputOffset : ConfigManager.KeyboardInputOffset.Value;
            OnTrigger(new DrumChannelEvent(Track.AbsoluteTime - offset * Track.EffectiveRate, channel) { MIDI = context.Midi });
            return ConsumeInputs;
        }
        return false;
    }
    public bool OnMidiNote(MidiNoteOnEvent e)
    {
        // need to pull version before trackTime
        // ideally we could lock and pull both at the same time
        // if we pull version after, track could seek after we pull time, but our version would be the latest still
        // if a seek happens between the version pull, that's okay but it does mean we drop an event when we technically don't need to
        var version = Track.TimeVersion;
        var trackTime = Track.AbsoluteTime - MidiInputOffset * Track.EffectiveRate;
        var channel = e.DrumChannel;
        if (channel != DrumChannel.None)
        {
            var ev = e.ToDrumChannelEvent(trackTime);
            ev.TimeVersion = version;
            Util.Host.UpdateThread.Scheduler.Add(() => OnTrigger(ev), false);
            return ConsumeInputs;
        }
        return false;
    }

    public void Dispose()
    {
        Util.CommandController.RemoveHandler(Command.ToggleDrumChannel, ToggleDrumChannel);
    }
}
