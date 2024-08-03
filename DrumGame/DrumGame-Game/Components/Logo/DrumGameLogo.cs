using System;
using System.Runtime.InteropServices;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shaders.Types;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;

namespace DrumGame.Game.Components.Logo;

public class DrumGameLogo : Sprite
{

    public Texture Texture2;
    public DrumGameLogo()
    {
        // make sure textures are same size
        Texture = Util.Resources.GetAssetTextureNoAtlas("logo-mesh.png");
        Texture2 = Util.Resources.GetAssetTextureNoAtlas("logo-shadow.png");
    }

    DrumShaderManager.ShaderWatcher ShaderWatcher;

    [BackgroundDependencyLoader]
    private void load(DrumShaderManager shaders)
    {
        ShaderWatcher = shaders.LoadHotWatch("sh_Shaders/sh_Logo.fs", e =>
        {
            TextureShader = e;
            Invalidate(Invalidation.DrawInfo);
        });
    }

    protected override void Dispose(bool isDisposing)
    {
        ShaderWatcher?.Dispose();
        base.Dispose(isDisposing);
    }

    protected override void Update()
    {
        base.Update();
        Invalidate(Invalidation.DrawInfo);
    }

    protected override DrawNode CreateDrawNode() => new LogoDrawNode(this);



    public class LogoDrawNode : TexturedShaderDrawNode
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private record struct Parameters
        {
            public UniformFloat Time;
            private readonly UniformPadding12 Padding;
        }
        private IUniformBuffer<Parameters> parameters;

        protected Texture Texture { get; private set; }
        protected Texture Texture2 { get; private set; }
        protected Quad ScreenSpaceDrawQuad { get; private set; }

        protected RectangleF DrawRectangle { get; private set; }
        protected RectangleF TextureCoords { get; private set; }
        float Time;

        protected new DrumGameLogo Source => (DrumGameLogo)base.Source;

        protected Quad ConservativeScreenSpaceDrawQuad;

        public LogoDrawNode(DrumGameLogo source)
            : base(source)
        {
        }

        protected override void BindUniformResources(IShader shader, IRenderer renderer)
        {
            base.BindUniformResources(shader, renderer);

            parameters ??= renderer.CreateUniformBuffer<Parameters>();
            parameters.Data = new() { Time = Time };
            shader.BindUniformBlock("m_Parameters", parameters);
        }

        public override void ApplyState()
        {
            base.ApplyState();
            Time = (float)Source.Clock.CurrentTime;
            Texture = Source.Texture;
            Texture2 = Source.Texture2;
            ScreenSpaceDrawQuad = Source.ScreenSpaceDrawQuad;
            DrawRectangle = Source.DrawRectangle;

            TextureCoords = Source.DrawRectangle.RelativeIn(Source.DrawTextureRectangle);
            if (Texture != null)
                TextureCoords *= new Vector2(Texture.DisplayWidth, Texture.DisplayHeight);
        }

        protected virtual void Blit(IRenderer renderer)
        {
            if (DrawRectangle.Width == 0 || DrawRectangle.Height == 0)
                return;

            if (!renderer.BindTexture(Texture2, 1))
                return;

            renderer.DrawQuad(Texture, ScreenSpaceDrawQuad, DrawColourInfo.Colour, null, null, null, null, TextureCoords);
        }
        protected override void Draw(IRenderer renderer)
        {
            base.Draw(renderer);

            if (Texture?.Available != true)
                return;

            BindTextureShader(renderer);

            Blit(renderer);

            UnbindTextureShader(renderer);
        }
    }
}