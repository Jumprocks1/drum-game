using System;

namespace DrumGame.Game.Interfaces;

// Useful with BindableJson
public interface IChangedEvent
{
    public event Action Changed;
}