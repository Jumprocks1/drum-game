using System;
using DrumGame.Game.Components;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osuTK.Input;

namespace DrumGame.Game.Commands.Requests;

public class ButtonOption
{
    public string Text;
    public bool AutoSize;
}
public class ButtonArray : CompositeDrawable
{
    public int Focus = 0;
    public const float ButtonWidth = 150;
    public const float ButtonHeight = 30;
    public const float Spacing = 5;
    readonly Action<int> OnPress;
    public ButtonArray(Action<int> onPress, params ButtonOption[] options)
    {
        OnPress = onPress;
        var x = 0f;
        for (int i = 0; i < options.Length; i++)
        {
            var j = i; // capture for lambda
            var option = options[i];
            var b = new DrumButton
            {
                Width = ButtonWidth,
                Height = ButtonHeight,
                X = x,
                Text = option.Text,
                AutoFontSize = option.AutoSize,
                Action = () => onPress(j)
            };
            AddInternal(b);
            x += 150;
            if (i != options.Length - 1) x += Spacing;
        }
        AutoSizeAxes = Axes.Y;
        Width = x;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        UpdateFocus();
    }

    public void UpdateFocus()
    {
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            var child = (DrumButton)InternalChildren[i];
            child.BackgroundColour = Focus == i ? DrumColors.ActiveButton : DrumColors.Button;
        }
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key == Key.Left || (e.Key == Key.Tab && e.ShiftPressed))
        {
            Focus = (Focus + InternalChildren.Count - 1) % InternalChildren.Count;
            UpdateFocus();
        }
        else if (e.Key == Key.Right || e.Key == Key.Tab)
        {
            Focus = (Focus + 1) % InternalChildren.Count;
            UpdateFocus();
        }
        else if (e.Key == Key.Enter || e.Key == Key.KeypadEnter)
        {
            OnPress(Focus);
        }
        else
        {
            return base.OnKeyDown(e);
        }
        return true;
    }
}
