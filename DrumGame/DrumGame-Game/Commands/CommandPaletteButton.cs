using DrumGame.Game.Components;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK.Input;

namespace DrumGame.Game.Commands;

public class CommandPaletteButton : CommandButtonBase, IHasMarkupTooltip, IHasAppearDelay
{
    CommandInfo CommandInfo;
    public CommandPaletteButton(CommandInfo command)
    {
        CommandInfo = command;
        var hotkeyContainer = HotkeyDisplay.RenderKeys(command.Bindings);
        hotkeyContainer.Anchor = Anchor.CentreRight;
        hotkeyContainer.Origin = Anchor.CentreRight;
        hotkeyContainer.X = -CommandPalette.Margin;
        Text = command.Name;
        Add(hotkeyContainer);
    }
    public string MarkupTooltip
    {
        get
        {
            var hotkeyText = IHasCommand.GetMarkupHotkeyBase(Command.EditKeybind);
            // note, don't get confused, CommandInfo != EditKeybind command
            var verb = CommandInfo.Bindings.Count == 0 ? "set" : "change";
            if (hotkeyText == null) // null => no hotkeys currently for EditKeybind
                return $"Right click to {verb} keybind";
            else
                return $"Right click (or {IHasCommand.GetMarkupHotkeyBase(Command.EditKeybind)}) to {verb} keybind";
        }
    }
    public double AppearDelay => 500;

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button == MouseButton.Right)
        {
            Util.Palette.EditKeybind(CommandInfo);
            return true;
        }
        else return base.OnMouseDown(e);
    }
}
public class CommandButtonBase : BasicButton
{
    public new const float Height = 25;
    public CommandButtonBase()
    {
        RelativeSizeAxes = Axes.X;
        base.Height = Height;
        BackgroundColour = Colour4.Transparent;
    }
    protected override SpriteText CreateText()
    {
        return new SpriteText
        {
            Depth = -1,
            Padding = new MarginPadding { Left = CommandPalette.Margin },
            Origin = Anchor.CentreLeft,
            Anchor = Anchor.CentreLeft,
            Font = FrameworkFont.Regular
        };
    }
}
