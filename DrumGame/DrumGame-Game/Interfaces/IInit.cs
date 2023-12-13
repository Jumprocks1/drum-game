namespace DrumGame.Game.Interfaces;

// Useful with JSON since we can't use the constructor for this type of logic
// This should be called after deserialization
public interface IInit
{
    public void Init();
}