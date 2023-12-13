using System.Collections.Generic;
using osuTK;

namespace DrumGame.Game.Graphics3D.Shapes;

public class Triangle : IShape
{
    public IList<float> Positions { get; protected set; }
    public IList<int> Indicies { get; protected set; }
    public Triangle()
    {
        Positions = new float[] {
                -1, -1, 0,
                0, 1, 0,
                1, -1, 0
            };
        Indicies = new[] { 0, 1, 2 };
    }
}
