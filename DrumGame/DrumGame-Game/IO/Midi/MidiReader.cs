using System;
using System.IO;
using System.IO.Compression;

namespace DrumGame.Game.IO.Midi;

public class MidiReader : BigEndianReader
{
    static Stream FixStream(Stream stream)
    {
        if (stream is DeflateStream) // deflate stream doesn't support position or length
        {
            var o = new MemoryStream();
            stream.CopyTo(o);
            o.Seek(0, SeekOrigin.Begin);
            return o;
        }
        return stream;
    }
    public MidiReader(Stream stream) : base(FixStream(stream)) { }
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

