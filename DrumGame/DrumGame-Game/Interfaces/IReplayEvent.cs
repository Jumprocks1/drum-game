using System;

namespace DrumGame.Game.Interfaces;

public interface IReplayEvent : IComparable<double>
{
    public double Time { get; }
    int IComparable<double>.CompareTo(double other) => Time.CompareTo(other);
}