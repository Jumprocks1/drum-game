using osu.Framework.Allocation;
using System;
using System.Collections.Generic;
using System.IO;
using osu.Framework.Configuration;
using osu.Framework.Graphics.Animations;
using osu.Framework.Platform;
using osuTK;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Video;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Timing;
using osu.Framework.Bindables;
using System.Threading;
using DrumGame.Game.Utils;
using DrumGame.Game.Components;
using osu.Framework.Graphics.Rendering;

namespace DrumGame.Game.Media;
public class SynchronousVideoPlayer : CustomisableSizeCompositeDrawable
{
    public VideoDecoder.DecoderState State => decoder?.State ?? VideoDecoder.DecoderState.Ready;
    private VideoDecoder decoder;
    private readonly Queue<DecodedFrame> availableFrames = new Queue<DecodedFrame>();
    private readonly Stream stream;
    public event Action SizeLoaded;
    public bool IsSizeLoaded;

    private bool isDisposed;

    internal VideoSprite Sprite;
    public BindableDouble Offset = new BindableDouble(0);
    public Matrix3 ConversionMatrix => decoder.GetConversionMatrix();

    public double FrameRate;

    readonly IClock TargetClock;

    public SynchronousVideoPlayer(SyncedVideo video) : this(video.Track, video.Path)
    {
        Offset.Value = video.Offset.Value;
        Anchor = video.Anchor;
        Origin = video.Origin;
        Size = video.Size;
        RelativeSizeAxes = video.RelativeSizeAxes;
        Position = video.Position;
        RelativePositionAxes = video.RelativePositionAxes;
        FillMode = video.FillMode;
        Depth = video.Depth;
    }
    public SynchronousVideoPlayer(Stream stream, IClock clock)
    {
        TargetClock = clock;
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }
    public SynchronousVideoPlayer(IClock clock, string path) : this(File.OpenRead(path), clock) { }
    [BackgroundDependencyLoader]
    private void load(GameHost gameHost, FrameworkConfigManager config)
    {
        AddInternal(Sprite = new VideoSprite(this) { RelativeSizeAxes = Axes.Both });
        decoder = gameHost.CreateVideoDecoder(stream);
        config.BindWith(FrameworkSetting.HardwareVideoDecoder, decoder.TargetHardwareVideoDecoders);
        decoder.StartDecoding();
        FrameRate = decoder.FrameRate;
    }

    protected override void Update()
    {
        LoadFrame(TargetClock.CurrentTime + Offset.Value);
        base.Update();
    }

    DecodedFrame CurrentFrame;

    public void LoadFrame(double time)
    {
        if (decoder.State == VideoDecoder.DecoderState.EndOfStream && availableFrames.Count == 0)
            return;

        var i = 0;

        if (time - decoder.LastDecodedFrameTime > 5000 / FrameRate)
        {
            Console.WriteLine($"seeking decoder to {time} from {decoder.LastDecodedFrameTime}");
            decoder.Seek(time);
            decoder.ReturnFrames(availableFrames);
            availableFrames.Clear();
        }

        var first = true;
        while (availableFrames.Count == 0)
        {
            if (!first)
            {
                if (Util.IsSingleThreaded)
                {
                    i += 1;
                    if (i > 200) throw new Exception("decoder too slow");
                    Thread.Sleep(10); // wait for available frames
                }
                else return; // give up if we are multi-threaded
            }
            first = false;
            if (availableFrames.Count < 8)
                foreach (var f in decoder.GetDecodedFrames()) availableFrames.Enqueue(f);

            // if we are 2 full frames behind, skip frame
            while (availableFrames.Count > 0 && time - availableFrames.Peek().Time > 2000 / FrameRate)
                availableFrames.Dequeue();

            // if our current frame is good, we can stop for now
            if (availableFrames.Count > 0 && availableFrames.Peek().Time > time && CurrentFrame != null) return;
            if (decoder.State == VideoDecoder.DecoderState.EndOfStream && availableFrames.Count == 0)
                return;
        }

        if (availableFrames.Peek().Time > time && CurrentFrame != null)
            return;

        var nextFrame = availableFrames.Dequeue();
        var tex = nextFrame.Texture;
        Sprite.Texture = tex;
        IsSizeLoaded = true;
        if (SizeLoaded != null) // something is waiting for our size
        {
            if (tex.Size != Vector2.Zero)
            {
                SizeLoaded();
                SizeLoaded = null;
            }
        }
        UpdateSizing();

        if (CurrentFrame != null) decoder.ReturnFrames(new[] { CurrentFrame });
        CurrentFrame = nextFrame;
    }


    protected override void Dispose(bool isDisposing)
    {
        if (isDisposed)
            return;

        base.Dispose(isDisposing);

        isDisposed = true;

        if (decoder != null)
        {
            decoder.ReturnFrames(availableFrames);
            availableFrames.Clear();
            CurrentFrame = null;

            decoder.Dispose();
        }
        else
        {
            foreach (var f in availableFrames)
                f.Texture.Dispose();
        }
    }

    protected override float GetFillAspectRatio() => Sprite.FillAspectRatio;

    protected override Vector2 GetCurrentDisplaySize() =>
        new Vector2(Sprite.Texture?.DisplayWidth ?? 0, Sprite.Texture?.DisplayHeight ?? 0);
}
internal class VideoSprite : Sprite
{
    private readonly SynchronousVideoPlayer video;

    public VideoSprite(SynchronousVideoPlayer video)
    {
        this.video = video;
    }

    [BackgroundDependencyLoader]
    private void load(ShaderManager shaders)
    {
        TextureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.VIDEO);
        RoundedTextureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.VIDEO_ROUNDED);
    }

    protected override DrawNode CreateDrawNode() => new VideoSpriteDrawNode(video);
}
internal class VideoSpriteDrawNode : SpriteDrawNode
{
    private readonly SynchronousVideoPlayer video;

    public VideoSpriteDrawNode(SynchronousVideoPlayer source)
        : base(source.Sprite)
    {
        video = source;
    }

    private int yLoc, uLoc = 1, vLoc = 2;

    public override void Draw(IRenderer renderer)
    {
        var shader = GetAppropriateShader(renderer);

        shader.GetUniform<int>("m_SamplerY").UpdateValue(ref yLoc);
        shader.GetUniform<int>("m_SamplerU").UpdateValue(ref uLoc);
        shader.GetUniform<int>("m_SamplerV").UpdateValue(ref vLoc);

        var yuvCoeff = video.ConversionMatrix;
        shader.GetUniform<Matrix3>("yuvCoeff").UpdateValue(ref yuvCoeff);

        base.Draw(renderer);
    }
}
