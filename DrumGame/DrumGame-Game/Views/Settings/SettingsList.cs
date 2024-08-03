using System;
using DrumGame.Game.Commands;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using DrumGame.Game.Views.Settings.SettingInfos;
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
    public static SettingInfo[] GetSettings(DrumGameConfigManager config, FrameworkConfigManager fConfig) => new SettingInfo[] {
        new WindowModeSetting(fConfig.GetBindable<WindowMode>(FrameworkSetting.WindowMode)) {
            Tooltip = "Highly recommend Fullscreen (not Borderless)"
        },
        new FrameSyncSetting(fConfig.GetBindable<FrameSync>(FrameworkSetting.FrameSync)) {
            Tooltip = "Highly recommend using VSync"
        },
        new ModalSettingInfo<MidiMappingView>("MIDI Mapping") {
            Tooltip = "Use this if any of your hits don't register as the correct drum",
            AfterRender = control => control.AddCommandIconButton(Command.ViewMidi, FontAwesome.Solid.Music)
        },
        new DoubleSettingInfo("MIDI Input Offset", config.MidiInputOffset) {
            Tooltip = "This value (ms) is subtracted from the time on every MIDI input event"
        },
        new DoubleSettingInfo("MIDI Output Offset", config.MidiOutputOffset) {
            Tooltip = $"MIDI output events are queued earlier (or later) based on this value.\nNegative values cause output events to be delayed.\n\nOutput events occur when using some form of autoplay (replays, autoplay mod, or editor autoplay).\nYou can view connected MIDI output devices with the {IHasCommand.GetMarkupTooltipIgnoreUnbound(Command.ViewMidi)} command."
        },
        new SkinSetting("Skin"),
        new ModalSettingInfo<ChannelEquivalentsView>("Channel Equivalents") {
            Tooltip = "This can be helpful if you do not have a trigger for every channel in Drum Game. Especially useful for cymbals."
        },
        new BooleanSettingInfo("Automatically Load BG Video", config.GetBindable<bool>(DrumGameSetting.AutoLoadVideo)) {
            Tooltip = $"Some maps include background videos. If this setting is enabled, videos will be shown if they exists.\nYou can manually toggle the video with {IHasCommand.GetMarkupTooltipNoModify(Command.ToggleVideo)}"
        },
        new DoubleSettingInfo("Minimum Lead-In", config.GetBindable<double>(DrumGameSetting.MinimumLeadIn)) {
            Tooltip = "Minimum time in seconds before the first note of a song. Defaults to 1 second."
        },
        new IntSettingInfo("MIDI Threshold", config.MidiThreshold) {
            Tooltip = "MIDI events with a velocity less than or equal to this value will be completely ignored by the game.\nMIDI events typically have velocities between 0 and 127. Recommended to set this to 0 and configure your module for adjusting specific pads.\nPrimarily useful for excluding velocity 0 events since some modules always output these events."
        },
        new EnumSettingInfo<LayoutPreference>("Layout Preference", config.LayoutPreference) {
            Tooltip = "This moves the input display off to the side so you can overlay a camera."
        },
        new EnumSettingInfo<RendererType>("Preferred Renderer", fConfig.GetBindable<RendererType>(FrameworkSetting.Renderer)) {
            Tooltip = $"Requires rebooting the game to take effect.\nTo see the current renderer, activate {IHasCommand.GetMarkupTooltipIgnoreUnbound(Command.CycleFrameStatistics)}",
            GetLabel = e => Util.Description(e)
        },
        new SliderSettingInfo("Sample Volume", config.SampleVolume) {
            Tooltip = "Volume of samples/sound effects. Currently only applies to metronome and autoplay notes."
        },
        new BooleanSettingInfo("Save Full Replay Data", config.GetBindable<bool>(DrumGameSetting.SaveFullReplayData)) {
            Tooltip = "Automatically save all the note events/hits after each play. Equivalent to pressing 'Save Replay' after each play."
        },
        new BooleanSettingInfo("Play Samples From MIDI", config.GetBindable<bool>(DrumGameSetting.PlaySamplesFromMidi)) {
            Tooltip = "With this enabled, the current soundfont file (located at resources/soundfonts/main.sf2) will play a sample whenever a MIDI event is recieved."
        },
        new BooleanSettingInfo("Discord Rich Presence", config.GetBindable<bool>(DrumGameSetting.DiscordRichPresence)) {
            Tooltip = "When enabled, the game will attempt to connect to the DiscordRPC API."
        },
        new BooleanSettingInfo("Preserve Pitch", config.GetBindable<bool>(DrumGameSetting.PreservePitch)) {
            Tooltip = "When enabled and using playback speeds other than 1, the game will use pitch correction to keep the pitch the same when changing speeds.\nCan be useful when editing at lower speeds."
        },
    };
}
