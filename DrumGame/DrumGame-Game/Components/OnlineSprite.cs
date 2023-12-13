using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;

namespace DrumGame.Game.Components;

[LongRunningLoad]
public class OnlineSprite : Sprite
{
    private readonly string url;
    public OnlineSprite(string url)
    {
        this.url = url;
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        Texture = textures.Get(url);
    }
}