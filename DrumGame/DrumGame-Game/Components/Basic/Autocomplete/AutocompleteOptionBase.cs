using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;

namespace DrumGame.Game.Components.Basic.Autocomplete;

public class AutocompleteOptionBase : BasicButton
{
    public new const float Margin = 8;
    public new const float Height = 25;
    public AutocompleteOptionBase()
    {
        RelativeSizeAxes = Axes.X;
        base.Height = Height;
        BackgroundColour = Colour4.Transparent;
    }
    protected override SpriteText CreateText()
    {
        return new SpriteText
        {
            Depth = -1,
            Padding = new MarginPadding { Left = Margin },
            Origin = Anchor.CentreLeft,
            Anchor = Anchor.CentreLeft,
            Font = FrameworkFont.Regular
        };
    }
}