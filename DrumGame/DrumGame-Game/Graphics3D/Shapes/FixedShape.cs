using System.Collections.Generic;
using osuTK;

namespace DrumGame.Game.Graphics3D.Shapes;

public class FixedShape : IShape
{
    public IList<float> Positions { get; }
    public IList<int> Indicies { get; }
    readonly float[] Normals;
    float[] IShape.GetNormals() => Normals;
    public FixedShape(float[] positions, int[] indicies, float[] normals)
    {
        Positions = positions;
        Indicies = indicies;
        Normals = normals;
    }
}
