namespace DrumGame.Game.Beatmaps.Data;

public interface ICongruent<T>
{
    bool Congruent(T other);
}
