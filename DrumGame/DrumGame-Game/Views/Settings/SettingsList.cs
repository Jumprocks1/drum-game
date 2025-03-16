using System;
using System.Collections.Generic;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Commands;
using DrumGame.Game.Components.Basic.Autocomplete;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using DrumGame.Game.Views.Settings.SettingInfos;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Views.Settings;

public abstract class SettingInfo : IDisposable
{
    // Basically just deferred tooltip
    public virtual string Description => null; // could also make a virtual IBindable instead and map this to IBindable.Description
    public string Label;
    public virtual bool Open => false;
    public virtual float Height => 30;
    public string Tooltip;
    public SettingInfo(string label)
    {
        Label = label;
    }
    public Action<SettingControl> AfterRender;
    public virtual void Render(SettingControl control) { } // could be abstract
    public virtual void OnClick(SettingControl control) { }

    public virtual void Dispose() { }
}
public static class SettingsList
{
    public static void RenderSettings(SettingsView view, DrumGameConfigManager config, FrameworkConfigManager fConfig)
    {
        view.AddSetting(new EnumSettingInfo<WindowMode>("Window Mode", fConfig.GetBindable<WindowMode>(FrameworkSetting.WindowMode))
        {
            AfterRender = e => e.Command = Command.ToggleFullscreen,
            Tooltip = "Highly recommend Fullscreen (not Borderless)"
        });
        view.AddSetting(new EnumSettingInfo<FrameSync>("Frame Sync", fConfig.GetBindable<FrameSync>(FrameworkSetting.FrameSync))
        {
            AfterRender = e => e.Command = Command.CycleFrameSync,
            GetLabel = e => e == FrameSync.VSync ? "VSync" : null,
            Tooltip = "Recommend using VSync.\nIf using in-game audio samples, higher limits can slightly reduce latency."
        });
        new SettingsListBuilder()
            .Add(new ButtonSettingInfo("MIDI Mapping", "Use this if any of your hits don't register as the correct drum", "Edit")
            {
                Action = control => Util.Palette.Push<MidiMappingView>()
            })
            .AddCommandIconButton(Command.MidiMonitor, FontAwesome.Solid.Music)
            .Add(new DoubleSettingInfo("MIDI Input Offset", config.MidiInputOffset)
            {
                Tooltip = "This value (ms) is subtracted from the time on every MIDI input event.\nIf you are getting too many late judgements, increase this value.\nIf you're using your drum module's hit sounds, this should most likely be a small positive number."
            })
            .Add(new DoubleSettingInfo("MIDI Output Offset", config.MidiOutputOffset)
            {
                Tooltip = $"MIDI output events are queued earlier (or later) based on this value.\nNegative values cause output events to be delayed.\n\nOutput events occur when using some form of autoplay (replays, autoplay mod, or editor autoplay).\nYou can view connected MIDI output devices with the {IHasCommand.GetMarkupTooltipIgnoreUnbound(Command.MidiMonitor)} command."
            })
            .Add(HitWindowSettingsInfo.Create())
            .Add(new SkinSetting("Skin"))
            .Add(new ButtonSettingInfo("Channel Equivalents", "This can be helpful if you do not have a trigger for every channel in Drum Game. Especially useful for cymbals.", "Edit")
            {
                Action = control => Util.Palette.Push<ChannelEquivalentsView>()
            })
            .BuildTo(view);
        view.AddSetting(new BooleanSettingInfo("Automatically Load BG Video", config.GetBindable<bool>(DrumGameSetting.AutoLoadVideo))
        {
            Tooltip = $"Some maps include background videos. If this setting is enabled, videos will be shown if they exists.\nYou can manually toggle the video with {IHasCommand.GetMarkupTooltipNoModify(Command.ToggleVideo)}"
        });
        view.AddSetting(new DoubleSettingInfo("Minimum Lead-In", config.GetBindable<double>(DrumGameSetting.MinimumLeadIn))
        {
            Tooltip = "Minimum time in seconds before the first note of a song. Defaults to 1 second."
        });
        view.AddSetting(new IntSettingInfo("MIDI Threshold", config.MidiThreshold)
        {
            Tooltip = "MIDI events with a velocity less than or equal to this value will be completely ignored by the game.\nMIDI events typically have velocities between 0 and 127. Recommended to set this to 0 and configure your module for adjusting specific pads.\nPrimarily useful for excluding velocity 0 events since some modules always output these events."
        });
        view.AddSetting(new EnumSettingInfo<RendererType>("Preferred Renderer", fConfig.GetBindable<RendererType>(FrameworkSetting.Renderer))
        {
            Tooltip = $"Requires rebooting the game to take effect.\nTo see the current renderer, activate {IHasCommand.GetMarkupTooltipIgnoreUnbound(Command.CycleFrameStatistics)}",
            GetLabel = e => Util.Description(e)
        });
        view.AddSetting(new VolumeSliderSettingInfo("Sample Volume", config.SampleVolume)
        {
            Tooltip = "Volume of samples/sound effects. Currently only applies to metronome and autoplay notes."
        });
        view.AddSetting(new BooleanSettingInfo("Save Full Replay Data", config.GetBindable<bool>(DrumGameSetting.SaveFullReplayData))
        {
            Tooltip = "Automatically save all the note events/hits after each play. Equivalent to pressing 'Save Replay' after each play."
        });
        new SettingsListBuilder()
            .Add(new BooleanSettingInfo("Play Samples From MIDI", config.GetBindable<bool>(DrumGameSetting.PlaySamplesFromMidi))
            {
                Tooltip = "With this enabled, the current soundfont file (located at resources/soundfonts/main.sf2) will play a sample whenever a MIDI event is recieved.\nIf the current map supports custom hit sounds, those will be activated when possible.\nYou should expect additional latency when using this setting (compared to using sounds coming directly from a drum module)."
            })
            .AdvancedMenu("Sample Playback Settings", e => e
                .Add(new VolumeSliderSettingInfo("Soundfont Volume", config.SoundfontVolume)
                {
                    Tooltip = "The default soundfont is a bit quiet, so this should generally be a positive dB value."
                })
                .Add(new BooleanSettingInfo("Play Soundfount Outside of Maps", config.GetBindable<bool>(DrumGameSetting.PlaySoundfontOutsideMaps))
                {
                    Tooltip = "If no map is open, soundfont audio is played for MIDI events."
                        + "\nPrimarily meant for song select."
                        + "\nRequires `Play Samples From MIDI` to be enabled."
                })
                .Add(new BooleanSettingInfo("Use MIDI Velocity", config.GetBindable<bool>(DrumGameSetting.SoundfontUseMidiVelocity))
                {
                    Tooltip = "Adjusts soundfont playback volume based on MIDI velocity.\nMay require fine tuning of the soundfont file."
                        + "\nRequires `Play Samples From MIDI` to be enabled."
                })
            )
            .BuildTo(view);
        view.AddSetting(new BooleanSettingInfo("Discord Rich Presence", config.GetBindable<bool>(DrumGameSetting.DiscordRichPresence))
        {
            Tooltip = "When enabled, the game will attempt to connect to the DiscordRPC API."
        });
        view.AddSetting(new BooleanSettingInfo("Preserve Pitch", config.GetBindable<bool>(DrumGameSetting.PreservePitch))
        {
            Tooltip = "When enabled and using playback speeds other than 1, the game will use pitch correction to keep the pitch the same when changing speeds.\nCan be useful when editing at lower speeds."
        });
        new SettingsListBuilder()
            .Add(new DoubleSettingInfo("Keyboard Input Offset", config.KeyboardInputOffset)
            {
                Tooltip = "This value (ms) is subtracted from the time on every keyboard input event.\nIf you are getting too many late judgements, increase this value.\nSince audio events for key presses can only be queued after the game receives the input, this value should typically be a negative value roughly equal to your system's audio latency."
            })
            .BuildTo(view);
    }
}
