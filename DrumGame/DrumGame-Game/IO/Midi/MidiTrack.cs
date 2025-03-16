using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DrumGame.Game.IO.Midi;

// https://github.com/colxi/midi-parser-js/wiki/MIDI-File-Format-Specifications

public class MidiTrack : MidiFile.Chunk
{
    public string Name;
    public List<Event> events;
    static HashSet<string> IgnoreTracks = ["PART VOCALS", "PART BASS", "PART GUITAR", "VENUE"];
    public static MidiTrack Read(MidiReader reader, int length)
    {
        var events = new List<Event>();
        var end = reader.BaseStream.Position + length;
        byte? previousEventType = null;
        var track = new MidiTrack { events = events };
        var t = 0;
        while (reader.BaseStream.Position < end)
        {
            var e = Event.Read(reader, track, ref previousEventType, out var delta);
            t += delta;
            if (e != null)
            {
                e.time = t;
                events.Add(e);
            }
            if (IgnoreTracks.Contains(track.Name))
            {
                reader.BaseStream.Seek(end, SeekOrigin.Begin);
                return null;
            }
        }
        if (reader.BaseStream.Position != end)
            Console.WriteLine($"Unexpected end position for track: {track.Name}. This likely indicates a problem with the MIDI parser.");
        return track;
    }

    public class TempoEvent : Event
    {
        public int MicrosecondsPerQuarterNote;
        public double BPM()
        {
            var bpm = 6e7 / MicrosecondsPerQuarterNote;
            var round = Math.Round(bpm, 3);
            if (round == Math.Round(bpm, 0))
            {
                return round;
            }
            return bpm;
        }
    }
    public class TimingEvent : Event
    {
        public int Numerator;
        public int Denominator;
    }
    public class MidiEvent : Event
    {
        public byte type;
        public byte channel;
        public byte parameter1;
        public byte parameter2;
    }
    public class SysExEvent : Event
    {
        public byte[] bytes;
    }
    public class TextEvent : Event
    {
        public byte Type;
        public string Text;
    }

    public abstract class Event
    {
        public static int outputCount = 0;
        public static HashSet<int> midiEvents = new HashSet<int>();
        public int time;
        public static Event Read(MidiReader reader, MidiTrack track, ref byte? previousEventType, out int delta)
        {
            delta = reader.ReadVInt32();
            var eventType = reader.ReadByte();
            if (eventType <= 127)
            {
                if (previousEventType.HasValue)
                {
                    eventType = previousEventType.Value;
                    reader.BaseStream.Seek(-1, SeekOrigin.Current);
                }
                else
                {
                    throw new Exception("MIDI missing event type");
                }
            }
            previousEventType = eventType;
            if (eventType == 0xF0 || eventType == 0xF7)
            {
                var length = reader.ReadVInt32();
                var bytes = reader.ReadBytes(length);
                // Console.WriteLine($"MIDI message {eventType:x} length {length} value {BitConverter.ToString(bytes)}");
                return new SysExEvent { bytes = bytes };
            }
            else if (eventType == 0xFF) // meta-event
            {
                var type = reader.ReadByte();
                var length = reader.ReadVInt32();
                if (type == 84 && length == 5)
                {
                    var bytes = reader.ReadBytes(5);
                    var hours = bytes[0] & 0x1F;
                    var frB = (bytes[0] & 0x60) >> 5;
                    if ((hours | bytes[1] | bytes[2] | bytes[3] | bytes[4]) > 0)
                    {
                        // throw new NotImplementedException("Unknown SMPTE Offset");
                    }
                }
                else if (type == 81 && length == 3)
                {
                    var micro = (reader.ReadByte() << 16) + reader.ReadUInt16();
                    return new TempoEvent { MicrosecondsPerQuarterNote = micro };
                }
                else if (type == 33 && length == 1)
                {
                    // supposed to specify the MIDI port number https://www.mixagesoftware.com/en/midikit/help/HTML/meta_events.html
                    reader.ReadByte();
                }
                else if (type == 88 && length == 4)
                {
                    var num = reader.ReadByte();
                    var denom = 1 << reader.ReadByte();
                    var metro = reader.ReadByte();
                    var n32 = reader.ReadByte();
                    if (metro != 24) throw new Exception("Memtronome not 24");
                    if (n32 != 8) throw new Exception("32nd note not 8");
                    var e = new TimingEvent
                    {
                        Numerator = num,
                        Denominator = denom
                    };
                    // Console.WriteLine($"Time signature: {num} {denom}");
                    return e;
                }
                else if (type == 89 && length == 2)
                {
                    Console.WriteLine($"Key signature: {reader.ReadByte()} {reader.ReadByte()}");
                }
                else if (type == 47 && length == 0) // end of track
                {
                }
                else if (type == 2)
                {
                    Console.WriteLine($"Copyright: {Encoding.ASCII.GetString(reader.ReadBytes(length))}");
                }
                else if (type == 3)
                {
                    track.Name = Encoding.ASCII.GetString(reader.ReadBytes(length));
                    return null;
                }
                else if (type == 4)
                    Console.WriteLine($"Instrument: {Encoding.ASCII.GetString(reader.ReadBytes(length))}");
                else if (type == 5) { reader.BaseStream.Seek(length, SeekOrigin.Current); } // lyric event
                else if (type == 1) // text event
                {
                    // These are usually not useful. Things like lighting events or section markers
                    reader.BaseStream.Seek(length, SeekOrigin.Current);
                    // return new TextEvent
                    // {
                    //     Text = Encoding.ASCII.GetString(reader.ReadBytes(length)),
                    //     Type = type,
                    //     delta = delta
                    // };
                }
                else
                {
                    // see https://www.music.mcgill.ca/~ich/classes/mumt306/StandardMIDIfileformat.html#BM3_
                    // see https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.mid/Core%20Infrastructure.md#meta-events
                    // throw new NotImplementedException($"Unknown meta-event {type} length {length}");
                    var bytes = reader.ReadBytes(length);
                    Console.WriteLine($"Unknown meta-event {type} length {length}, {(bytes.Length < 20 ? BitConverter.ToString(bytes) : null)}");
                }
            }
            else // midi event
            {
                var midiEventType = (byte)(eventType >> 4);
                var midiChannel = (byte)(eventType & 0xF);
                var parameter1 = reader.ReadByte();
                var hasP2 = midiEventType switch
                {
                    8 => true,
                    9 => true,
                    0xB => true,
                    0xC => false,
                    _ => throw new Exception($"Unknown MIDI event {midiEventType}")
                };
                var parameter2 = hasP2 ? reader.ReadByte() : (byte)0;
                return new MidiEvent
                {
                    channel = midiChannel,
                    type = midiEventType,
                    parameter1 = parameter1,
                    parameter2 = parameter2,
                };
            }
            return null;
        }
    }
}

