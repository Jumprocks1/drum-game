using System;
using DrumGame.Game.Channels;

namespace DrumGame.Game.Beatmaps.Loaders;

// https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.mid/Standard/Drums.md
public static class SongIniMapping
{
    // For now we will only support pro_drums tracks

    // https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.mid/Standard/Drums.md#track-notes
    public static int Difficulty(byte midiNote) => midiNote >= 59 && midiNote <= 101 ? (midiNote - 59) / 12 : midiNote switch
    {
        116 => -2, // star power, ignore
        >= 120 and <= 124 => -2, // fill sections, ignore
        _ => -1 // unrecognized, include for all
    };

    public static HitObjectData ToHitObjectData(byte midiNote, byte velocity, bool[,] phrases, bool[] markers, SongIniLoader.TrackType type)
    {
        // markers: https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.mid/Standard/Drums.md#track-notes
        var d = Difficulty(midiNote);
        if (d >= 0)
        {
            var relativeNote = midiNote - 59 - d * 12;
            if (type == SongIniLoader.TrackType.Drums)
            {
                var crash1 = markers[34] || markers[35] || markers[36] || markers[37];
                var crash2 = markers[38] || markers[39] || markers[44] || markers[45];
                if (relativeNote == 3)
                    return markers[110] ? new HitObjectData(DrumChannel.SmallTom) : new HitObjectData(DrumChannel.OpenHiHat);
                else if (relativeNote == 4)
                {
                    if (!markers[111])
                    {
                        if (crash1 && crash2)
                            return new HitObjectData(DrumChannel.Crash, NoteModifiers.Accented);
                        else if (crash1)
                            return new HitObjectData(DrumChannel.Crash, NoteModifiers.Left);
                        else if (crash2)
                            return new HitObjectData(DrumChannel.Crash, NoteModifiers.Right);
                    }
                }
                else if (relativeNote == 5)
                {
                    if (!markers[112])
                    {
                        if (crash1 && crash2)
                            return new HitObjectData(DrumChannel.Crash, NoteModifiers.Accented);
                    }
                }
            }
            var o = relativeNote switch
            {
                0 => new HitObjectData(DrumChannel.BassDrum, NoteModifiers.Left),
                1 => new HitObjectData(DrumChannel.BassDrum),
                2 => new HitObjectData(DrumChannel.Snare, phrases[d, 0x07] ? NoteModifiers.Accented : default), // red
                3 => markers[110] ? new HitObjectData(DrumChannel.SmallTom) :
                    phrases[d, 0x05] ? new HitObjectData(DrumChannel.OpenHiHat) :
                    phrases[d, 0x06] ? new HitObjectData(DrumChannel.HiHatPedal) :
                    phrases[d, 0x08] ? new HitObjectData(DrumChannel.ClosedHiHat) : // technically should be half open hi-hat
                    new HitObjectData(DrumChannel.Crash, NoteModifiers.Left), // yellow
                4 => markers[111] ? new HitObjectData(DrumChannel.MediumTom) : new HitObjectData(DrumChannel.Ride), // blue
                5 => markers[112] ? new HitObjectData(DrumChannel.LargeTom) : new HitObjectData(DrumChannel.Crash, NoteModifiers.Right), // green/orange?
                6 => markers[112] ? new HitObjectData(DrumChannel.LargeTom) : new HitObjectData(DrumChannel.None), // green
                _ => default
            };
            return o;
        }
        return default;
    }


    public static HitObjectData DotChartMapping(int note, bool[] modifierFlags)
    {
        var modifiers = NoteModifiers.None;
        HitObjectData o = note switch
        {
            0 => new(DrumChannel.BassDrum),
            32 => new(DrumChannel.BassDrum, NoteModifiers.Left),
            1 => new(DrumChannel.Snare), // red
            2 => modifierFlags[32] ? new(DrumChannel.OpenHiHat) : new(DrumChannel.SmallTom), // yellow
            3 => new(modifierFlags[33] ? DrumChannel.Ride : DrumChannel.MediumTom), // blue
            4 => modifierFlags[34] ? new(DrumChannel.Crash, NoteModifiers.Right) : new(DrumChannel.LargeTom), // orange
            5 => new(DrumChannel.LargeTom), // green
            _ => default
        };
        if (note <= 5 && modifierFlags[note]) modifiers |= NoteModifiers.Accented;
        if (note <= 5 && modifierFlags[note + 6]) modifiers |= NoteModifiers.Ghost;
        if (modifiers == NoteModifiers.None) return o;
        return new HitObjectData(o.Channel, o.Modifiers | modifiers);
    }
}