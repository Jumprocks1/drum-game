using osu.Framework.Allocation;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osuTK.Input;

namespace DrumGame.Game.Components;

public abstract class NonPositionalTextBox : TextBox
{
    public override bool HandleNonPositionalInput => HoldFocus;
    public override bool RequestsFocus => HoldFocus;
    private bool allowImmediateFocus => host?.OnScreenKeyboardOverlapsGameWindow != true;
    public void TakeFocus()
    {
        if (allowImmediateFocus) GetContainingFocusManager().ChangeFocus(this);
    }
    [Resolved] GameHost host { get; set; }
    private bool _holdFocus = true;
    public bool HoldFocus
    {
        get => allowImmediateFocus && _holdFocus; set
        {
            _holdFocus = value;
            if (!_holdFocus && HasFocus)
                base.KillFocus();
        }
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(Current.Value) && HasFocus)
            {
                Current.Value = string.Empty;
                return true;
            }
            else
            {
                return false;
            }
        }
        return base.OnKeyDown(e);
    }
}

