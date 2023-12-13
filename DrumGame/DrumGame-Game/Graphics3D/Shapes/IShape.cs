using System.Collections.Generic;
using osuTK;

namespace DrumGame.Game.Graphics3D.Shapes;

public static class IShapeExtensions
{
    public static IShape Flat(this IShape shape)
    {
        var indicies = shape.Indicies;
        var positions = shape.Positions;
        var count = indicies.Count;
        var pos = new float[count * 3];
        var normals = new float[count * 3];
        for (int i = 0; i < count; i += 3)
        {
            var i0 = indicies[i];
            var i1 = indicies[i + 1];
            var i2 = indicies[i + 2];

            var p0 = new Vector3(
                positions[i0 * 3],
                positions[i0 * 3 + 1],
                positions[i0 * 3 + 2]
            );
            var p1 = new Vector3(
                positions[i1 * 3],
                positions[i1 * 3 + 1],
                positions[i1 * 3 + 2]
            );
            var p2 = new Vector3(
                positions[i2 * 3],
                positions[i2 * 3 + 1],
                positions[i2 * 3 + 2]
            );
            var A = p1 - p0;
            var B = p2 - p0;
            var normal = new Vector3(
                A.Y * B.Z - A.Z * B.Y,
                A.Z * B.X - A.X * B.Z,
                A.X * B.Y - A.Y * B.X
            );

            pos[i * 3 + 0] = p0.X;
            pos[i * 3 + 1] = p0.Y;
            pos[i * 3 + 2] = p0.Z;
            pos[i * 3 + 3] = p1.X;
            pos[i * 3 + 4] = p1.Y;
            pos[i * 3 + 5] = p1.Z;
            pos[i * 3 + 6] = p2.X;
            pos[i * 3 + 7] = p2.Y;
            pos[i * 3 + 8] = p2.Z;
            normals[i * 3 + 0] = normal.X;
            normals[i * 3 + 1] = normal.Y;
            normals[i * 3 + 2] = normal.Z;
            normals[i * 3 + 3] = normal.X;
            normals[i * 3 + 4] = normal.Y;
            normals[i * 3 + 5] = normal.Z;
            normals[i * 3 + 6] = normal.X;
            normals[i * 3 + 7] = normal.Y;
            normals[i * 3 + 8] = normal.Z;
        }
        return new FixedShape(pos, null, normals);
    }
}
public interface IShape
{
    public IList<float> Positions { get; }
    public IList<int> Indicies { get; }
    public virtual float[] GetNormals() => null;
}
