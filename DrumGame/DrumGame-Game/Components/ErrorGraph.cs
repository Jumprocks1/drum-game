using System;
using System.Collections.Generic;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osuTK.Graphics;
using osuTK.Graphics.ES30;

namespace DrumGame.Game.Graphics3D.View;

public class ErrorGraph : Drawable
{
    public const int SampleCount = 64;
    public const string VertexShader = VertexShaderDescriptor.TEXTURE_2;
    public const string FragmentShader = "sh_Shaders/sh_ErrorGraph.fs";

    public float[] Samples = new float[SampleCount];

    public float MaxError = 145;
    float Offset;
    public void UpdateData(List<(double beat, double time)> data, double currentTime, double slope, double intercept)
    {
        MaxError = 50;
        var currentBeat = (currentTime - intercept) / slope;
        var beat = (int)currentBeat;
        var beatStart = Math.Max(0, beat - 32);
        var beatEnd = beatStart + 64;
        for (var i = 0; i < SampleCount; i++) Samples[i] = float.NaN;
        foreach (var (b, t) in data)
        {
            var error = t - (b * slope + intercept);
            if (Math.Abs(error) > MaxError) MaxError = (float)Math.Abs(error);
            if (b >= beatStart && b < beatEnd)
            {
                Samples[(int)b - beatStart] = (float)error;
            }
        }
        Offset = beat < 32 ? 0 : (float)(-(currentBeat - beat) / SampleCount);
    }

    [Resolved] DrumShaderManager Shaders { get; set; }
    private IShader shader;
    public IShader Shader
    {
        get => shader; set
        {
            if (shader == value) return;
            shader = value;
            if (DrawNode != null) DrawNode.Context = null;
        }
    }

    protected override void Update()
    {
        DrawNode?.ApplyState();
        base.Update();
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        var random = new Random();
        // this can be assigned externally to override
        Shader ??= Shaders.Load(VertexShader, FragmentShader);
        for (var i = 0; i < SampleCount; i++) Samples[i] = float.NaN;
    }
    protected override DrawNode CreateDrawNode() => DrawNode ??= new ErrorGraphDrawNode(this);
    ErrorGraphDrawNode DrawNode;

    public class ErrorGraphDrawNode : DrawNode
    {
        public class DrawContext
        {
            public Uniform<float> AspectRatio;
            public Uniform<float> Scale;
            public Uniform<float> Offset;
            public int m_Samples;
            public DrawContext(IShader shader)
            {
                AspectRatio = shader.GetUniform<float>("m_AspectRatio");
                Scale = shader.GetUniform<float>("m_Scale");
                Offset = shader.GetUniform<float>("m_Offset");
                m_Samples = GL.GetUniformLocation(shader.ProgramId(), "m_Samples");
            }
        }
        protected IShader Shader => Source.Shader;
        public ErrorGraphDrawNode(ErrorGraph source) : base(source) { }
        protected new ErrorGraph Source => (ErrorGraph)base.Source;
        public DrawContext Context;
        Quad ScreenSpaceDrawQuad;
        float MaxError;
        float Offset;
        const float Thickness = 0.015f; // from shader

        public override void ApplyState()
        {
            base.ApplyState();
            ScreenSpaceDrawQuad = Source.ScreenSpaceDrawQuad;
            MaxError = Source.MaxError;
            Offset = Source.Offset;
        }

        public override void Draw(IRenderer renderer)
        {
            base.Draw(renderer);

            if (!Shader.IsLoaded || renderer.WhitePixel?.Available != true) return;
            Shader.Bind();

            Context ??= new DrawContext(Shader);
            var asp = ScreenSpaceDrawQuad.Width / ScreenSpaceDrawQuad.Height;
            Context.AspectRatio.UpdateValue(ref asp);

            var scale = (MaxError * 2) * asp * (1 + Thickness * 2 * asp);
            Context.Scale.UpdateValue(ref scale);
            Context.Offset.UpdateValue(ref Offset);

            GL.Uniform1(Context.m_Samples, SampleCount, Source.Samples);

            renderer.DrawQuad(renderer.WhitePixel, ScreenSpaceDrawQuad, new ColourInfo
            {
                TopLeft = new SRGBColour { Linear = new Color4(0, 0, 0, 0) },
                TopRight = new SRGBColour { Linear = new Color4(1f, 0, 0, 0) },
                BottomLeft = new SRGBColour { Linear = new Color4(0, 1f, 0, 0) },
                BottomRight = new SRGBColour { Linear = new Color4(1f, 1f, 0, 0) },
            });

            Shader.Unbind();
        }
    }
}