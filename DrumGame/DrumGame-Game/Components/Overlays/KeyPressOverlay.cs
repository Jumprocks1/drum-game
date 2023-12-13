using System;
using DrumGame.Game.Commands;
using DrumGame.Game.Components.Abstract;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osuTK;

namespace DrumGame.Game.Components.Overlays;

public class KeyPressOverlay : FadeContainer
{
    SpriteText Text;


    public KeyPressOverlay()
    {
        Util.KeyPressOverlay = this;
        AutoSizeAxes = Axes.Both;
        Anchor = Anchor.BottomLeft;
        Origin = Anchor.BottomLeft;
        Y = -100;
        X = 100;
        AddInternal(Text = new SpriteText
        {
            Padding = new MarginPadding(15),
            Font = FrameworkFont.Regular.With(size: 20)
        });
        Util.CommandController.AfterCommandActivated += AfterCommandActivated;
    }

    Vector2 DragAnchor;
    protected override bool OnDragStart(DragStartEvent e)
    {
        DragAnchor = e.MouseDownPosition - Position;
        return true;
    }
    protected override void OnDrag(DragEvent e)
    {
        Position = e.MousePosition - DragAnchor;
    }

    string baseText;
    int repeatCount = 0;
    void UpdateDisplay(string text, string keys)
    {
        string res;
        if (text == baseText)
        {
            res = $"{baseText} x{++repeatCount}";
        }
        else
        {
            repeatCount = 1;
            res = baseText = text;
        }
        if (keys != null) res += $"   [ {keys} ]";
        Text.Text = res;
    }

    public void Handle(string name, KeyCombo keyCombo)
    {
        UpdateDisplay(name, keyCombo.DisplayString);
        Touch();
    }

    void AfterCommandActivated(CommandInfo command, CommandContext context) =>
        Handle(command.Name, context.KeyEvent != null ? new KeyCombo(context.KeyEvent) : KeyCombo.None);

    protected override void Dispose(bool isDisposing)
    {
        Util.CommandController.AfterCommandActivated -= AfterCommandActivated;
        base.Dispose(isDisposing);
    }
}