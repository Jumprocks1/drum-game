using System.Collections.Generic;
using osuTK;

namespace DrumGame.Game.Graphics3D.Shapes;

public class Sphere : Icosahedron
{
    public Sphere(float radius = 1, int subdivide = 1)
    {
        var positions = Positions as List<float> ?? new List<float>(Positions);
        Positions = positions;
        for (var i = 0; i < subdivide; i++)
            Indicies = Subdivide(positions, Indicies);
        if (subdivide > 0 || radius != 1)
        {
            for (var i = 0; i < Positions.Count; i += 3)
            {
                var length = radius / new Vector3(Positions[i], Positions[i + 1], Positions[i + 2]).Length;
                Positions[i] *= length;
                Positions[i + 1] *= length;
                Positions[i + 2] *= length;
            }
        }
    }
    public static List<int> Subdivide(List<float> positions, IList<int> indicies)
    {
        // note, this doesn't remove duplicate midpoints, it could, but I think the overhead isn't worth it
        var outIndicies = new List<int>(12 * indicies.Count);
        for (int i = 0; i < indicies.Count; i += 3)
        {
            var i0 = indicies[i];
            var i1 = indicies[i + 1];
            var i2 = indicies[i + 2];
            var m0 = positions.Count / 3;
            positions.Add((positions[i0 * 3] + positions[i1 * 3]) / 2);
            positions.Add((positions[i0 * 3 + 1] + positions[i1 * 3 + 1]) / 2);
            positions.Add((positions[i0 * 3 + 2] + positions[i1 * 3 + 2]) / 2);
            var m1 = m0 + 1;
            positions.Add((positions[i1 * 3] + positions[i2 * 3]) / 2);
            positions.Add((positions[i1 * 3 + 1] + positions[i2 * 3 + 1]) / 2);
            positions.Add((positions[i1 * 3 + 2] + positions[i2 * 3 + 2]) / 2);
            var m2 = m1 + 1;
            positions.Add((positions[i2 * 3] + positions[i0 * 3]) / 2);
            positions.Add((positions[i2 * 3 + 1] + positions[i0 * 3 + 1]) / 2);
            positions.Add((positions[i2 * 3 + 2] + positions[i0 * 3 + 2]) / 2);
            outIndicies.Add(i0);
            outIndicies.Add(m0);
            outIndicies.Add(m2);
            outIndicies.Add(i1);
            outIndicies.Add(m1);
            outIndicies.Add(m0);
            outIndicies.Add(i2);
            outIndicies.Add(m2);
            outIndicies.Add(m1);
            outIndicies.Add(m0);
            outIndicies.Add(m1);
            outIndicies.Add(m2);
        }
        return outIndicies;
    }
}
