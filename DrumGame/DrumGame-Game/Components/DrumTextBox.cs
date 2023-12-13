
using DrumGame.Game.Utils;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK.Graphics;

namespace DrumGame.Game.Components;

public class DrumTextBox : TextBox
{
    protected virtual float CaretWidth => 2;

    public Color4 BackgroundFocused = DrumColors.ActiveTextBox;
    public Color4 BackgroundUnfocused = DrumColors.FieldBackground;

    private readonly Box background;

    protected virtual Color4 InputErrorColour => Color4.Red;

    protected override bool OnMouseDown(MouseDownEvent e) => base.OnMouseDown(e) || true;

    public DrumTextBox()
    {
        Add(background = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Depth = 1,
            Colour = BackgroundUnfocused,
        });
        TextContainer.Height = 0.75f;
    }

    protected override void NotifyInputError() => background.FlashColour(InputErrorColour, 200);

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);

        background.ClearTransforms();
        background.Colour = BackgroundFocused;
        background.FadeColour(BackgroundUnfocused, 200, Easing.OutExpo);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);

        background.ClearTransforms();
        background.Colour = BackgroundUnfocused;
        background.FadeColour(BackgroundFocused, 200, Easing.Out);
    }

    protected override SpriteText CreatePlaceholder() => new SpriteText
    {
        Colour = DrumColors.Placeholder,
        Font = FrameworkFont.Condensed,
        Anchor = Anchor.CentreLeft,
        Origin = Anchor.CentreLeft,
        X = CaretWidth,
    };
    protected override Caret CreateCaret() => new DrumCaret
    {
        CaretWidth = CaretWidth,
        SelectionColour = DrumColors.Selection,
    };
}