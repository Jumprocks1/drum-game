using System;
using DrumGame.Game.Graphics3D.View;

namespace DrumGame.Game.Graphics3D;

public interface IModel3D : IDisposable
{
    public void Draw(RenderContext context);
}
