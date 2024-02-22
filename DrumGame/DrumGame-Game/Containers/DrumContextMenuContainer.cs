using System;
using System.Linq;
using DrumGame.Game.Interfaces;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

namespace DrumGame.Game.Containers;

// Draw order:
// Back
//   Game content
//   Blocker (only when menu open) - prevents tooltips below us
//   Menu
//   Tooltips (parent container)
// Front

// Logic copied from ContextMenuContainer.cs due to too many private variables
public class DrumContextMenuContainer : CursorEffectContainer<ContextMenuContainer, IHasContextMenu>
{
    public readonly Menu Menu;

    IHasContextMenu MenuTarget;
    private Vector2 targetRelativePosition;

    private readonly Container content;

    protected override Container<Drawable> Content => content;

    protected override void OnSizingChanged()
    {
        base.OnSizingChanged();

        if (content != null)
        {
            // reset to none to prevent exceptions
            content.RelativeSizeAxes = Axes.None;
            content.AutoSizeAxes = Axes.None;

            // in addition to using this.RelativeSizeAxes, sets RelativeSizeAxes on every axis that is neither relative size nor auto size
            content.RelativeSizeAxes = Axes.Both & ~AutoSizeAxes;
            content.AutoSizeAxes = AutoSizeAxes;
        }
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        switch (e.Button)
        {
            case MouseButton.Right:
                var (target, items) = FindTargets()
                                      .Select(t => (target: t, items: t.ContextMenuItems))
                                      .FirstOrDefault(result => result.items != null);

                MenuTarget = target;

                if (MenuTarget == null || items.Length == 0)
                {
                    if (Menu.State == MenuState.Open)
                        Menu.Close();
                    return false;
                }

                Menu.Items = items;

                targetRelativePosition = MenuTarget.ToLocalSpace(e.ScreenSpaceMousePosition);

                Menu.Open();
                return true;

            default:
                close();
                return false;
        }
    }

    void close()
    {
        Menu.Close();
        MenuTarget = null;
    }

    protected override void UpdateAfterChildren()
    {
        base.UpdateAfterChildren();

        if (Menu.State != MenuState.Open || MenuTarget == null) return;

        if ((MenuTarget as Drawable)?.FindClosestParent<DrumContextMenuContainer>() != this || !MenuTarget.IsPresent)
        {
            close();
            return;
        }

        var pos = MenuTarget.ToSpaceOfOtherDrawable(targetRelativePosition, this);

        var overflow = pos + Menu.DrawSize - DrawSize;

        if (overflow.X > 0)
            pos.X -= Math.Clamp(overflow.X, 0, Menu.DrawWidth);
        if (overflow.Y > 0)
            pos.Y -= Math.Clamp(overflow.Y, 0, Menu.DrawHeight);

        if (pos.X < 0)
            pos.X += Math.Clamp(-pos.X, 0, Menu.DrawWidth);
        if (pos.Y < 0)
            pos.Y += Math.Clamp(-pos.Y, 0, Menu.DrawHeight);

        Menu.Position = pos;
    }

    MouseBlocker Blocker;
    public DrumContextMenuContainer()
    {
        AddInternal(content = new Container
        {
            RelativeSizeAxes = Axes.Both,
        });
        AddInternal(Menu = new DrumContextMenu
        {
            Depth = -5 // make sure menu is above the blocker
        });

        RelativeSizeAxes = Axes.Both;
        AddInternal(Blocker = new() { Depth = -4, Alpha = 0, RelativeSizeAxes = Axes.Both });
        Menu.StateChanged += state =>
        {
            Blocker.Alpha = state == MenuState.Open ? 1 : 0;
            if (MenuTarget is IHasContextMenuEvent e)
                e.ContextMenuStateChanged(state);
        };
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
