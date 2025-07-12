using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace DrumGame.Game.IO.Midi;

public class MidiFile
{
    public void ToJson(string outputPath)
    {
        using var outputStream = File.OpenWrite(outputPath);
        using var writer = new StreamWriter(outputStream);
        new JsonSerializer().Serialize(writer, this);
    }
    public List<MidiTrack> Tracks;
    public MidiHeader Header;
    public MidiFile(Stream stream) : this(new MidiReader(stream)) { }
    public MidiFile(MidiReader reader)
    {
        var tracks = new List<MidiTrack>();
        Chunk chunk;
        while ((chunk = Chunk.Read(reader)) != null)
        {
            if (chunk is MidiHeader h)
            {
                Header = h;
            }
            else if (chunk is MidiTrack t)
            {
                tracks.Add(t);
            }
        }
        Tracks = tracks;
    }
    public static MidiFile Read(MidiReader reader) => new MidiFile(reader);
    public enum ChunkType
    {
        Unknown,
        Header,
        Track
    }
    public abstract class Chunk
    {
        public static Chunk Read(MidiReader reader)
        {
            while (true)
            {
                var typeBytes = reader.ReadBytes(4);
                if (typeBytes.Length == 0) return null;
                var typeString = Encoding.ASCII.GetString(typeBytes);
                var length = reader.ReadInt32();
                var type = typeString switch
                {
                    "MThd" => ChunkType.Header,
                    "MTrk" => ChunkType.Track,
                    _ => ChunkType.Unknown
                };
                if (type == ChunkType.Unknown)
                {
                    reader.BaseStream.Seek(length, SeekOrigin.Current);
                    Console.WriteLine($"Unknown chunk type: {typeString}");
                    continue;
                }
                Chunk read = type switch
                {
                    ChunkType.Header => MidiHeader.Read(reader),
                    // returns null for skipped tracks, we should keep reading in that case
                    ChunkType.Track => MidiTrack.Read(reader, length),
                    _ => throw new NotImplementedException()
                };
                if (read == null) continue;
                return read;
            }
        }
    }
}

