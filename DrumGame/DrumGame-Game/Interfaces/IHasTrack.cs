using DrumGame.Game.Timing;

namespace DrumGame.Game.Interfaces;

public interface IHasTrack
{
    public BeatClock Track { get; }
}