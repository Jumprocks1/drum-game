using System.Linq;
using DrumGame.Game.Graphics3D.Shapes;

namespace DrumGame.Game.Graphics3D;

public class ShapeModel3D : SimpleModel3D
{
    public IShape Shape;
    public ShapeModel3D(IShape shape)
    {
        Shape = shape;
    }
    public override float[] GetNormals() => Shape.GetNormals();
    public override int[] GetIndicies()
    {
        var indicies = Shape.Indicies;
        if (indicies == null) return null;
        return (indicies as int[]) ?? indicies.ToArray();
    }
    public override float[] GetVertices()
    {
        var v = Shape.Positions;
        return (v as float[]) ?? v.ToArray();
    }
    public override void Init()
    {
        base.Init();
        Shape = null; // don't want to hold reference when we don't need it anymore
    }
}

