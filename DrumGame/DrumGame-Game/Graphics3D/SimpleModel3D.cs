using System;
using DrumGame.Game.Graphics3D.View;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics.ES30;

namespace DrumGame.Game.Graphics3D;

public abstract class SimpleModel3D : IModel3D, IDisposable
{
    public abstract float[] GetVertices();
    public abstract int[] GetIndicies();
    public virtual float[] GetNormals() => null;
    int IndexCount;
    public Matrix4 ModelMatrix = Matrix4.Identity;
    public Vector3 Colour = Colour4.AntiqueWhite.Vector3();
    public Vector3 EmissiveColour = Vector3.Zero;
    bool initialized;
    int vertexBufferObject;
    int normalBuffer;
    int vertexArrayObject;
    int elementBuffer;
    DrawElementsType ElementsType = DrawElementsType.UnsignedInt;
    public virtual void Init()
    {
        initialized = true;
        vertexArrayObject = GL.GenVertexArray();
        GL.BindVertexArray(vertexArrayObject); // don't have to unbind vertex array since we render right after this

        var vertices = GetVertices();
        vertexBufferObject = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        var normals = GetNormals();
        if (normals != null)
        {
            normalBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, normalBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, normals.Length * sizeof(float), normals, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(1);
        }


        var indicies = GetIndicies();
        if (indicies == null)
        {
            IndexCount = vertices.Length / 3;
        }
        else
        {
            IndexCount = indicies.Length;
            elementBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, elementBuffer);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indicies.Length * sizeof(int), indicies, BufferUsageHint.StaticDraw);
        }
    }

    public virtual void Draw(RenderContext context)
    {
        if (!initialized) Init();

        var transform = ModelMatrix * context.Transform;
        GL.UniformMatrix4(context.ModelMatrix, false, ref transform);

        GL.Uniform3(context.m_Colour, ref Colour);
        GL.Uniform3(context.m_EmissiveColour, ref EmissiveColour);

        context.Renderer.WhitePixel.Bind();

        GL.BindVertexArray(vertexArrayObject);
        if (elementBuffer == 0)
        {
            GL.DrawArrays(PrimitiveType.Triangles, 0, IndexCount);
        }
        else
        {
            GL.DrawElements(PrimitiveType.Triangles, IndexCount, ElementsType, (IntPtr)0);
        }
    }

    public void Dispose()
    {
        if (!initialized) return;
        GL.DeleteBuffer(elementBuffer);
        GL.DeleteBuffer(normalBuffer);
        GL.DeleteBuffer(vertexBufferObject);
        GL.DeleteVertexArray(vertexArrayObject);
    }
}

