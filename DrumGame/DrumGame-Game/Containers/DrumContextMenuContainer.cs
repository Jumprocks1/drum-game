using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Containers;

// Draw order:
// Back
//   Game content
//   Blocker (only when menu open) - prevents tooltips below us
//   Menu
//   Tooltips (parent container)
// Front

public class DrumContextMenuContainer : ContextMenuContainer
{
    public DrumContextMenu Menu;
    protected override Menu CreateMenu() => Menu = new DrumContextMenu
    {
        Depth = -5 // make sure menu is above the blocker
    };
    MouseBlocker Blocker;
    public DrumContextMenuContainer()
    {
        RelativeSizeAxes = Axes.Both;
        AddInternal(Blocker = new() { Depth = -4, Alpha = 0, RelativeSizeAxes = Axes.Both });
        Menu.StateChanged += state => Blocker.Alpha = state == MenuState.Open ? 1 : 0;
    }
    class MouseBlocker : Drawable
    {
        protected override bool Handle(UIEvent e)
        {
            var mouseEvent = e is MouseEvent;
            if (mouseEvent)
            {
                if (e is MouseButtonEvent || e is ScrollEvent)
                    ((DrumContextMenuContainer)Parent).Menu.Close();
                return true;
            }
            return false;
        }
    }
}
