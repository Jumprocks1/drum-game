using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DrumGame.Game.Graphics3D.glTF;
using DrumGame.Game.Graphics3D.View;
using DrumGame.Game.Utils;
using glTFLoader.Schema;
using osu.Framework.Graphics.OpenGL;
using osu.Framework.Threading;
using osuTK;

namespace DrumGame.Game.Graphics3D;

public class glTFScene : CompositeModel<glTFPrimitive>
{
    public bool Loaded;
    public byte[] buffer; // make sure to clear after we load into OpenGL
    public glTFTextureLoader textureLoader;
    public Matrix4[] globalTransforms;
    public Matrix4[] transforms;
    public int[] Parent;

    public Action AfterInit;

    public Skin3D Skin;

    public Gltf model;
    bool fileLoaded;
    public glTFScene(string absolutePath)
    {
        Task.Run(() =>
        {
            using var stream = File.OpenRead(absolutePath);
            model = glTFLoader.Interface.LoadModel(stream);
            buffer = glTFLoader.Interface.LoadBinaryBuffer(absolutePath);
            globalTransforms = new Matrix4[model.Nodes.Length];
            transforms = new Matrix4[model.Nodes.Length];
            textureLoader = new glTFTextureLoader(this);
            fileLoaded = true;
            GLUtil.Renderer.ScheduleExpensiveOperation(new ScheduledDelegate(Init));
            // AddInternal(new TriangleExample
            // {
            //     Texture = textureLoader.GetTexture(model.Materials[0].EmissiveTexture)
            // });
        });
    }

    protected glTFPrimitive mainModel = null;

    // must run on draw thread
    void Init()
    {
        if (!fileLoaded || Loaded) return;

        var scene = model.Scenes[model.Scene ?? 0];

        var meshNodes = new List<int>();

        Parent = new int[model.Nodes.Length];

        void processNode(int i, int parent)
        {
            Parent[i] = parent;
            var node = model.Nodes[i];
            transforms[i] = Matrix4.CreateScale(node.Scale[0], node.Scale[1], node.Scale[2])
                * Matrix4.CreateFromQuaternion(new Quaternion(node.Rotation[0], node.Rotation[1], node.Rotation[2], node.Rotation[3]))
                * Matrix4.CreateTranslation(node.Translation[0], node.Translation[1], node.Translation[2]);
            globalTransforms[i] = parent >= 0 ? transforms[i] * globalTransforms[parent] : transforms[i];

            if (node.Mesh.HasValue)
            {
                meshNodes.Add(i);
            }

            if (node.Children != null)
            {
                for (int j = 0; j < node.Children.Length; j++) processNode(node.Children[j], i);
            }
        }

        foreach (var i in scene.Nodes) processNode(i, -1);

        Skin = model.Skins != null ? new Skin3D(model.Skins[0], this) : null;

        foreach (var nodeId in meshNodes)
        {
            var node = model.Nodes[nodeId];
            var mesh = model.Meshes[node.Mesh.Value];
            foreach (var primitive in mesh.Primitives)
            {
                var gltfModel = mainModel = new glTFPrimitive();
                gltfModel.ModelMatrix = globalTransforms[nodeId];
                gltfModel.Init(primitive, this);
                AddInternal(gltfModel);
            }
        }

        transforms = null;
        globalTransforms = null;
        buffer = null;
        model = null;
        Parent = null;
        Loaded = true;
        AfterInit?.Invoke();
    }

    public Stream GetStream(int bufferViewId)
    {
        var bufferView = model.BufferViews[bufferViewId];
        return new MemoryStream(buffer, bufferView.ByteOffset, bufferView.ByteLength, false);
    }
    public Stream GetStream(Accessor accessor)
    {
        var bufferView = model.BufferViews[accessor.BufferView.Value];
        return new MemoryStream(buffer, bufferView.ByteOffset + accessor.ByteOffset, bufferView.ByteLength, false);
    }

    public override void Dispose()
    {
        textureLoader.Dispose();
        base.Dispose();
    }

    public override void Draw(RenderContext context)
    {
        if (Skin != null)
        {
            // Skin.Animations[2].ApplyTo(Skin, (context.Time / 1000 * 24));
            // Util.WriteJson(Skin.JointDictionary.Keys);
            Skin.ComputeFinalPoses();
            UnsafeUtil.UploadMatrices(context.BoneMatrices, Skin.FinalPoses);
        }
        base.Draw(context);
    }

    public Vector3 GetVector3(Accessor accessor, int i = 0)
    {
        var bufferView = model.BufferViews[accessor.BufferView.Value];
        var offset = accessor.ByteOffset + bufferView.ByteOffset + i * 12;
        return new Vector3(
            BitConverter.ToSingle(buffer, offset),
            BitConverter.ToSingle(buffer, offset + 4),
            BitConverter.ToSingle(buffer, offset + 8)
        );
    }
    public Quaternion GetQuat(Accessor accessor, int i = 0)
    {
        var bufferView = model.BufferViews[accessor.BufferView.Value];
        var offset = accessor.ByteOffset + bufferView.ByteOffset + i * 16;
        return new Quaternion(
            BitConverter.ToSingle(buffer, offset),
            BitConverter.ToSingle(buffer, offset + 4),
            BitConverter.ToSingle(buffer, offset + 8),
            BitConverter.ToSingle(buffer, offset + 12)
        );
    }
}

