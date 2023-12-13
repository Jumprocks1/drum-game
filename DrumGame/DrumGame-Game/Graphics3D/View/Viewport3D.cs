using System;
using System.Collections.Generic;
using DrumGame.Game.Commands;
using DrumGame.Game.Graphics3D.Shapes;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics.ES30;

namespace DrumGame.Game.Graphics3D.View;

public partial class Viewport3D : Drawable
{
    public const string VertexShader = "sh_Shaders/sh_BoneVertex.vs";
    public const string FragmentShader = "sh_Shaders/sh_Frag.fs";

    [Resolved] CommandController command { get; set; }
    [Resolved] DrumShaderManager Shaders { get; set; }

    private IShader shader;
    public IShader Shader
    {
        get => shader; set
        {
            if (shader == value) return;
            shader = value;
            if (DrawNode != null) DrawNode.RenderContext = null;
        }
    }

    protected override void LoadComplete()
    {
        InputManager = GetContainingInputManager();
        base.LoadComplete();
    }

    protected override void Update()
    {
        UpdateCameraControls();
        UpdateMouseControl();
        DrawNode?.ApplyState();
        base.Update();
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        // this can be assigned externally to override
        Shader ??= Shaders.Load(VertexShader, FragmentShader);
        command.RegisterHandlers(this);
    }
    protected override void Dispose(bool isDisposing)
    {
        command.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        foreach (var control in Controls)
        {
            if (control.OnKeyPress(e.Key)) return true;
        }
        return base.OnKeyDown(e);
    }

    // normally this will be called 3 times.
    // not sure why osu-framework uses multiple drawnode trees, but I guess it has to do with update/draw thread
    // in the future we could probably let it create 3 drawnodes, but have shared buffer data somewhere
    protected override DrawNode CreateDrawNode() => DrawNode ??= new Viewport3DDrawNode(this);

    Viewport3DDrawNode DrawNode;
    private List<IModel3D> Models = new();

    public Camera Camera = new Camera
    {
        Position = new Vector3(0, 0, -4f),
    };

    public void Add(IModel3D model)
    {
        if (model is IViewportControl vc) Controls.Add(vc);
        Models.Add(model);
    }
    public void Add(IShape shape) => Add(new ShapeModel3D(shape));


    // not sure how to get masking to work, but maybe I don't care really
    // can probably just draw this behind everything else
    public class Viewport3DDrawNode : DrawNode
    {
        protected IShader Shader => Source.Shader;
        public Viewport3DDrawNode(Viewport3D source) : base(source)
        {
            lastTime = time;
        }
        protected Matrix4 CameraMatrix { get; private set; }
        protected new Viewport3D Source => (Viewport3D)base.Source;

        double time => Source.Time.Current;
        double lastTime;
        public override void ApplyState()
        {
            base.ApplyState();
            Source.Camera.TargetQuad = Source.ScreenSpaceDrawQuad;
            Source.Camera.GlobalProjection = GLUtil.Renderer.ProjectionMatrix;

            CameraMatrix = Source.Camera.Matrix4;
        }

        List<IModel3D> Models => Source.Models;

        public RenderContext RenderContext;

        protected override void Dispose(bool isDisposing)
        {
            for (var i = 0; i < Models.Count; i++) Models[i].Dispose();
            Source.Models = null;
            base.Dispose(isDisposing);
        }

        public override void Draw(IRenderer renderer)
        {
            base.Draw(renderer);

            if (Shader.IsLoaded) Shader.Bind();
            if (RenderContext == null)
            {
                RenderContext = new RenderContext(Shader);
            }
            RenderContext.Time = time;
            RenderContext.Delta = time - lastTime;
            lastTime = time;

            renderer.PushDepthInfo(new DepthInfo(true, true, BufferTestFunction.LessThan));
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.Uniform1(RenderContext.g_BackbufferDraw, 0);
            GL.Enable(EnableCap.CullFace);

            var outMat = CameraMatrix;
            GL.UniformMatrix4(RenderContext.ProjectionMatrix, false, ref outMat);

            for (int i = 0; i < Models.Count; i++)
            {
                Models[i].Draw(RenderContext);
            }
            GL.Disable(EnableCap.CullFace);

            // osu-framework doesn't use vertex arrays, so we have to make sure to unbind after we render all our models
            // if we don't unbind, the framework will overwrite a bunch of our configurations
            GL.BindVertexArray(0);

            renderer.PopDepthInfo();
            Shader.Unbind();
        }
    }
}
public class RenderContext
{
    public int ModelMatrix;
    public int ProjectionMatrix;
    public int m_Colour;
    public int m_EmissiveColour;
    public int BoneMatrices;
    public int g_BackbufferDraw;
    public Matrix4 Transform = Matrix4.Identity;
    public IRenderer Renderer => GLUtil.Renderer;
    public RenderContext(IShader shader)
    {
        // have to pull first one with GetUniform since this calls EnsureShaderCompiled
        ProjectionMatrix = shader.GetUniform<Matrix4>("m_ProjMatrix").Location;
        var shaderId = shader.ProgramId();
        ModelMatrix = GL.GetUniformLocation(shaderId, "m_ModelMatrix");
        BoneMatrices = GL.GetUniformLocation(shaderId, "m_bone");

        // PBR
        m_Colour = GL.GetUniformLocation(shaderId, "m_Colour");
        m_EmissiveColour = GL.GetUniformLocation(shaderId, "m_EmissiveColour");

        g_BackbufferDraw = GL.GetUniformLocation(shaderId, "g_BackbufferDraw");
    }

    public double Time;
    public double Delta;
}

