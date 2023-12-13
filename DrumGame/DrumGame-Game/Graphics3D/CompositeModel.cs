using System.Collections.Generic;
using DrumGame.Game.Graphics3D.View;
using osuTK;

namespace DrumGame.Game.Graphics3D;

public class CompositeModel : CompositeModel<IModel3D> { }
public class CompositeModel<T> : IModel3D where T : IModel3D
{
    public IReadOnlyList<T> Children => InternalChildren;
    public Matrix4 ModelMatrix = Matrix4.Identity;
    protected List<T> InternalChildren = new();
    public virtual void Dispose()
    {
        for (int i = 0; i < InternalChildren.Count; i++) InternalChildren[i].Dispose();
        InternalChildren = null;
    }

    protected void AddInternal(T model) => InternalChildren.Add(model);

    public virtual void Draw(RenderContext context)
    {
        var old = context.Transform;
        context.Transform = ModelMatrix * context.Transform;
        for (int i = 0; i < InternalChildren.Count; i++) InternalChildren[i].Draw(context);
        context.Transform = old;
    }
}

