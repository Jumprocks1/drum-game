using DrumGame.Game.Components;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;

namespace DrumGame.Game.Skinning;

public class AnimatedSprite : Sprite
{
    public Texture[] Textures;
    public double FrameLength;
    public AnimatedSprite(Texture[] textures, double frameLength)
    {
        FrameLength = frameLength;
        Textures = textures;
    }
    protected override void Update()
    {
        if (Textures == null || Textures.Length == 0 || FrameLength == 0) return;
        var frame = ((int)(Util.DrumGame.Clock.CurrentTime / FrameLength)) % Textures.Length;
        Texture = Textures[frame];
        base.Update();
    }
}

public class SkinTexture
{
    public TextureFilteringMode FilteringMode = TextureFilteringMode.Nearest;
    public float AspectRatio;
    public string File;

    public WrapMode WrapModeS; // horizontal
    public WrapMode WrapModeT; // vertical
    public RectangleF Crop;
    public float AnimateX;
    public float AnimateY;
    public int FrameCount;
    public double FrameDuration;

    bool Animated => !(FrameCount == 0 || FrameDuration == 0);

    // Texture _texture;
    Texture Texture => _texture ??= _getTexture(); // this will cache, but might be best that we don't rely on that
    public string Blend;
    public FillMode Fill = FillMode.Fill;
    public float Alpha = 1;
    public float ScaleX = 1;
    public float ScaleY = 1;
    public Colour4 Color;


    void ApplyBasic(Drawable drawable)
    {
        drawable.Blending = Blending;
        drawable.Colour = Color;
        drawable.RelativeSizeAxes = Axes.Both;
        drawable.Width = ScaleX;
        drawable.Height = ScaleY;
        drawable.FillMode = Fill;
        drawable.FillAspectRatio = AspectRatio;
        drawable.Anchor = Anchor.Centre;
        drawable.Origin = Anchor.Centre;
    }
    Texture[] AnimatedTextures;
    public Sprite MakeSprite()
    {
        Sprite sprite;
        PrepareForDraw();
        if (Animated)
            sprite = new AnimatedSprite(AnimatedTextures, FrameDuration);
        else
            sprite = new Sprite { Texture = Texture };
        ApplyBasic(sprite);
        return sprite;
    }
    [JsonIgnore]
    public SkinTexture Prepared { get { PrepareForDraw(); return this; } }
    public string FragmentShader;
    IShader LoadedShader;
    bool Ready;
    BlendingParameters Blending;
    public void PrepareForDraw() // sometimes called on draw thread, sometimes on update thread
    {
        if (Ready) return;

        if (Animated)
        {
            if (AnimatedTextures == null)
            {
                var t = Texture;
                if (t != null)
                {
                    AnimatedTextures = new Texture[FrameCount];
                    var area = Crop.Area;
                    var rel = area <= 1 ? Axes.Both : Axes.None;
                    if (area > 0)
                    {
                        for (var i = 0; i < FrameCount; i++)
                            AnimatedTextures[i] = t.Crop(Crop.Offset(AnimateX * i, AnimateY * i), rel);
                    }
                }
            }
            if (AspectRatio == default)
                AspectRatio = Crop.Width * ScaleX / (Crop.Height * ScaleY);
        }
        else
        {
            _texture ??= _getTexture();
            if (Crop.Area > 0)
                AspectRatio = Crop.Width * ScaleX / (Crop.Height * ScaleY);
            else if (Texture != null && Texture.Height > 0)
                AspectRatio = Texture.Width * ScaleX / (Texture.Height * ScaleY);
        }
        if (Color == default) Color = new Colour4(1, 1, 1, Alpha);
        else if (Alpha != 1) Color = Color.MultiplyAlpha(Alpha);
        Ready = true;
        if (Blend == "additive")
            Blending = BlendingParameters.Additive;
        else
            Blending = BlendingParameters.Mixture;
        if (!string.IsNullOrEmpty(FragmentShader))
        {
            try
            {
                LoadedShader = Util.Skin.ShaderManager.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShader);
            }
            catch { LoadedShader = null; }
        }
    }
    Texture _texture;
    Texture _getTexture()
    {
        if (string.IsNullOrWhiteSpace(File)) return GLUtil.Renderer.WhitePixel;
        // we can have this rotate based on the current time
        // note that in DTXManiaNX, they use 70ms per frame
        // pretty bad
        var t = Util.Resources.GetAssetTexture(File, FilteringMode, WrapModeS, WrapModeT);
        if (t == null) return null;
        if (Animated) return t;
        var area = Crop.Area;
        if (area > 0)
            t = t.Crop(Crop, area <= 1 ? Axes.Both : Axes.None);
        return t;
    }

    // WARNING: this sets the blend mode. Make sure to reset it before drawing again
    // We also bind to shader
    public void DrawCentered(CanvasNode node, float x, float y, float w, float h) => Draw(node, x, y, w, h, true);
    public void Draw(CanvasNode node, float x, float y, float w, float h, bool center = false)
    {
        PrepareForDraw();
        node.DesiredShader = LoadedShader ?? node.TextureShader;
        node.Color = Color;
        w *= ScaleX;
        h *= ScaleY;
        var texture = Texture;
        if (AnimatedTextures != null && AnimatedTextures.Length > 0 && FrameDuration > 0)
        {
            var frame = ((int)(node.Time / FrameDuration)) % AnimatedTextures.Length;
            texture = AnimatedTextures[frame];
        }
        if (texture != null)
        {
            if (Fill == FillMode.Fill)
            {
                if (texture.WrapModeT == default && texture.WrapModeS == default)
                {
                    var ratioAdjustment = AspectRatio / w * h / node.RelativeAspectRatio;
                    if (ratioAdjustment < 1) h /= ratioAdjustment;
                    else w *= ratioAdjustment;
                }
            }
            else if (Fill == FillMode.Fit)
            {
                var ratioAdjustment = AspectRatio / w * h / node.RelativeAspectRatio;
                if (ratioAdjustment < 1) w *= ratioAdjustment;
                else h /= ratioAdjustment;
            }
            if (Blending != default)
                node.SetBlend(Blending);
            if (center)
                node.Sprite(texture, x - w / 2, y - h / 2, w, h);
            else
                node.Sprite(texture, x, y, w, h);
        }
    }
}