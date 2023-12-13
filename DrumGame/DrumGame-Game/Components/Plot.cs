using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Caching;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osuTK.Graphics.ES30;
using osuTK;
using osuTK.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Input.Events;
using DrumGame.Game.Containers;
using DrumGame.Game.Utils;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Rendering;
using DrumGame.Game.Interfaces;

public class Plot : Drawable, IBufferedDrawable, IHasMarkupTooltip
{
    public IShader RoundedTextureShader { get; private set; }
    public IShader TextureShader { get; private set; }
    private IShader pathShader;

    [BackgroundDependencyLoader]
    private void load(ShaderManager shaders)
    {
        RoundedTextureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE_ROUNDED);
        TextureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
        pathShader = shaders.Load(VertexShaderDescriptor.TEXTURE_3, FragmentShaderDescriptor.TEXTURE);
    }

    public float[] Vertices;

    public void Invalidate()
    {
        segmentsCache.Invalidate();
        Invalidate(Invalidation.DrawInfo);
    }

    private float pathRadius = 10f;
    public virtual float PathRadius
    {
        get => pathRadius;
        set
        {
            if (pathRadius == value) return;

            pathRadius = value;

            Invalidate();
        }
    }

    public override Axes RelativeSizeAxes
    {
        get => base.RelativeSizeAxes;
        set
        {
            if ((AutoSizeAxes & value) != 0)
                throw new InvalidOperationException("No axis can be relatively sized and automatically sized at the same time.");

            base.RelativeSizeAxes = value;
        }
    }

    private Axes autoSizeAxes;

    /// <summary>
    /// Controls which <see cref="Axes"/> are automatically sized w.r.t. the bounds of the vertices.
    /// It is not allowed to manually set <see cref="Size"/> (or <see cref="Width"/> / <see cref="Height"/>)
    /// on any <see cref="Axes"/> which are automatically sized.
    /// </summary>
    public virtual Axes AutoSizeAxes
    {
        get => autoSizeAxes;
        set
        {
            if (value == autoSizeAxes)
                return;

            if ((RelativeSizeAxes & value) != 0)
                throw new InvalidOperationException("No axis can be relatively sized and automatically sized at the same time.");

            autoSizeAxes = value;
            OnSizingChanged();
        }
    }

    private readonly List<Line> segmentsBacking = new List<Line>();
    private readonly Cached segmentsCache = new Cached();
    private List<Line> segments => segmentsCache.IsValid ? segmentsBacking : generateSegments();

    private List<Line> generateSegments()
    {
        segmentsBacking.Clear();

        if (Vertices != null && Vertices.Length > 1)
        {
            var scale = DrawWidth / Vertices.Length;
            for (int i = 0; i < Vertices.Length - 1; ++i)
                segmentsBacking.Add(new Line(new Vector2(i * scale, Vertices[i]), new Vector2((i + 1) * scale, Vertices[i + 1])));
        }

        segmentsCache.Validate();
        return segmentsBacking;
    }

    public DrawColourInfo? FrameBufferDrawColour => base.DrawColourInfo;

    public Vector2 FrameBufferScale { get; } = Vector2.One;

    // The path should not receive the true colour to avoid colour doubling when the frame-buffer is rendered to the back-buffer.
    public override DrawColourInfo DrawColourInfo => new DrawColourInfo(Color4.White, base.DrawColourInfo.Blending);

    public Color4 BackgroundColour => new Color4(0, 0, 0, 0);

    public string MarkupTooltip { get; set; }

    public Func<int, MouseEvent, string> SampleTooltip;

    protected override bool OnMouseMove(MouseMoveEvent e)
    {
        UpdateTooltip();
        return base.OnMouseMove(e);
    }

    public void UpdateTooltip()
    {
        var mousePosition = Util.Mouse.Position;
        var p = Parent.ToLocalSpace(mousePosition);
        var i = (int)(p.X / DrawWidth * Vertices.Length);
        if (i < Vertices.Length)
        {
            if (SampleTooltip == null)
                MarkupTooltip = $"({i}, {Vertices[i]})";
            else
                MarkupTooltip = SampleTooltip(i, null);
        }
    }

    public Action<int, ClickEvent> Clicked;
    protected override bool OnClick(ClickEvent e)
    {
        var p = Parent.ToSpaceOfOtherDrawable(e.MousePosition, this);
        var i = (int)(p.X / DrawWidth * Vertices.Length);
        Clicked?.Invoke(i, e);
        return base.OnClick(e);
    }

    private readonly BufferedDrawNodeSharedData sharedData = new BufferedDrawNodeSharedData(new[] { RenderBufferFormat.D16 }, clipToRootNode: true);

    protected override DrawNode CreateDrawNode() => new BufferedDrawNode(this, new PlotDrawNode(this), sharedData);

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        sharedData.Dispose();
    }

    public Func<int, Color4> GetColor;

    private class PlotDrawNode : DrawNode
    {
        public const int MAX_RES = 24;

        protected new Plot Source => (Plot)base.Source;

        private readonly List<Line> segments = new List<Line>();

        private Texture texture;
        private Vector2 drawSize;
        private float radius;
        private IShader pathShader;

        // We multiply the size param by 3 such that the amount of vertices is a multiple of the amount of vertices
        // per primitive (triangles in this case). Otherwise overflowing the batch will result in wrong
        // grouping of vertices into primitives.
        private readonly IVertexBatch<TexturedVertex3D> halfCircleBatch = GLUtil.Renderer.CreateLinearBatch<TexturedVertex3D>(MAX_RES * 100 * 3, 10, PrimitiveTopology.Triangles);
        private readonly IVertexBatch<TexturedVertex3D> quadBatch = GLUtil.Renderer.CreateQuadBatch<TexturedVertex3D>(200, 10);

        public PlotDrawNode(Plot source) : base(source) { }

        public override void ApplyState()
        {
            base.ApplyState();

            segments.Clear();
            segments.AddRange(Source.segments);

            texture = GLUtil.Renderer.WhitePixel;
            drawSize = Source.DrawSize;
            radius = Source.PathRadius;
            pathShader = Source.pathShader;
        }

        private Vector2 pointOnCircle(float angle) => new Vector2(MathF.Sin(angle), -MathF.Cos(angle));

        private Vector2 relativePosition(Vector2 localPos) => Vector2.Divide(localPos, drawSize);

        private Color4 colourAt(int index)
        {
            if (Source.GetColor != null) return Source.GetColor(index);
            return ((SRGBColour)DrawColourInfo.Colour).Linear;
        }

        private void addLineQuads(Line line, RectangleF texRect, int index)
        {
            Vector2 ortho = line.OrthogonalDirection;
            Line lineLeft = new Line(line.StartPoint + ortho * radius, line.EndPoint + ortho * radius);
            Line lineRight = new Line(line.StartPoint - ortho * radius, line.EndPoint - ortho * radius);

            Line screenLineLeft = new Line(Vector2Extensions.Transform(lineLeft.StartPoint, DrawInfo.Matrix), Vector2Extensions.Transform(lineLeft.EndPoint, DrawInfo.Matrix));
            Line screenLineRight = new Line(Vector2Extensions.Transform(lineRight.StartPoint, DrawInfo.Matrix), Vector2Extensions.Transform(lineRight.EndPoint, DrawInfo.Matrix));
            Line screenLine = new Line(Vector2Extensions.Transform(line.StartPoint, DrawInfo.Matrix), Vector2Extensions.Transform(line.EndPoint, DrawInfo.Matrix));

            quadBatch.Add(new TexturedVertex3D
            {
                Position = new Vector3(screenLineRight.EndPoint.X, screenLineRight.EndPoint.Y, 0),
                TexturePosition = new Vector2(texRect.Left, texRect.Centre.Y),
                Colour = colourAt(index + 1)
            });
            quadBatch.Add(new TexturedVertex3D
            {
                Position = new Vector3(screenLineRight.StartPoint.X, screenLineRight.StartPoint.Y, 0),
                TexturePosition = new Vector2(texRect.Left, texRect.Centre.Y),
                Colour = colourAt(index)
            });

            // Each "quad" of the slider is actually rendered as 2 quads, being split in half along the approximating line.
            // On this line the depth is 1 instead of 0, which is done properly handle self-overlap using the depth buffer.
            // Thus the middle vertices need to be added twice (once for each quad).
            Vector3 firstMiddlePoint = new Vector3(screenLine.StartPoint.X, screenLine.StartPoint.Y, 1);
            Vector3 secondMiddlePoint = new Vector3(screenLine.EndPoint.X, screenLine.EndPoint.Y, 1);
            Color4 firstMiddleColour = colourAt(index);
            Color4 secondMiddleColour = colourAt(index + 1);

            for (int i = 0; i < 2; ++i)
            {
                quadBatch.Add(new TexturedVertex3D
                {
                    Position = firstMiddlePoint,
                    TexturePosition = new Vector2(texRect.Right, texRect.Centre.Y),
                    Colour = firstMiddleColour
                });
                quadBatch.Add(new TexturedVertex3D
                {
                    Position = secondMiddlePoint,
                    TexturePosition = new Vector2(texRect.Right, texRect.Centre.Y),
                    Colour = secondMiddleColour
                });
            }

            quadBatch.Add(new TexturedVertex3D
            {
                Position = new Vector3(screenLineLeft.EndPoint.X, screenLineLeft.EndPoint.Y, 0),
                TexturePosition = new Vector2(texRect.Left, texRect.Centre.Y),
                Colour = colourAt(index + 1)
            });
            quadBatch.Add(new TexturedVertex3D
            {
                Position = new Vector3(screenLineLeft.StartPoint.X, screenLineLeft.StartPoint.Y, 0),
                TexturePosition = new Vector2(texRect.Left, texRect.Centre.Y),
                Colour = colourAt(index)
            });
        }

        private void updateVertexBuffer()
        {
            // Offset by 0.5 pixels inwards to ensure we never sample texels outside the bounds
            RectangleF texRect = texture.GetTextureRect(new RectangleF(0.5f, 0.5f, texture.Width - 1, texture.Height - 1));
            var j = 0;
            foreach (Line segment in segments)
                addLineQuads(segment, texRect, j++);
        }

        public override void Draw(IRenderer renderer)
        {
            base.Draw(renderer);

            if (texture?.Available != true || segments.Count == 0)
                return;

            renderer.PushDepthInfo(DepthInfo.Default);

            // Blending is removed to allow for correct blending between the wedges of the path.
            renderer.SetBlend(BlendingParameters.None);

            pathShader.Bind();

            texture.Bind();

            updateVertexBuffer();

            pathShader.Unbind();

            renderer.PopDepthInfo();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            halfCircleBatch.Dispose();
            quadBatch.Dispose();
        }
    }
}