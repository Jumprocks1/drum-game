using System;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Skinning;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Beatmaps.Display;

public class SongInfoPanel : AdjustableSkinElement, IHasCommandInfo
{
    public override ElementLayout[] AvailableLayouts => [ElementLayout.Horizontal, ElementLayout.Vertical];
    public const float DefaultImageHeight = 90; // should be at least as big a BeatmapCard
    public const float DefaultHeight = DefaultImageHeight; // only works for default layout
    public override AdjustableSkinData DefaultData() => Mania ? new()
    {
        Anchor = Anchor.CentreRight,
        Origin = Anchor.CentreLeft,
        AnchorTarget = SkinAnchorTarget.PositionIndicator,
        Layout = ElementLayout.Vertical
    } : new()
    {
        Origin = Anchor.BottomLeft,
        Anchor = Anchor.TopLeft,
        AnchorTarget = SkinAnchorTarget.ModeText
    };

    Beatmap Beatmap;

    public override ref AdjustableSkinData SkinPath
    {
        get
        {
            if (Mania) return ref Util.Skin.Mania.SongInfoPanel;
            return ref Util.Skin.Notation.SongInfoPanel;
        }
    }

    bool Mania;

    public CommandInfo CommandInfo => Util.GetParent<BeatmapEditor>(this) != null ? Util.CommandController[Command.EditBeatmapMetadata] : null;

    // after this is all set up for both displays, we should accept a DisplayPreference argument
    // based on this, we pull the skin data from the skin
    // if the skin data is null, we load the appropriate DefaultSkinData
    // make sure when we update this data is goes to the correct spot in the skin, not sure how to do that yet
    public SongInfoPanel(Beatmap beatmap, bool mania = false) : base(skipInit: true)
    {
        Mania = mania;
        InitializeSkinData();
        Beatmap = beatmap;
        AddInternal(Title = new() { Colour = Util.Skin.Notation.NotationColor });
        AddInternal(Artist = new() { Colour = Util.Skin.Notation.NotationColor });
        AddInternal(Image = new());
        SkinManager.RegisterTarget(SkinAnchorTarget.SongInfoPanel, this);
    }

    SpriteText Title;
    SpriteText Artist;
    Sprite Image;

    public override void LayoutChanged() => UpdateData();

    protected override void Dispose(bool isDisposing)
    {
        SkinManager.UnregisterTarget(SkinAnchorTarget.SongInfoPanel);
        base.Dispose(isDisposing);
    }

    public void UpdateData()
    {
        var x = 0f;
        var y = 0f;
        var imagePath = Beatmap.FullAssetPath(Beatmap.Image);

        // when in vertical, total height is:
        //    imageHeight + 44 * yUnit = imageHeight + 44 * imageHeight / 75f = imageHeight * 119f / 75
        // therefore, imageHeight should be totalHeight * 75f / 119f

        var height = SkinData.Height == default ? (
            Layout == ElementLayout.Vertical ? DefaultImageHeight * 119f / 75 : DefaultImageHeight
        ) : SkinData.Height;

        var imageHeight = Layout == ElementLayout.Vertical ? height * 75f / 119 : height;

        var yUnit = imageHeight / 75f;
        var titleHeight = 25 * yUnit;
        var artistHeight = 18 * yUnit;

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
            Image.Width = imageHeight;
            Image.Height = imageHeight;
            if (Layout == ElementLayout.Vertical)
                y += Image.Width;
            else
                x += Image.Width;
            Image.Texture = texture;
            Image.Alpha = 1;
        }
        else Image.Alpha = 0;
        x += 5;

        // formatting here loosely based off of BeatmapCard
        // height breakdown:
        // Title: 25, Space: 1, Artist 18 - Total: 44
        // we auto scale in case we eventually allow users to change our height
        // we don't divide by 44 since we want the text to be a bit smaller (so it's not so wide)
        // eventually we should add auto scaling text which crunches really long text
        Title.Text = Beatmap.Title;
        Title.Font = FrameworkFont.Regular.With(size: titleHeight);
        Title.X = x;
        Title.Y = y;
        y += titleHeight + yUnit;

        var artistIndent = Layout == ElementLayout.Vertical ? 0f : 8f;
        Artist.Text = Beatmap.Artist;
        Artist.Font = FrameworkFont.Regular.With(size: artistHeight);
        Artist.X = x + artistIndent;
        Artist.Y = y;
        y += artistHeight;

        x += Math.Max(Title.Width, Artist.Width + artistIndent) + 5;
        if (SkinData.Width != default) Width = SkinData.Width;
        else Width = Math.Max(x, 200);
        Height = Math.Max(imageHeight, y);
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        UpdateData();
    }
}