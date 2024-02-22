using System;
using DrumGame.Game.Components;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Beatmaps.Display;

public class SongInfoPanel : AdjustableSkinElement
{
    public const float DefaultHeight = 60;
    public override AdjustableSkinData DefaultData() => new()
    {
        Anchor = Anchor.BottomLeft,
        Y = -BeatmapTimeline.Height - MusicNotationBeatmapDisplay.ModeTextHeight
    };

    Beatmap Beatmap;

    public override ref AdjustableSkinData SkinPath => ref Util.Skin.Notation.SongInfoPanel;

    // after this is all set up for both displays, we should accept a DisplayPreference argument
    // based on this, we pull the skin data from the skin
    // if the skin data is null, we load the appropriate DefaultSkinData
    // make sure when we update this data is goes to the correct spot in the skin, not sure how to do that yet
    public SongInfoPanel(Beatmap beatmap)
    {
        Beatmap = beatmap;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        Height = DefaultHeight;
        var x = 0f;
        var imagePath = Beatmap.FullAssetPath(Beatmap.Image);
        if (imagePath != null)
        {
            // not sure if this is perfect, oh well
            // it seems like the carousel texture is disposed at this point, so this is a cache miss
            var texture = Util.Resources.LargeTextures.Get(imagePath);
            if (texture != null)
            {
                if (texture.Width != texture.Height)
                {
                    if (texture.Width > texture.Height)
                    {
                        var trim = (texture.Width - texture.Height) / 2;
                        texture = texture.Crop(new osu.Framework.Graphics.Primitives.RectangleF(trim, 0, texture.Width - trim * 2, texture.Height));
                    }
                }
            }
            Sprite sprite;
            AddInternal(sprite = new Sprite
            {
                Width = Height,
                Height = Height,
                Texture = texture
            });
            x += sprite.Width;
        }
        x += 5;

        // formatting here loosely based off of BeatmapCard
        // height breakdown:
        // Title: 25, Space: 1, Artist 18 - Total: 44
        // we auto scale in case we eventually allow users to change our height
        var scale = Height / 46;
        var title = new SpriteText
        {
            Text = Beatmap.Title,
            Font = FrameworkFont.Regular.With(size: 25 * scale),
            X = x
        };
        AddInternal(title);
        const float artistIndent = 8f;
        var artist = new SpriteText
        {
            Text = Beatmap.Artist,
            Font = FrameworkFont.Regular.With(size: 18 * scale),
            X = x + artistIndent,
            Y = 26 * scale
        };
        AddInternal(artist);
        x += Math.Max(title.Width, artist.Width + artistIndent) + 5;
        // TODO may need some sort of max width
        Width = Math.Max(x, 200);
    }
}