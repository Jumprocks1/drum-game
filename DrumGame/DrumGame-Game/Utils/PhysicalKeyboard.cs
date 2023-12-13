using System.Collections.Generic;
using osu.Framework.Input.Bindings;
using osuTK;

namespace DrumGame.Game.Views;

public static class PhysicalKeyboard
{
    public static readonly List<(Vector2, InputKey, Vector2)> SpecialKeys = new()
    {
        (new Vector2(0, 2.25f), InputKey.Tab, new Vector2(1.5f, 1)),
        (new Vector2(0, 3.25f), InputKey.CapsLock, new Vector2(1.75f, 1)),
        (new Vector2(0, 4.25f), InputKey.LShift, new Vector2(2.25f, 1)),
        (new Vector2(0, 5.25f), InputKey.LControl, new Vector2(1.25f, 1)),
        (new Vector2(1.25f, 5.25f), InputKey.LSuper, new Vector2(1.25f, 1)),
        (new Vector2(2.5f, 5.25f), InputKey.LAlt, new Vector2(1.25f, 1)),
        (new Vector2(3.75f, 5.25f), InputKey.Space, new Vector2(6.25f, 1)),
        (new Vector2(10f, 5.25f), InputKey.RAlt, new Vector2(1.25f, 1)),
        (new Vector2(11.25f, 5.25f), InputKey.RSuper, new Vector2(1.25f, 1)),
        (new Vector2(12.5f, 5.25f), InputKey.Menu, new Vector2(1.25f, 1)),
        (new Vector2(13.75f, 5.25f), InputKey.RControl, new Vector2(1.25f, 1)),
        (new Vector2(12.25f, 4.25f), InputKey.RShift, new Vector2(2.75f, 1)),
        (new Vector2(12.75f, 3.25f), InputKey.Enter, new Vector2(2.25f, 1)),
        (new Vector2(13.5f, 2.25f), InputKey.BackSlash, new Vector2(1.5f, 1)),
        (new Vector2(13f, 1.25f), InputKey.BackSpace, new Vector2(2f, 1)),

        (new Vector2(18.5f, 5.25f), InputKey.Keypad0, new Vector2(2f, 1)),
        (new Vector2(21.5f, 4.25f), InputKey.KeypadEnter, new Vector2(1f, 2)),
        (new Vector2(21.5f, 2.25f), InputKey.KeypadPlus, new Vector2(1f, 2)),
    };
    public static readonly List<(Vector2, InputKey)> SquareKeys = new()
    {
        (new Vector2(0, 0), InputKey.Escape),
        (new Vector2(2, 0), InputKey.F1),
        (new Vector2(3, 0), InputKey.F2),
        (new Vector2(4, 0), InputKey.F3),
        (new Vector2(5, 0), InputKey.F4),
        (new Vector2(6.5f, 0), InputKey.F5),
        (new Vector2(7.5f, 0), InputKey.F6),
        (new Vector2(8.5f, 0), InputKey.F7),
        (new Vector2(9.5f, 0), InputKey.F8),
        (new Vector2(11, 0), InputKey.F9),
        (new Vector2(12, 0), InputKey.F10),
        (new Vector2(13, 0), InputKey.F11),
        (new Vector2(14, 0), InputKey.F12),

        (new Vector2(0, 1.25f), InputKey.Tilde),
        (new Vector2(1, 1.25f), InputKey.Number1),
        (new Vector2(2, 1.25f), InputKey.Number2),
        (new Vector2(3, 1.25f), InputKey.Number3),
        (new Vector2(4, 1.25f), InputKey.Number4),
        (new Vector2(5, 1.25f), InputKey.Number5),
        (new Vector2(6, 1.25f), InputKey.Number6),
        (new Vector2(7, 1.25f), InputKey.Number7),
        (new Vector2(8, 1.25f), InputKey.Number8),
        (new Vector2(9, 1.25f), InputKey.Number9),
        (new Vector2(10, 1.25f), InputKey.Number0),
        (new Vector2(11, 1.25f), InputKey.Minus),
        (new Vector2(12, 1.25f), InputKey.Plus),


        (new Vector2(1.5f, 2.25f), InputKey.Q),
        (new Vector2(2.5f, 2.25f), InputKey.W),
        (new Vector2(3.5f, 2.25f), InputKey.E),
        (new Vector2(4.5f, 2.25f), InputKey.R),
        (new Vector2(5.5f, 2.25f), InputKey.T),
        (new Vector2(6.5f, 2.25f), InputKey.Y),
        (new Vector2(7.5f, 2.25f), InputKey.U),
        (new Vector2(8.5f, 2.25f), InputKey.I),
        (new Vector2(9.5f, 2.25f), InputKey.O),
        (new Vector2(10.5f, 2.25f), InputKey.P),
        (new Vector2(11.5f, 2.25f), InputKey.BracketLeft),
        (new Vector2(12.5f, 2.25f), InputKey.BracketRight),


        (new Vector2(1.75f, 3.25f), InputKey.A),
        (new Vector2(2.75f, 3.25f), InputKey.S),
        (new Vector2(3.75f, 3.25f), InputKey.D),
        (new Vector2(4.75f, 3.25f), InputKey.F),
        (new Vector2(5.75f, 3.25f), InputKey.G),
        (new Vector2(6.75f, 3.25f), InputKey.H),
        (new Vector2(7.75f, 3.25f), InputKey.J),
        (new Vector2(8.75f, 3.25f), InputKey.K),
        (new Vector2(9.75f, 3.25f), InputKey.L),
        (new Vector2(10.75f, 3.25f), InputKey.Semicolon),
        (new Vector2(11.75f, 3.25f), InputKey.Quote),

        (new Vector2(2.25f, 4.25f), InputKey.Z),
        (new Vector2(3.25f, 4.25f), InputKey.X),
        (new Vector2(4.25f, 4.25f), InputKey.C),
        (new Vector2(5.25f, 4.25f), InputKey.V),
        (new Vector2(6.25f, 4.25f), InputKey.B),
        (new Vector2(7.25f, 4.25f), InputKey.N),
        (new Vector2(8.25f, 4.25f), InputKey.M),
        (new Vector2(9.25f, 4.25f), InputKey.Comma),
        (new Vector2(10.25f, 4.25f), InputKey.Period),
        (new Vector2(11.25f, 4.25f), InputKey.Slash),

        (new Vector2(15.25f, 0f), InputKey.PrintScreen),
        (new Vector2(16.25f, 0f), InputKey.ScrollLock),
        (new Vector2(17.25f, 0f), InputKey.Pause),

        (new Vector2(15.25f, 1.25f), InputKey.Insert),
        (new Vector2(16.25f, 1.25f), InputKey.Home),
        (new Vector2(17.25f, 1.25f), InputKey.PageUp),
        (new Vector2(15.25f, 2.25f), InputKey.Delete),
        (new Vector2(16.25f, 2.25f), InputKey.End),
        (new Vector2(17.25f, 2.25f), InputKey.PageDown),

        (new Vector2(16.25f, 4.25f), InputKey.Up),
        (new Vector2(15.25f, 5.25f), InputKey.Left),
        (new Vector2(16.25f, 5.25f), InputKey.Down),
        (new Vector2(17.25f, 5.25f), InputKey.Right),

        (new Vector2(18.5f, 1.25f), InputKey.NumLock),
        (new Vector2(19.5f, 1.25f), InputKey.KeypadDivide),
        (new Vector2(20.5f, 1.25f), InputKey.KeypadMultiply),
        (new Vector2(21.5f, 1.25f), InputKey.KeypadSubtract),
        (new Vector2(18.5f, 2.25f), InputKey.Keypad7),
        (new Vector2(19.5f, 2.25f), InputKey.Keypad8),
        (new Vector2(20.5f, 2.25f), InputKey.Keypad9),
        (new Vector2(18.5f, 3.25f), InputKey.Keypad4),
        (new Vector2(19.5f, 3.25f), InputKey.Keypad5),
        (new Vector2(20.5f, 3.25f), InputKey.Keypad6),
        (new Vector2(18.5f, 4.25f), InputKey.Keypad1),
        (new Vector2(19.5f, 4.25f), InputKey.Keypad2),
        (new Vector2(20.5f, 4.25f), InputKey.Keypad3),
        (new Vector2(20.5f, 5.25f), InputKey.KeypadDecimal),
    };
}
