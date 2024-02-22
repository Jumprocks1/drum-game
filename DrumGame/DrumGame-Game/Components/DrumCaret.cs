using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace DrumGame.Game.Components;

public class DrumCaret : Caret
{
    public DrumCaret()
    {
        Colour = Color4.Transparent;

        InternalChild = new Container
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            RelativeSizeAxes = Axes.Both,
            Height = 0.9f,
            CornerRadius = 1f,
            Masking = true,
            Child = new Box { RelativeSizeAxes = Axes.Both },
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
