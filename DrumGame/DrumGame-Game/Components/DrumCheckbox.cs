using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;

namespace DrumGame.Game.Components;

public class DrumCheckbox : Checkbox, IHasHandCursor
{
    protected override bool OnMouseDown(MouseDownEvent e) => true;
    public LocalisableString LabelText
    {
        get => labelSpriteText.Text;
        set => labelSpriteText.Text = value;
    }

    SpriteText labelSpriteText;
    new float Size;
    public DrumCheckbox(float size = 30)
    {
        Size = size;
        labelSpriteText = new SpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Depth = float.MinValue,
            Font = FrameworkFont.Condensed,
        };
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        Drawable box;

        var boxPad = 3;

        AutoSizeAxes = Axes.Both;

        Add(new Container
        {
            Size = new Vector2(Size),
            Children = new Drawable[] {
                new Box {
                    Size = new Vector2(Size),
                    Colour = DrumColors.CheckboxBackground
                },
                box = new SpriteIcon {
                    Icon = FontAwesome.Solid.Check,
                    Size = new Vector2(Size - boxPad * 2),
                    X = boxPad,
                    Y = boxPad
                }
            }
        });
        labelSpriteText.X = Size + 10;
        Add(labelSpriteText);

        Current.BindValueChanged(e => box.Alpha = e.NewValue ? 1 : 0, true);
    }
}