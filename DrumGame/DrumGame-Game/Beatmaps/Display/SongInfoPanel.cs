using System;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Beatmaps.Display;

public class SongInfoPanel : AdjustableSkinElement, IHasCommandInfo
{
    public const float DefaultHeight = 90; // should be at least as big a BeatmapCard
    public override AdjustableSkinData DefaultData() => new()
    {
        Anchor = Anchor.BottomLeft,
        Y = -BeatmapTimeline.Height - MusicNotationBeatmapDisplay.ModeTextHeight
    };

    Beatmap Beatmap;

    public override ref AdjustableSkinData SkinPath => ref Util.Skin.Notation.SongInfoPanel;

    public CommandInfo CommandInfo => Util.GetParent<BeatmapEditor>(this) != null ? Util.CommandController[Command.EditBeatmapMetadata] : null;

    // after this is all set up for both displays, we should accept a DisplayPreference argument
    // based on this, we pull the skin data from the skin
    // if the skin data is null, we load the appropriate DefaultSkinData
    // make sure when we update this data is goes to the correct spot in the skin, not sure how to do that yet
    public SongInfoPanel(Beatmap beatmap)
    {
        Beatmap = beatmap;
        AddInternal(Title = new());
        AddInternal(Artist = new());
        AddInternal(Image = new());
    }

    SpriteText Title;
    SpriteText Artist;
    Sprite Image;

    public void UpdateData()
    {
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
            Image.Width = Height;
            Image.Height = Height;
            Image.Texture = texture;
            Image.Alpha = 1;
            x += Image.Width;
        }
        else Image.Alpha = 0;
        x += 5;

        // formatting here loosely based off of BeatmapCard
        // height breakdown:
        // Title: 25, Space: 1, Artist 18 - Total: 44
        // we auto scale in case we eventually allow users to change our height
        // we don't divide by 44 since we want the text to be a bit smaller (so it's not so wide)
        // eventually we should add auto scaling text which crunches really long text
        var yScale = Height / 75;
        Title.Text = Beatmap.Title;
        Title.Font = FrameworkFont.Regular.With(size: 25 * yScale);
        Title.X = x;
        const float artistIndent = 8f;
        Artist.Text = Beatmap.Artist;
        Artist.Font = FrameworkFont.Regular.With(size: 18 * yScale);
        Artist.X = x + artistIndent;
        Artist.Y = 26 * yScale;
        x += Math.Max(Title.Width, Artist.Width + artistIndent) + 5;
        // TODO may need some sort of max width
        Width = Math.Max(x, 200);
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        Height = DefaultHeight;
        UpdateData();
    }
}