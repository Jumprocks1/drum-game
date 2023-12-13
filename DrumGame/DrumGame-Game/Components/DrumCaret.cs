using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace DrumGame.Game.Components;

public class DrumCaret : Caret
{
    public DrumCaret()
    {
        RelativeSizeAxes = Axes.Y;
        Size = new Vector2(1, 0.9f);

        Colour = Color4.Transparent;
        Anchor = Anchor.CentreLeft;
        Origin = Anchor.CentreLeft;

        Masking = true;
        CornerRadius = 1;

        InternalChild = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = Color4.White,
        };
    }

    public override void Hide() => this.FadeOut(200);

    public float CaretWidth { get; set; }

    public Color4 SelectionColour { get; set; }

    public override void DisplayAt(Vector2 position, float? selectionWidth)
    {
        if (selectionWidth != null)
        {
            Position = new Vector2(position.X, position.Y);
            Width = selectionWidth.Value + CaretWidth / 2;
            this
                .FadeTo(0.5f, 200, Easing.Out)
                .FadeColour(SelectionColour, 200, Easing.Out);
        }
        else
        {
            Position = new Vector2(position.X - CaretWidth / 2, position.Y);
            Width = CaretWidth;
            this
                .FadeColour(Color4.White, 200, Easing.Out)
                .Loop(c => c.FadeTo(0.7f).FadeTo(0.4f, 500, Easing.InOutSine));
        }
    }
}
