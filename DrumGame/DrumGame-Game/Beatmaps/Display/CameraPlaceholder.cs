using DrumGame.Game.Containers;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Skinning;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;

namespace DrumGame.Game.Beatmaps.Display;

public class CameraPlaceholder : CompositeDrawable, IHasContextMenu, IHasMarkupTooltip
{
    Box Background;
    public CameraPlaceholder()
    {
        AddInternal(Background = new Box
        {
            Colour = Util.Skin.Notation.InputDisplayBackground * 0.98f + Util.Skin.Notation.NotationColor * 0.02f,
            RelativeSizeAxes = Axes.Both,
        });
        AddInternal(new SpriteText
        {
            Colour = Util.Skin.Notation.NotationColor,
            Alpha = 0.2f,
            Text = "Place OBS camera here",
            Font = FrameworkFont.Regular.With(size: 40),
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre
        });
        SkinManager.RegisterTarget(SkinAnchorTarget.CameraPlaceholder, this);
    }

    protected override void Dispose(bool isDisposing)
    {
        SkinManager.UnregisterTarget(this);
        base.Dispose(isDisposing);
    }

    public static double AspectRatio => Util.Skin.StreamingCameraAspectRatio;

    public MenuItem[] ContextMenuItems => ContextMenuBuilder.New(this)
        .Add("Set aspect ratio", _ => Util.Palette.RequestNumber("Aspect Ratio", "Ratio", AspectRatio, v =>
        {
            SkinPathUtil.SetAndDirty(e => e.StreamingCameraAspectRatio, v);
            Util.GetParent<BeatmapAuxDisplay>(this)?.InvalidateLayout();
        }))
        .Build();

    public string MarkupTooltip => "Right click to set aspect ratio";
}