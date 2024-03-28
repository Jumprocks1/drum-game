using System;
using DrumGame.Game.Stores;
using DrumGame.Game.Views.Settings.SettingInfos;
using osu.Framework.Configuration;

namespace DrumGame.Game.Views.Settings;

public abstract class SettingInfo : IDisposable
{
    public string Label;
    public virtual bool Open => false;
    public virtual float Height => 30;
    public string Tooltip;
    public SettingInfo(string label)
    {
        Label = label;
    }
    public virtual void Render(SettingControl control) { } // could be abstract
    public virtual void OnClick(SettingControl control) { }
    public virtual void Close(SettingControl control) { }

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
            Tooltip = "Use this if any of your hits don't register as the correct drum"
        },
        new DoubleSettingInfo("MIDI Input Offset", config.MidiInputOffset) {
            Tooltip = "This value (ms) is subtracted from the time on every MIDI input event"
        },
        new DoubleSettingInfo("MIDI Output Offset", config.MidiOutputOffset) {
            Tooltip = "MIDI output events are queued earlier (or later) based on this value. Negative values cause output events to be delayed."
        },
        new SkinSetting("Skin"),
        new BooleanSettingInfo("Smooth Scroll", config.SmoothScroll) {
            Tooltip = "Smooth scroll makes the notes move towards a static cursor/judgement line."
        },
        // TODO we should try making these nice sliders with manual override if you click the number value
        // default range can be like 50%-200% - could even make it logarithmic
        new DoubleSettingInfo("Note Spacing Multiplier", config.GetBindable<double>(DrumGameSetting.NoteSpacingMultiplier)) {
            Tooltip = "Increases or decreases spacing between notes. Higher values will make denser maps easier to read. Recommended between 1 and 2. Defaults to 1."
        },
        new DoubleSettingInfo("Zoom Multiplier", config.GetBindable<double>(DrumGameSetting.ZoomMultiplier)) {
            Tooltip = "Increases or decreases default zoom of the in-game staff. Defaults to 1. Recommended between 0.5 and 2."
        },
        new DoubleSettingInfo("Cursor Inset", config.CursorInset) {
            Tooltip = "Distance (in beats) between the cursor and the left edge of the screen. Defaults to 4. Smaller values are recommended when at higher zoom or note spacing."
        },
        new DoubleSettingInfo("Mania Scroll Multiplier", config.GetBindable<double>(DrumGameSetting.ManiaScrollMultiplier)) {
            Tooltip = "Increases or decreases spacing between mania chips. Only applies when display mode set to Mania.\nHigher values will make faster maps easier to read.\nThe numbers here should match scroll rates in DTXmania."
        },
        new ModalSettingInfo<ChannelEquivalentsView>("Channel Equivalents") {
            Tooltip = "This can be helpful if you do not have a trigger for every channel in Drum Game. Especially useful for cymbals."
        },
        new DoubleSettingInfo("Minimum Lead-In", config.GetBindable<double>(DrumGameSetting.MinimumLeadIn)) {
            Tooltip = "Minimum time in seconds before the first note of a song. Default to 1 second."
        },
        new IntSettingInfo("MIDI Threshold", config.MidiThreshold) {
            Tooltip = "MIDI events with a velocity less than or equal to this value will be completely ignored by the game.\nMIDI events typically have velocities between 0 and 127. Recommended to set this to 0 and configure your module for adjusting specific pads.\nPrimarily useful for excluding velocity 0 events since some modules always output these events."
        },
        new EnumSettingInfo<LayoutPreference>("Layout Preference", config.LayoutPreference) {
            Tooltip = "This moves the input display off to the side so you can overlay a camera."
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
