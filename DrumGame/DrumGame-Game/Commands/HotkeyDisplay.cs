using System.Collections.Generic;
using DrumGame.Game.Channels;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;

namespace DrumGame.Game.Commands;

public static class HotkeyDisplay
{
    public static readonly FontUsage Font = FrameworkFont.Regular.With(size: 16);
    public static readonly FontUsage SmallFont = FrameworkFont.Regular.With(size: 12);
    static Drawable DrawKey(Drawable inner) => new Container
    {
        AutoSizeAxes = Axes.Both,
        Anchor = Anchor.CentreLeft,
        Origin = Anchor.CentreLeft,
        Children = new Drawable[] {
            new Box {
                Colour = DrumColors.DarkBackgroundAlt,
                RelativeSizeAxes = Axes.Both,
            }.WithEffect(new EdgeEffect {
                CornerRadius = 2,
                Parameters = new EdgeEffectParameters {
                    Colour = DrumColors.LightBorder,
                    Radius = 2,
                    Hollow = true,
                    Type= EdgeEffectType.Glow
                }
            }),
            inner
        }
    };

    static void CheckModifier(FillFlowContainer container, KeyCombo key, ModifierKey modifier)
    {
        if ((key.Modifier & modifier) == modifier)
        {
            container.Add(DrawKey(new SpriteText
            {
                Text = modifier.ToString(),
                Padding = new MarginPadding { Left = 2, Right = 2 },
                Font = Font
            }));
            container.Add(new SpriteText
            {
                Text = "+",
                Padding = new MarginPadding { Left = 2, Right = 2 },
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Font = SmallFont
            });
        }
    }
    public static string KeyString(InputKey key) => key switch
    {
        InputKey.BracketLeft => "[",
        InputKey.BracketRight => "]",
        InputKey.Plus => "+",
        InputKey.Minus => "-",
        InputKey.KeypadPlus => "Kp+",
        InputKey.KeypadMinus => "Kp-",
        InputKey.Number0 => "0",
        InputKey.Number1 => "1",
        InputKey.Number2 => "2",
        InputKey.Number3 => "3",
        InputKey.Number4 => "4",
        InputKey.Number5 => "5",
        InputKey.Number6 => "6",
        InputKey.Number7 => "7",
        InputKey.Number8 => "8",
        InputKey.Number9 => "9",
        InputKey.Keypad0 => "Kp0",
        InputKey.Keypad1 => "Kp1",
        InputKey.Keypad2 => "Kp2",
        InputKey.Keypad3 => "Kp3",
        InputKey.Keypad4 => "Kp4",
        InputKey.Keypad5 => "Kp5",
        InputKey.Keypad6 => "Kp6",
        InputKey.Keypad7 => "Kp7",
        InputKey.Keypad8 => "Kp8",
        InputKey.Keypad9 => "Kp9",
        InputKey.KeypadEnter => "KpEnt",
        InputKey.Escape => "Esc",
        InputKey.Semicolon => ";",
        InputKey.Quote => "\"",
        InputKey.Comma => ",",
        InputKey.Period => ".",
        InputKey.Slash => "/",
        InputKey.Tilde => "`",
        InputKey.Insert => "Ins",
        InputKey.PageUp => "PgUp",
        InputKey.PageDown => "PgDn",
        InputKey.Up => "⬆",
        InputKey.Down => "⬇",
        InputKey.Left => "⬅",
        InputKey.Right => "➡",
        InputKey.Delete => "Del",
        InputKey.KeypadMultiply => "Kp*",
        InputKey.KeypadDivide => "Kp/",
        InputKey.KeypadDecimal => "Kp.",
        InputKey.LControl => "Ctrl",
        InputKey.RControl => "Ctrl",
        InputKey.LShift => "Shift",
        InputKey.RShift => "Shift",
        InputKey.LAlt => "Alt",
        InputKey.RAlt => "Alt",
        InputKey.LSuper => "Super",
        InputKey.RSuper => "Super",
        InputKey.BackSpace => "Back",
        InputKey.BackSlash => "\\",
        InputKey.PrintScreen => "PrtScr",
        InputKey.ScrollLock => "ScrLk",
        InputKey.NumLock => "Num",
        InputKey.None => null,
        _ => key.IsMidi() ? key.MidiString() : key.ToString()
    };
    public static FillFlowContainer RenderKeys(List<KeyCombo> keys)
    {
        var hotkeyContainer = new FillFlowContainer
        {
            Direction = FillDirection.Horizontal,
            AutoSizeAxes = Axes.X
        };
        for (var i = 0; i < keys.Count; i++)
        {
            RenderHotkey(hotkeyContainer, keys[i]);
            if (i < keys.Count - 1)
            {
                hotkeyContainer.Add(Comma());
            }
        }
        return hotkeyContainer;
    }
    public static void RenderHotkey(FillFlowContainer container, KeyCombo key)
    {
        CheckModifier(container, key, ModifierKey.Ctrl);
        CheckModifier(container, key, ModifierKey.Shift);
        CheckModifier(container, key, ModifierKey.Alt);
        if (key.Key != InputKey.None)
        {
            container.Add(DrawKey(new SpriteText
            {
                Text = KeyString(key.Key),
                Padding = new MarginPadding { Left = 2, Right = 2 },
                Font = Font
            }));
        }
    }
    public static Drawable Comma()
    {
        return new SpriteText
        {
            Text = ",",
            Padding = new MarginPadding { Left = 2, Right = 4 },
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Font = Font
        };
    }
}
