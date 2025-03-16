using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DrumGame.Game.Components;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Skinning;
using DrumGame.Game.Utils;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shaders.Types;

namespace DrumGame.Game.Beatmaps.Display.Mania;

public class BackgroundSkinTexture(Func<SkinTexture> GetSkinTexture, IHasTrack HasTrack) : Canvas<BackgroundSkinTexture.Data>
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public record struct Parameters
    {
        public UniformFloat AspectRatio;
        public UniformPadding12 Padding;
    }
    public class Data
    {
        public IUniformBuffer<Parameters> Parameters;
    }
    protected override void ApplyState(Node node)
    {
        if (HasTrack?.Track != null)
            node.ApplyTrackInfo(HasTrack.Track);
    }
    protected override void BindUniformResources(Node node, IShader shader, IRenderer renderer)
    {
        node.State.Parameters ??= renderer.CreateUniformBuffer<Parameters>();
        node.State.Parameters.Data = new() { AspectRatio = node.Width / node.Height };
        shader.BindUniformBlock("m_Parameters", node.State.Parameters);
    }
    protected override void Draw(Node node) => GetSkinTexture()?.Draw(node, 0, 0, 1, 1);
}
