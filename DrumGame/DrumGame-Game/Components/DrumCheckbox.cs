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

public class DrumCheckbox : Checkbox
{
    protected override bool OnMouseDown(MouseDownEvent e) => true;
    public LocalisableString LabelText
    {
        get => labelSpriteText.Text;
        set => labelSpriteText.Text = value;
    }

    SpriteText labelSpriteText;
    public DrumCheckbox()
    {
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

        var size = 30;
        var boxPad = 3;

        AutoSizeAxes = Axes.Both;

        Add(new Container
        {
            Size = new Vector2(size),
            Children = new Drawable[] {
                new Box {
                    Size = new Vector2(size),
                    Colour = DrumColors.CheckboxBackground
                },
                box = new SpriteIcon {
                    Icon = FontAwesome.Solid.Check,
                    Size = new Vector2(size - boxPad * 2),
                    X = boxPad,
                    Y = boxPad
                }
            }
        });
        labelSpriteText.X = size + 10;
        Add(labelSpriteText);

        Current.BindValueChanged(e => box.Alpha = e.NewValue ? 1 : 0, true);
    }
}