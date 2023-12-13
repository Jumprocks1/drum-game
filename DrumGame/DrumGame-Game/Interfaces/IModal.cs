using System;
using osu.Framework.Graphics;

namespace DrumGame.Game.Interfaces;

public interface IModal : IDisposable, IDrawable
{
    Action CloseAction { set; get; }
}