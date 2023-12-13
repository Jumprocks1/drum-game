using System;
using System.Collections.Generic;
using osuTK;

namespace DrumGame.Game.Graphics3D.IK;

public class NoConstraint : IConstraint
{
    public Vector3 Constrain(FabrikSolver solver, int i) =>
        solver.Positions[i - 1] + (solver.Positions[i] - solver.Positions[i - 1]).Normalized() * solver.Lengths[i - 1];
    public Vector3 ConstrainReverse(FabrikSolver solver, int i) =>
        solver.Positions[i + 1] + (solver.Positions[i] - solver.Positions[i + 1]).Normalized() * solver.Lengths[i];
}
public class FabrikSolver : IIKSolver
{
    public const int MAX_ITERATIONS = 50;
    Skin3D Skin;
    // Lengths.Length = Positions.Length - 1
    // Length between p[i] and p[i + 1] => Lengths[i]
    public float[] Lengths;
    public Vector3[] Positions;
    IConstraint[] Constraints;
    Matrix4 ModelSpace;
    Matrix4 RootTransform;
    List<int> chain = new();
    float TotalLength2;

    Vector3 _target;
    public Vector3 Target
    {
        get => _target; set
        {
            var t = (new Vector4(value, 1) * ModelSpace).Xyz;
            _target = t;
            Update();
        }
    }

    static Vector3 Transform(Vector3 vec, Matrix4 mat)
        => (new Vector4(vec, 1) * mat).Xyz;

    // end joint will not be included
    // for an arm, start will usually be shoulder/pectoral and endJoint will usually be the wrist
    public FabrikSolver(Skin3D skin, int startJoint, int endJoint)
    {
        Skin = skin;
        var length = 0f;
        var joint = endJoint;
        chain.Add(joint);
        while ((joint = skin.Parent[joint]) != startJoint) chain.Add(joint);
        chain.Add(joint);
        var count = chain.Count;
        chain.Reverse();

        var currentTransform = Matrix4.Identity; // relative to start joint
        RootTransform = skin[chain[0]].Transform();
        ModelSpace = skin.ModelSpace(chain[0]).Inverted();
        Positions = new Vector3[count]; // relative to start joint
        Lengths = new float[count - 1];
        Constraints = new IConstraint[count - 1];
        for (int i = 1; i < count; i++)
        {
            joint = chain[i];
            currentTransform = skin.Joints[joint].Transform() * currentTransform;
            Positions[i] = currentTransform.ExtractTranslation();
            length += Lengths[i - 1] = (Positions[i] - Positions[i - 1]).Length;
            Constraints[i - 1] = new NoConstraint();
        }
        TotalLength2 = length * length;
    }

    public static Quaternion FromToRotation(Vector3 aFrom, Vector3 aTo)
    {
        if (aFrom == aTo) return Quaternion.Identity;
        var axis = Vector3.Cross(aFrom, aTo);
        var angle = Vector3.CalculateAngle(aFrom, aTo);
        return Quaternion.FromAxisAngle(axis.Normalized(), angle);
    }

    public void ApplySolution()
    {
        var currentTransform = RootTransform;
        for (int i = 0; i < chain.Count - 1; i++)
        {
            var joint = Skin[chain[i]];
            joint.Rotation = FromToRotation(Skin[chain[i + 1]].Translation,
                Transform(Positions[i + 1], currentTransform) - joint.Translation);
            currentTransform *= joint.Transform().Inverted();
        }
    }


    public void Update()
    {
        // pole
        Positions[1] = new Vector3(-1, 1, -1);
        var error = 1f;
        for (int it = 0; it < MAX_ITERATIONS; it++)
        {
            // backwards
            Positions[^1] = Target;
            for (int i = Positions.Length - 2; i >= 1; i--)
            {
                Positions[i] = Constraints[i].ConstrainReverse(this, i);
            }
            // forwards
            Positions[0] = Vector3.Zero;
            for (int i = 1; i < Positions.Length; i++)
            {
                Positions[i] = Constraints[i - 1].Constrain(this, i);
            }
            var lastError = error;
            error = (Positions[^1] - Target).Length;
            if (error == lastError) break;
        }
    }
}
