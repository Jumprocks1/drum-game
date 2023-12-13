using System;
using System.Collections.Generic;
using System.Diagnostics;
using DrumGame.Game.Graphics3D.View;
using DrumGame.Game.Utils;
using glTFLoader.Schema;
using osuTK;
using osuTK.Graphics.ES30;
using Texture = osu.Framework.Graphics.Textures.Texture;

namespace DrumGame.Game.Graphics3D;

public class glTFPrimitive : IDisposable, IModel3D
{
    const double GoldenRatio = 1.61803398874989484820458683436;
    public Texture Texture;
    int VertexCount;
    protected bool initialized;
    int VertexArrayObject;
    List<int> Buffers = new();
    DrawElementsType ElementsType;
    public Matrix4 ModelMatrix;
    Vector3 BaseColour;
    Vector3 EmissiveColour;


    int CreateBuffer(Accessor accessor, Gltf model, byte[] buffer, BufferTarget bufferTarget = BufferTarget.ArrayBuffer)
    {
        var bufferView = model.BufferViews[(int)accessor.BufferView.Value];
        var pos = accessor.ByteOffset + bufferView.ByteOffset;
        var glBuffer = GL.GenBuffer();
        GL.BindBuffer(bufferTarget, glBuffer);
        GL.BufferData(bufferTarget, bufferView.ByteLength, ref buffer[pos], BufferUsageHint.StaticDraw);
        Buffers.Add(glBuffer);
        return glBuffer;
    }

    public void Init(MeshPrimitive primitive, glTFScene scene)
    {
        // we are going to force the same texture for emissive and baseColor
        // we will load the texture from baseColor, and just ignore the emissive texture
        // the shader will handle the rest

        initialized = true;
        Debug.Assert(primitive.Mode == MeshPrimitive.ModeEnum.TRIANGLES);

        var model = scene.model;
        var buffer = scene.buffer;
        var material = model.Materials[primitive.Material.Value];

        var pbr = material.PbrMetallicRoughness;

        var textureInfo = pbr.BaseColorTexture;
        var hasTexture = textureInfo != null;
        Texture = hasTexture ? scene.textureLoader.GetTexture(textureInfo) : GLUtil.Renderer.WhitePixel;
        BaseColour = new Vector3(pbr.BaseColorFactor[0], pbr.BaseColorFactor[1], pbr.BaseColorFactor[2]);
        EmissiveColour = new Vector3(material.EmissiveFactor[0], material.EmissiveFactor[1], material.EmissiveFactor[2]);

        // TODO: roughnessFactor 

        var indices = model.Accessors[primitive.Indices.Value];
        VertexCount = indices.Count;
        ElementsType = indices.ComponentType.ElementsType();

        VertexArrayObject = GL.GenVertexArray();
        GL.BindVertexArray(VertexArrayObject);

        CreateBuffer(indices, model, buffer, BufferTarget.ElementArrayBuffer);

        foreach (var attribute in primitive.Attributes)
        {
            var position = GLUtil.AttribPosition(attribute.Key);
            var accessor = model.Accessors[attribute.Value];
            var type = accessor.ComponentType;

            // we don't want texture coords when we are using white pixel
            if (!hasTexture && position == GLUtil.GLAttribPosition.TexCoord) continue;

            var glBuffer = CreateBuffer(accessor, model, buffer);

            var size = accessor.Type.Size();

            if (position == GLUtil.GLAttribPosition.Joints) // joint ids
            {
                GL.VertexAttribIPointer((int)position, size,
                    (VertexAttribIntegerType)accessor.ComponentType,
                    size * accessor.ComponentType.Size(), IntPtr.Zero);
            }
            else
            {
                GL.VertexAttribPointer((int)position, size,
                    (VertexAttribPointerType)accessor.ComponentType,
                    false, size * accessor.ComponentType.Size(), IntPtr.Zero);
            }
            GL.EnableVertexAttribArray((int)position);
        }

        // we have to unbind since osu-framework doesn't use vertex arrays
        GL.BindVertexArray(0);
    }

    public void Draw(RenderContext context)
    {
        if (!initialized) return;

        var transform = ModelMatrix * context.Transform; // could cache this
        GL.UniformMatrix4(context.ModelMatrix, false, ref transform);

        // could buffer PBR uniforms
        GL.Uniform3(context.m_Colour, ref BaseColour);
        GL.Uniform3(context.m_EmissiveColour, ref EmissiveColour);

        Texture?.Bind();

        GL.BindVertexArray(VertexArrayObject);
        GL.DrawElements(PrimitiveType.Triangles, VertexCount, ElementsType, (IntPtr)0);
    }

    public void Dispose()
    {
        if (!initialized) return;
        foreach (var buffer in Buffers) GL.DeleteBuffer(buffer);
        GL.DeleteVertexArray(VertexArrayObject);
    }
}

