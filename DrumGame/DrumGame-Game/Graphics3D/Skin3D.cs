using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DrumGame.Game.Utils;
using osuTK;

namespace DrumGame.Game.Graphics3D;

public class JointState
{
    public Vector3 Scale;
    public Vector3 Translation;
    public Quaternion Rotation;
    public Matrix4 Transform() => Matrix4.CreateScale(Scale)
        * Matrix4.CreateFromQuaternion(Rotation)
        * Matrix4.CreateTranslation(Translation);
}
public class Joint
{
    public Vector3 Scale => State.Scale;
    public Vector3 Translation => State.Translation;
    public Quaternion Rotation { get => State.Rotation; set => State.Rotation = value; }
    public void Set(Matrix4 mat)
    {
        State.Scale = mat.ExtractScale();
        State.Translation = mat.ExtractTranslation();
        State.Rotation = mat.ExtractRotation();
    }
    public JointState State;
    public Matrix4 Binding;
    public Matrix4 Transform() => State.Transform();
    public Matrix4 RotationMatrix() => Matrix4.CreateFromQuaternion(State.Rotation);
    public int[] Children;
}
public class Skin3D
{
    public Joint[] Joints;
    public Joint this[int jointId] { get => Joints[jointId]; }
    public Joint this[string joint] { get => Joints[JointDictionary[joint]]; }
    public int[] Parent;
    public Dictionary<string, int> JointDictionary = new();
    public Animation3D[] Animations;

    public Dictionary<int, int> BoneMapping = new(); // can be cleared after we finish loading

    public const int Root = 0;

    public Matrix4[] FinalPoses;

    public Matrix4 ArmatureTransform;

    public Skin3D(glTFLoader.Schema.Skin skin, glTFScene scene)
    {
        var model = scene.model;
        var accessor = model.Accessors[skin.InverseBindMatrices.Value];

        using var stream = scene.GetStream(accessor);
        using var binaryReader = new BinaryReader(stream);

        FinalPoses = new Matrix4[skin.Joints.Length];
        Joints = new Joint[skin.Joints.Length];
        Parent = new int[skin.Joints.Length];

        var armature = scene.Parent[skin.Joints[Root]];
        ArmatureTransform = scene.globalTransforms[armature];

        for (int i = 0; i < skin.Joints.Length; i++)
        {
            var nodeId = skin.Joints[i];
            var node = model.Nodes[nodeId];
            BoneMapping[nodeId] = i;
            Joints[i] = new Joint { Binding = binaryReader.ReadMatrix4(), State = new JointState() };
            Joints[i].Set(scene.transforms[nodeId]); // could optimize this by storing Mat4 in JointState
            var name = model.Nodes[nodeId].Name;
            JointDictionary[name] = i;
            var under = name.LastIndexOf('_');
            if (under >= 0)
            {
                JointDictionary[name.Substring(0, under)] = i;
            }
        }
        Parent[Root] = -1;
        for (int i = 0; i < skin.Joints.Length; i++)
        {
            var node = model.Nodes[skin.Joints[i]];
            if (node.Children != null)
            {
                foreach (var child in node.Children) Parent[BoneMapping[child]] = i;
                Joints[i].Children = node.Children.Select(e => BoneMapping[e]).ToArray();
            }
        }
        Animations = new Animation3D[model.Animations?.Length ?? 0];
        for (int i = 0; i < Animations.Length; i++)
        {
            Animations[i] = new Animation3D(model.Animations[i], this, scene);
        }
    }


    public void ComputeFinalPoses() => ComputeFinalPoses(Root, Matrix4.Identity);
    public void ComputeFinalPoses(int jointId, Matrix4 globalTransform)
    {
        var joint = Joints[jointId];
        globalTransform = joint.Transform() * globalTransform;
        FinalPoses[jointId] = joint.Binding * globalTransform;
        if (joint.Children != null)
        {
            foreach (var child in joint.Children) ComputeFinalPoses(child, globalTransform);
        }
    }
    public Matrix4 ModelSpace(int jointId)
    {
        var joint = Joints[jointId];
        if (jointId == Root) return joint.Transform() * ArmatureTransform;
        return joint.Transform() * ModelSpace(Parent[jointId]);
    }
    public Matrix4 GlobalTransform(string name) => ModelSpace(JointDictionary[name]);
    public int GetId(string name) => JointDictionary[name];
}
