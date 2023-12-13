using System;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Modals;

public class ModalBackground : Box
{
    readonly Action Action;
    public ModalBackground(Action action)
    {
        Action = action;
        Colour = DrumColors.ModalBackground;
        RelativeSizeAxes = Axes.Both;
    }
    protected override bool OnMouseDown(MouseDownEvent e)
    {
        Action();
        return true;
    }

    protected override bool Handle(UIEvent e)
    {
        if (base.Handle(e)) return true;
        switch (e)
        {
            case ScrollEvent:
            case MouseEvent:
                return true;
        }
        return false;
    }
}