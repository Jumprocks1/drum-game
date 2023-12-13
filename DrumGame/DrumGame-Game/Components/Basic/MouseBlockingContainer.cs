using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Components.Basic;

// mostly used for modals/popups to prevent clicks and hovers from passing through the background
public class MouseBlockingContainer : Container
{
    protected override bool Handle(UIEvent e)
    {
        if (base.Handle(e)) return true;
        return e switch
        {
            MouseEvent => true,
            _ => false,
        };
    }
}
