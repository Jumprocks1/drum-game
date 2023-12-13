using System;
using System.IO;

namespace DrumGame.Game.IO.Midi;

public class MidiReader : BigEndianReader
{
    public MidiReader(Stream stream) : base(stream) { }
    public MidiFile ReadFile() => new MidiFile(this);
    public int ReadVInt32()
    {
        bool more = true;
        int value = 0;
        while (more)
        {
            byte lower7bits = ReadByte();
            more = (lower7bits & 128) != 0;
            value = value << 7 | (lower7bits & 0x7f);
        }
        return value;
    }
}

