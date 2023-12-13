using osuTK;

namespace DrumGame.Game.Graphics3D.IK;

public interface IConstraint
{
    public Vector3 Constrain(FabrikSolver solver, int i);
    public Vector3 ConstrainReverse(FabrikSolver solver, int i);
}