using System;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osuTK;

namespace DrumGame.Game.Components;

// in general, this performs about 4x better on the draw thread than using raw components
// if set up properly, there is virtually no update thread overhead (easily 100x less than raw components)

// best used with things we know will change every frame
// Pretty sure performance will still be way better than regular components even if nothing changes
// The main downside here is we have to be careful how we send all the data during the update thread
public class Canvas<State> : Canvas where State : new()
{
    protected override DrawNode CreateDrawNode() => new Node(this);
    public Action<State> ApplyState;
    public Action<Node, State> Draw; // don't change this ever
    public class Node : CanvasNode
    {
        protected readonly new Canvas<State> Source; // don't use this on draw thread
        public State State = new();
        public Node(Canvas<State> source) : base(source)
        {
            Source = source;
        }

        public override void ApplyState()
        {
            base.ApplyState();
            Source.ApplyState(State);
        }
        protected override void BindUniformResources(IShader shader, IRenderer renderer)
        {
            if (shader == null || !shader.IsLoaded) return;
            Shader = shader;
            base.BindUniformResources(shader, renderer);
        }
        // TODO add opaque mode (not sure why we need, but I think it's good)
        // only use when a full background is drawn
        protected override void Draw(IRenderer renderer)
        {
            base.Draw(renderer);
            BindTextureShader(renderer);
            Renderer = renderer;

            Color = DrawColourInfo.Colour; // have to reset color on each render
            Source.Draw(this, State);

            Shader = null;
            Renderer = null;
            UnbindTextureShader(renderer);
        }
    }
}

public abstract class Canvas : Drawable, ITexturedShaderDrawable
{
    public bool Relative;
    public IShader TextureShader { get; set; }
    [BackgroundDependencyLoader]
    private void load(ShaderManager shaders)
    {
        // I don't fully understand the differences here
        // Defaults should work fine since we don't use EdgeSmoothness
        TextureShader ??= shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
    }
    protected override void Update()
    {
        Invalidate(Invalidation.DrawInfo);
    }
}

// This is nice since we can pass this reference around with the generic
public abstract class CanvasNode : TexturedShaderDrawNode
{
    public ColourInfo Color;
    public double Time; // need so that the clock time is identical for the entire frame
    public bool Relative;
    public float Width;
    public float Height;
    protected Quad ScreenSpaceDrawQuad { get; private set; }
    public CanvasNode(ITexturedShaderDrawable source) : base(source) { }
    public DrawInfo pDrawInfo => DrawInfo;
    public float RelativeAspectRatio;
    public Matrix3 Matrix;

    public CanvasNode(Canvas source) : base(source) { Source = source; }
    readonly new Canvas Source;
    public override void ApplyState()
    {
        base.ApplyState();
        Relative = Source.Relative;
        Width = Source.DrawWidth;
        Height = Source.DrawHeight;
        Time = Util.DrumGame.Clock.CurrentTime;
        RelativeAspectRatio = Width / Height;
        Matrix = DrawInfo.Matrix;
        if (Relative)
        {
            // not sure if this is safe yet, think it's fine
            Matrix.Row0.X *= Width;
            Matrix.Row1.Y *= Height;
        }
    }

    // should probably override ApplyState here, but we can't since we don't have access to canvas source

    public IRenderer Renderer; // only set while in DrawAction()
    public IShader Shader;
    // would be dope if we could do a circle here
    public void Box(float x, float y, float w, float h) =>
        Sprite(Renderer.WhitePixel, x, y, w, h);
    public void CenterBox(float x, float y, float w, float h) =>
        CenterSprite(Renderer.WhitePixel, x, y, w, h);
    public void SetBlend(BlendingParameters blend) => Renderer.SetBlend(blend);
    public void CenterSprite(Texture texture, float x, float y, float w, float h) =>
        Sprite(texture, x - w / 2, y - h / 2, w, h);
    public void Translate(Vector2 translate) => Translate(translate.X, translate.Y);
    public void Translate(float x, float y)
    {
        Matrix.Row2.X += x * Matrix.Row0.X;
        Matrix.Row2.Y += y * Matrix.Row1.Y;
    }
    public void Sprite(Texture texture, float x, float y, float w, float h)
    {
        if (texture?.Available != true)
            return;
        if (w == 0 || h == 0)
            return;

        Renderer.DrawQuad(texture, new Quad(x, y, w, h) * Matrix, Color);
    }
}