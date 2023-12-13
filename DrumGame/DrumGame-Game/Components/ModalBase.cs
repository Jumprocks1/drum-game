using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Components;

// could probably add the clickable backdrop to this
public abstract class ModalBase : CompositeDrawable
{
    protected override bool Handle(UIEvent e)
    {
        if (base.Handle(e)) return true;
        return e switch
        {
            ScrollEvent or MouseEvent => true,
            _ => false,
        };
    }
}

