using System;
using System.Collections.Generic;
using DrumGame.Game.Graphics3D.View;
using osuTK;

namespace DrumGame.Game.Graphics3D;

// supports binding ModelMatrix to bones from PrimaryModel
public class BindingScene : IModel3D
{
    public readonly glTFScene PrimaryModel;
    readonly Matrix4 PrimitiveMatrix;
    public Skin3D Skin => PrimaryModel.Skin;
    protected List<(int JointId, IModel3D Model)> Bindings = new();

    public void Add(int jointId, IModel3D model)
    {
        Bindings.Add((jointId, model));
    }

    public BindingScene(glTFScene primary)
    {
        PrimaryModel = primary;
        PrimitiveMatrix = primary.Children[0].ModelMatrix;
    }

    public virtual void Draw(RenderContext context)
    {
        var old = context.Transform;
        PrimaryModel.Draw(context);
        if (Skin != null)
        {
            for (int i = 0; i < Bindings.Count; i++)
            {
                var model = Bindings[i].Model;
                context.Transform = Skin.ModelSpace(Bindings[i].JointId) * PrimitiveMatrix * PrimaryModel.ModelMatrix * old;
                model.Draw(context);
            }
        }
        context.Transform = old;
    }

    public virtual void Dispose()
    {
        for (int i = 0; i < Bindings.Count; i++) Bindings[i].Model.Dispose();
        PrimaryModel.Dispose();
    }
}
