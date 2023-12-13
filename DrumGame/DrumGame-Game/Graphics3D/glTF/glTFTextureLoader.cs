using System.Collections.Generic;
using DrumGame.Game.Utils;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osuTK.Graphics.ES30;
using SixLabors.ImageSharp.PixelFormats;

namespace DrumGame.Game.Graphics3D.glTF;

// we have to extend TextureLoaderStore since that's the only way to access LoadFromStream
public class glTFTextureLoader : TextureLoaderStore
{
    // sadly we need this to prevent Dispose from failing
    readonly static IResourceStore<byte[]> dummyStore = new ResourceStore<byte[]>();
    glTFScene scene;
    public glTFTextureLoader(glTFScene scene) : base(dummyStore)
    {
        this.scene = scene;
    }
    TextureUpload GetUpload(int bufferViewId)
    {
        try
        {
            using (var stream = scene.GetStream(bufferViewId))
            {
                if (stream != null) return new TextureUpload(ImageFromStream<Rgba32>(stream));
            }
        }
        catch { }
        return null;
    }

    Dictionary<(int?, int?), Texture> Textures = new();

    Texture LoadTexture(glTFLoader.Schema.Texture tex)
    {
        if (!tex.Source.HasValue) return GLUtil.Renderer.WhitePixel;


        var filteringMode = TextureFilteringMode.Linear;

        var samplerId = tex.Sampler;
        if (samplerId.HasValue)
        {
            // could set this up, later
            // would need to set filteringMode and some sort of mipmap mode
            var sampler = scene.model.Samplers[samplerId.Value];
        }

        var source = scene.model.Images[tex.Source.Value];
        var bufferViewId = source.BufferView.Value;


        var upload = GetUpload(bufferViewId); // upload is automatically disposed internally
        var texture = new DisposableTexture(GLUtil.Renderer.CreateTexture(upload.Width, upload.Height, filteringMode: filteringMode));
        texture.SetData(upload);
        return texture;
    }
    public Texture GetTexture(glTFLoader.Schema.TextureInfo textureInfo)
    {
        var textureId = textureInfo.Index;
        var tex = scene.model.Textures[textureId];
        var key = (tex.Source, tex.Sampler);
        if (Textures.TryGetValue(key, out var o)) return o;
        var v = LoadTexture(tex);
        Textures[key] = v;
        return v;
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var texture in Textures.Values)
        {
            if (texture is DisposableTexture d) d.Dispose();
        }
        Textures = null;
        base.Dispose(disposing);
    }
}
