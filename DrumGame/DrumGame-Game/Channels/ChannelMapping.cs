using DrumGame.Game.Commands;
using DrumGame.Game.Input;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Input;
using MidiInputKey = osu.Framework.Input.Bindings.InputKey;
using MK = osu.Framework.Input.MidiKey;

namespace DrumGame.Game.Channels;

public static class ChannelMapping
{
    public static bool IsMidi(this MidiInputKey key) => key >= (MidiInputKey.MidiA0 - (int)MK.A0) && key <= MidiInputKey.MidiC8;
    public static string MidiString(this MidiInputKey key)
    {
        var note = (byte)(key - MidiInputKey.MidiA0 + MK.A0);
        var dc = MidiMapping(note);
        if (dc != DrumChannel.None && dc.MidiKey() == (MK)note) return dc.ToString();
        else return ((MidiKey)note).ToString();
    }

    static MidiMapping CustomMapping => Util.ConfigManager.MidiMapping.Value;

    // this obeys the user's settings
    public static DrumChannel MidiMapping(byte note)
    {
        var dc = CustomMapping.MapNote(note);
        if (dc == DrumChannel.None) dc = StandardMidiMapping(note);
        return dc;
    }

    // TD 27 default mapping https://rolandus.zendesk.com/hc/en-us/articles/4407474950811-TD-27-Default-MIDI-Note-Map
    // this should ignore any user overrides
    public static DrumChannel StandardMidiMapping(int midiChannel) => midiChannel switch
    {
        35 => DrumChannel.BassDrum,
        36 => DrumChannel.BassDrum,
        37 => DrumChannel.SideStick,
        39 => DrumChannel.SideStick,
        38 => DrumChannel.Snare,
        40 => DrumChannel.Rim, // Snare 
        41 => DrumChannel.LargeTom,
        43 => DrumChannel.LargeTom,
        58 => DrumChannel.Rim, // Large tom rim
        45 => DrumChannel.MediumTom,
        47 => DrumChannel.Rim, // Medium tom rim
        48 => DrumChannel.SmallTom,
        50 => DrumChannel.Rim, // Small tom rim
        42 => DrumChannel.ClosedHiHat,
        22 => DrumChannel.ClosedHiHat, // hi-hat rim, taken from  TD-27
        46 => DrumChannel.OpenHiHat,
        26 => DrumChannel.OpenHiHat, // hi-hat rim, taken from  TD-27
        51 => DrumChannel.Ride, // bow
        53 => DrumChannel.RideBell, // bell
        59 => DrumChannel.RideCrash, // ride edge
        54 => DrumChannel.OpenHiHat, // Tambourine
        49 => DrumChannel.Crash, // crash 1
        55 => DrumChannel.Splash, // crash 1 edge
        57 => DrumChannel.Crash, // crash 2
        52 => DrumChannel.China, // crash 2 edge
        44 => DrumChannel.HiHatPedal,
        _ => DrumChannel.None
    };

    // meant to match https://musescore.org/sites/musescore.org/files/General%20MIDI%20Standard%20Percussion%20Set%20Key%20Map.pdf
    public static DrumChannel ImportMidiMapping(int midiChannel) => midiChannel switch
    {
        40 => DrumChannel.Snare,
        47 => DrumChannel.MediumTom,
        50 => DrumChannel.SmallTom,
        56 => DrumChannel.Splash, // fat stack?
        64 => DrumChannel.Crash, // left crash 2
        63 => DrumChannel.SmallTom, // some sort of conga?
        _ => StandardMidiMapping(midiChannel)
    };
    // mapped from https://musescore.org/sites/musescore.org/files/General%20MIDI%20Standard%20Percussion%20Set%20Key%20Map.pdf
    // note the notes in that list are 1 octave too low
    //   ex: bass drum (35) should be B1
    public static MidiInputKey InputKey(this DrumChannel drumChannel) => drumChannel switch
    { // subtract 8172 for midi number
        DrumChannel.Crash => MidiInputKey.MidiCSharp3,
        DrumChannel.OpenHiHat => MidiInputKey.MidiASharp2,
        DrumChannel.HalfOpenHiHat => MidiInputKey.MidiFSharp3,
        DrumChannel.ClosedHiHat => MidiInputKey.MidiFSharp2,
        DrumChannel.Ride => MidiInputKey.MidiDSharp3,
        DrumChannel.RideBell => MidiInputKey.MidiF3,
        DrumChannel.RideCrash => MidiInputKey.MidiB3,
        DrumChannel.Snare => MidiInputKey.MidiD2,
        DrumChannel.SideStick => MidiInputKey.MidiCSharp2,
        DrumChannel.Rim => MidiInputKey.MidiCSharp2,
        DrumChannel.SmallTom => MidiInputKey.MidiC3,
        DrumChannel.MediumTom => MidiInputKey.MidiA2,
        DrumChannel.LargeTom => MidiInputKey.MidiF2,
        DrumChannel.BassDrum => MidiInputKey.MidiC2,
        DrumChannel.HiHatPedal => MidiInputKey.MidiGSharp2,
        DrumChannel.Splash => MidiInputKey.MidiG3,
        DrumChannel.China => MidiInputKey.MidiE3,
        DrumChannel.Metronome => MidiInputKey.None,
        _ => MidiInputKey.None
    };
    public static MK MidiKey(this DrumChannel drumChannel) => (MK)((int)drumChannel.InputKey() - 8172);
    public static byte MidiNote(this DrumChannel drumChannel) => (byte)((int)drumChannel.InputKey() - 8172);
    // includes pedal
    public static bool IsHiHat(this DrumChannel channel) => channel switch
    {
        DrumChannel.OpenHiHat or DrumChannel.HalfOpenHiHat or DrumChannel.ClosedHiHat or DrumChannel.HiHatPedal => true,
        _ => false
    };
    public static bool IsFoot(this DrumChannel channel) => channel == DrumChannel.BassDrum || channel == DrumChannel.HiHatPedal; // used to separated voices
    public static bool IsCymbal(this DrumChannel channel) => channel switch
    {
        DrumChannel.China or DrumChannel.Crash or DrumChannel.Ride or DrumChannel.RideBell or DrumChannel.RideCrash
            or DrumChannel.OpenHiHat or DrumChannel.ClosedHiHat => true,
        _ => false
    };

    // this could be done with attributes, but then it wouldn't be able to change dynamically
    public static string MarkupDescription(this DrumChannel channel) => channel switch
    {
        DrumChannel.SideStick => $"Represents hitting the snare rim while the butt of the stick is pressed into the head.\nOnly needs to be bound if your module has separate MIDI values for snare head/rim/sidestick.",
        DrumChannel.Metronome => $"Used for metronome events, should not be bound.",
        DrumChannel.PracticeMetronome => $"Used for the practice metronome, should not be bound.",
        DrumChannel.Rim => $"Rim of any drum. Typically bound to snare rim if your module supports it.",
        DrumChannel.HalfOpenHiHat => $"A half open hi-hat hit is triggered when hitting the hi-hat while the MIDI hi-hat control value is between {Util.ConfigManager.HiHatRange.Value.Item1} and {Util.ConfigManager.HiHatRange.Value.Item2}."
            + "\nYou can configure the control range in the game settings."
            + $"\nTo see the range of control values your drum module outputs, use {IHasCommand.GetMarkupTooltipIgnoreUnbound(Command.MidiMonitor)}."
            + "\n\nTo group all hi-hat inputs, bind MIDI inputs to the half open channel and set closed and open as equivalents for half open.",
        _ => null
    };
}

