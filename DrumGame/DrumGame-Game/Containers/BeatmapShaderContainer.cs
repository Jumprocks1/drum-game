
using osuTK;
using osuTK.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using System.Collections.Generic;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.IO.Stores;
using System;
using osu.Framework.Logging;
using System.Runtime.InteropServices;
using osu.Framework.Graphics.Shaders.Types;
using osu.Framework.Graphics.Rendering;
using DrumGame.Game.Beatmaps;

namespace DrumGame.Game.Containers;

// Mostly copied from BufferedContainer.cs
public class BeatmapShaderContainer : Container, IBufferedDrawable
{
    public Color4 BackgroundColour { get; } = new Color4(0, 0, 0, 0);

    public Vector2 FrameBufferScale => Vector2.One; // this can reduce flickering probably

    public IShader TextureShader { get; private set; }

    private readonly BufferedDrawNodeSharedData sharedData;

    BeatmapPlayer Player;

    public readonly string FragmentShader;
    FileWatcher Watcher;
    ShaderManager ShaderManager;
    public BeatmapShaderContainer(BeatmapPlayer player, string fragmentShader, bool watchShader = true)
    {
        Player = player;
        AddInternal(player);
        if (watchShader)
        {
            var fullPath = Util.Resources.GetAbsolutePath(fragmentShader);
            Watcher = new FileWatcher(fullPath);
            Watcher.Register();
            Watcher.Changed += () => Schedule(ReloadShader);
        }
        FragmentShader = fragmentShader;
        sharedData = new BufferedDrawNodeSharedData(null, false, true);
    }

    void ReloadShader()
    {
        var shaderStore = new ResourceStore<byte[]>();
        shaderStore.AddStore(new NamespacedResourceStore<byte[]>(Util.DrumGame.Resources, @"Shaders"));
        shaderStore.AddStore(Util.Resources);
        var newShaderManager = new ShaderManager(Util.Host.Renderer, shaderStore);
        try
        {
            var shader = newShaderManager.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShader);
            TextureShader = shader;
            ShaderManager?.Dispose();
            ShaderManager = newShaderManager;
        }
        catch (Exception e)
        {
            newShaderManager?.Dispose();
            Util.Palette.ShowMessage("Shader compilation failed, see console");
            Logger.Error(e, "Shader compilation failed");
        }
    }

    [BackgroundDependencyLoader]
    private void load(DrumShaderManager shaders)
    {
        try
        {
            TextureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShader);
        }
        catch (Exception e)
        {
            Util.Palette.ShowMessage("Shader compilation failed, see console");
            Logger.Error(e, "Shader compilation failed");
            TextureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
        }
    }

    protected override DrawNode CreateDrawNode() => new ShaderContainerDrawNode(this, sharedData);

    public DrawColourInfo? FrameBufferDrawColour => null;

    protected override void Update()
    {
        base.Update();
        Invalidate(Invalidation.DrawNode);
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);
        Watcher?.Dispose();
        Watcher = null;
        ShaderManager?.Dispose();
        ShaderManager = null;
        sharedData.Dispose();
    }

    private class ShaderContainerDrawNode : BufferedDrawNode, ICompositeDrawNode
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private record struct Parameters
        {
            public UniformFloat AspectRatio;
            public UniformFloat Time;
            public UniformFloat TrackTime;
            public UniformFloat TrackBeat;
            // private readonly UniformPadding4 Padding;
        }
        private IUniformBuffer<Parameters> parameters;

        new BeatmapShaderContainer Source => (BeatmapShaderContainer)base.Source;

        float Time;
        float TrackTime;
        float TrackBeat;

        public override void ApplyState()
        {
            base.ApplyState();
            var player = Source.Player;

            Time = (float)Source.Clock.CurrentTime;
            TrackTime = (float)player.Track.CurrentTime;
            TrackBeat = (float)player.Track.CurrentBeat;
        }

        protected override void BindUniformResources(IShader shader, IRenderer renderer)
        {
            base.BindUniformResources(shader, renderer);

            parameters ??= renderer.CreateUniformBuffer<Parameters>();
            parameters.Data = new()
            {
                Time = Time,
                TrackTime = TrackTime,
                TrackBeat = TrackBeat,
                AspectRatio = DrawRectangle.Width / DrawRectangle.Height
            };

            shader.BindUniformBlock("m_Parameters", parameters);
        }
        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            parameters?.Dispose();
        }
        protected new CompositeDrawableDrawNode Child => (CompositeDrawableDrawNode)base.Child;

        public ShaderContainerDrawNode(BeatmapShaderContainer source, BufferedDrawNodeSharedData sharedData)
            : base(source, new CompositeDrawableDrawNode(source), sharedData)
        {
        }
        public List<DrawNode> Children
        {
            get => Child.Children;
            set => Child.Children = value;
        }
        public bool AddChildDrawNodes => RequiresRedraw;
    }
}
