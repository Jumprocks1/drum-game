using System;

namespace DrumGame.Game.Interfaces;

public interface ICanBind<T> : IDefault<T>
{
    static abstract T Parse(string str);
    Action Changed { get; set; }
}