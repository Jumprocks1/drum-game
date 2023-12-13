using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DrumGame.Game.IO.Midi;

public class MidiHeader : MidiFile.Chunk
{
    public enum Format
    {
        Unknown,
        MultiChannel,
        Tracks,
        TrackPatterns
    }
    public Format format;
    public int quarterNote;

    public new static MidiHeader Read(MidiReader reader)
    {
        var format = reader.ReadInt16();
        var numberTracks = reader.ReadInt16();
        var divisions = reader.ReadUInt16();
        if ((divisions & 0x8000) != 0) { throw new NotImplementedException(); }
        return new MidiHeader
        {
            format = format switch
            {
                0 => Format.MultiChannel,
                1 => Format.Tracks,
                2 => Format.TrackPatterns,
                _ => Format.Unknown
            },
            quarterNote = divisions
        };
    }
}

