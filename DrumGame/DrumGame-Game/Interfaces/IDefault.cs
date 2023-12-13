namespace DrumGame.Game.Interfaces;

public interface IDefault<T>
{
    static abstract T Default { get; }
}