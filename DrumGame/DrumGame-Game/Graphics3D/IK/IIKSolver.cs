using osuTK;

namespace DrumGame.Game.Graphics3D.IK;
public interface IIKSolver
{
    public Vector3 Target { get; set; }
    public void ApplySolution();
}