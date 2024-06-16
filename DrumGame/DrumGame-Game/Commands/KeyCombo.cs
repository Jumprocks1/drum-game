using System;
using DrumGame.Game.Channels;
using DrumGame.Game.Utils;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Commands;

[Flags]
public enum ModifierKey
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    Super = 8, // Not used
    CtrlShift = 3,
    CtrlAlt = 5,
    ShiftAlt = 6,
    CtrlShiftAlt = 7,
};
public static class ModifierKeyExtensions
{
    public static bool HasKey(this ModifierKey modifier, InputKey key) =>
        ((key == InputKey.Control || key == InputKey.LControl || key == InputKey.RControl) && modifier.HasFlagFast(ModifierKey.Ctrl)) ||
        ((key == InputKey.Shift || key == InputKey.LShift || key == InputKey.RShift) && modifier.HasFlagFast(ModifierKey.Shift)) ||
        ((key == InputKey.Alt || key == InputKey.LAlt || key == InputKey.RAlt) && modifier.HasFlagFast(ModifierKey.Alt));
}
public readonly struct KeyCombo
{
    public static readonly KeyCombo None = new(InputKey.None);
    public static string ToString(ModifierKey key) => key switch
    {
        ModifierKey.CtrlShift => "Ctrl+Shift",
        ModifierKey.CtrlAlt => "Ctrl+Alt",
        ModifierKey.CtrlShiftAlt => "Ctrl+Shift+Alt",
        ModifierKey.ShiftAlt => "Shift+Alt",
        _ => key.ToString()
    };
    public readonly ModifierKey Modifier;
    public readonly InputKey Key;
    public KeyCombo(ModifierKey modifier, InputKey key)
    {
        Modifier = modifier;
        Key = key;
    }
    public KeyCombo(InputKey key) : this(ModifierKey.None, key) { }
    public KeyCombo(ScrollEvent e)
    {
        Modifier = e.Modifier();
        Key = e.ScrollDelta.Y > 0 ? InputKey.MouseWheelUp :
            e.ScrollDelta.Y < 0 ? InputKey.MouseWheelDown :
            e.ScrollDelta.X > 0 ? InputKey.MouseWheelLeft :
            InputKey.MouseWheelRight;
    }
    public KeyCombo(KeyDownEvent e)
    {
        Modifier = e.Modifier();
        Key = KeyCombination.FromKey(e.Key);
    }
    public static implicit operator KeyCombo(InputKey key) => new(ModifierKey.None, key);
    public static implicit operator KeyCombo(DrumChannel channel) => new(ModifierKey.None, channel.InputKey());
    public override readonly string ToString() // this string is used for saving to keybind file
    {
        if (Modifier != ModifierKey.None)
            return ToString(Modifier) + "+" + Key.ToString();
        else
            return Key.ToString();
    }
    public string DisplayString => // use for pretty display inside a SpriteText
            Modifier != ModifierKey.None
                ? ToString(Modifier) + "+" + HotkeyDisplay.KeyString(Key)
                : HotkeyDisplay.KeyString(Key);
    public string MarkupString
    {
        get
        {
            var s = DisplayString;
            if (Key.IsMidi())
                return $"<midi>{DisplayString}</c>";
            return s;
        }
    }
    public static ModifierKey ParseModifier(string modifier) => modifier switch
    {
        "Ctrl" => ModifierKey.Ctrl,
        "Shift" => ModifierKey.Shift,
        "Alt" => ModifierKey.Alt,
        "Super" => ModifierKey.Super,
        "Ctrl+Shift" => ModifierKey.CtrlShift,
        "Ctrl+Alt" => ModifierKey.CtrlAlt,
        "Ctrl+Shift+Alt" => ModifierKey.CtrlShiftAlt,
        "Shift+Alt" => ModifierKey.ShiftAlt,
        _ => ModifierKey.None
    };
    public static KeyCombo Parse(string input)
    {
        var plus = input.LastIndexOf("+");
        if (plus == -1)
        {
            return new KeyCombo(ModifierKey.None, Enum.Parse<InputKey>(input, true));
        }
        else
        {
            var part1 = input[..plus];
            var part2 = input[(plus + 1)..];
            return new KeyCombo(ParseModifier(part1), Enum.Parse<InputKey>(part2, true));
        }
    }
    public static bool operator ==(KeyCombo a, KeyCombo b) => a.Modifier == b.Modifier && a.Key == b.Key;
    public static bool operator !=(KeyCombo a, KeyCombo b) => a.Modifier != b.Modifier || a.Key != b.Key;
    public override bool Equals(object obj) => obj is KeyCombo other && this.Equals(other);
    public bool Equals(KeyCombo p) => this == p;
    public override int GetHashCode() => (Modifier, Key).GetHashCode();
}
