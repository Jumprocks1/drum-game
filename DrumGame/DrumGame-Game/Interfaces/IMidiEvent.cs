using Commons.Music.Midi;
using DrumGame.Game.Beatmaps.Data;

namespace DrumGame.Game.Interfaces;

public interface IMidiEvent : ITickTime
{
    MidiEvent MidiEvent();
}