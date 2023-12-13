using System.Collections.Generic;
using osuTK;

namespace DrumGame.Game.Graphics3D.Shapes;

public class Icosahedron : IShape
{
    public IList<float> Positions { get; protected set; }
    public IList<int> Indicies { get; protected set; }
    const float X = 0.525731112119133606f;
    const float Z = 0.850650808352039932f;
    float[] IShape.GetNormals()
    {
        var o = new float[Positions.Count];
        for (int i = 0; i < o.Length; i += 3)
        {
            var n = new Vector3(Positions[i], Positions[i + 1], Positions[i + 2]).Normalized();
            o[i] = n.X;
            o[i + 1] = n.Y;
            o[i + 2] = n.Z;
        }
        return o;
    }
    public Icosahedron()
    {
        Indicies = new[]{
                4, 0, 1,
                9, 0, 4,
                5, 9, 4,
                5, 4, 8,
                8, 4, 1,
                10, 8, 1,
                3, 8, 10,
                3, 5, 8,
                2, 5, 3,
                7, 2, 3,
                10, 7, 3,
                6, 7, 10,
                11, 7, 6,
                0, 11, 6,
                1, 0, 6,
                1, 6, 10,
                0, 9, 11,
                11, 9, 2,
                2, 9, 5,
                2, 7, 11
            };
        Positions = new[] {
                -X, 0f, Z,
                X, 0f, Z,
                -X, 0f, -Z,
                X, 0f, -Z,
                0f, Z, X,
                0f, Z, -X,
                0f, -Z, X,
                0f, -Z, -X,
                Z, X, 0f,
                -Z, X, 0f,
                Z, -X, 0f,
                -Z, -X, 0
            };
    }
}
