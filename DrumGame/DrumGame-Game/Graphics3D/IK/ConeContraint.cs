using System;
using osuTK;

namespace DrumGame.Game.Graphics3D.IK;

public class ConeConstraint : IConstraint
{
    public float Dot; // -1 => no constraint, 0 => hemisphere
    public float Angle;
    public ConeConstraint(float radians)
    {
        Angle = radians;
        Dot = (float)Math.Cos(radians);
    }

    public Vector3 Constrain(FabrikSolver solver, int i)
    {
        var previousDir = (i == 1 ? solver.Positions[0] : solver.Positions[i - 1] - solver.Positions[i - 2]).Normalized();
        var dir = (solver.Positions[i] - solver.Positions[i - 1]).Normalized();
        var dot = Vector3.Dot(dir, previousDir);
        if (dot < Dot)
        {
            var axis = Vector3.Cross(previousDir, dir);
            var rot = Matrix3.CreateFromAxisAngle(axis, Angle);
            dir = previousDir * rot;
        }
        return solver.Positions[i - 1] + dir * solver.Lengths[i - 1];
    }
    public Vector3 ConstrainReverse(FabrikSolver solver, int i)
    {
        var dir = (solver.Positions[i] - solver.Positions[i + 1]).Normalized();
        var target = solver.Positions[i + 1] + dir * solver.Lengths[i];
        var newPreviousDir = (solver.Positions[i - 1] - target).Normalized();
        var dot = Vector3.Dot(dir, newPreviousDir);
        if (dot < Dot)
        {
            var axis = Vector3.Cross(newPreviousDir, dir);

            var nearLength = solver.Lengths[i];
            var hypLength = Vector3.Distance(solver.Positions[i + 1], solver.Positions[i - 1]);

            var nearAngle = -Math.Asin(nearLength * Math.Sin(Angle) / hypLength) + Angle;

            dir = (solver.Positions[i - 1] - solver.Positions[i + 1]).Normalized() * Matrix3.CreateFromAxisAngle(axis, (float)nearAngle);
            target = solver.Positions[i + 1] + dir * solver.Lengths[i];
        }
        return target;
    }
}