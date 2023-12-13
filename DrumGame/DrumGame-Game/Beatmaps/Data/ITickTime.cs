using System;

namespace DrumGame.Game.Beatmaps.Data;

public interface ITickTime : IComparable<int>
{
    public int Time { get; }
    int IComparable<int>.CompareTo(int other) => Time - other;
}
