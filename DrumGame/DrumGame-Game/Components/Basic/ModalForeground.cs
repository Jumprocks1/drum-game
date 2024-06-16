using System.Diagnostics.Contracts;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Components.Basic;

public class ModalForeground : Container
{
    protected override bool OnMouseDown(MouseDownEvent e) => true;
    protected override Container<Drawable> Content { get; }
    public ModalForeground(Axes autoSizeAxes)
    {
        AutoSizeAxes = autoSizeAxes;
        InternalChildren = [
            new Box
            {
                Colour = DrumColors.DarkBorder,
                RelativeSizeAxes = Axes.Both
            },
            Content = new Container {
                RelativeSizeAxes = Axes.Both & ~autoSizeAxes,
                AutoSizeAxes = autoSizeAxes,
                Padding = new MarginPadding(2),
                Child = new Box {
                    Colour = DrumColors.DarkBackground,
                    RelativeSizeAxes = Axes.Both
                }
            },
        ];
    }
}