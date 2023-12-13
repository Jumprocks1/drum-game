using System;
using System.Linq;
using DrumGame.Game.Commands;
using DrumGame.Game.Containers;
using DrumGame.Game.Utils;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;

namespace DrumGame.Game.Interfaces;

public interface IHasCommand : IHasCommandInfo
{
    Command Command { get; }
    CommandInfo IHasCommandInfo.CommandInfo => Util.CommandController[Command];

    public static bool HasHotkey(Command command) => Util.CommandController[command].Bindings.Count > 0;
    public static string GetMarkupTooltip(Command command) => GetMarkupTooltip(Util.CommandController[command]);
    public static string GetMarkupHotkeyBase(Command command) => GetMarkupHotkeyBase(Util.CommandController[command]);
    public static string GetMarkupHotkeyString(Command command) => GetMarkupHotkeyString(Util.CommandController[command]);
}

public interface IHasCommandInfo : IHasMarkupTooltip
{
    CommandInfo CommandInfo { get; }
    bool AllowClick => false;
    string IHasMarkupTooltip.MarkupTooltip => GetMarkupTooltip(CommandInfo);

    public static string GetMarkupTooltip(CommandInfo commandInfo)
        => commandInfo == null ? null : GetMarkupTooltip(commandInfo.Name, GetMarkupHotkeyString(commandInfo), commandInfo.HelperMarkup);
    public static string GetMarkupTooltip(string name, string hotkeyMarkup, string helperMarkup = null)
    {
        var s = $"<command>{name}</> {hotkeyMarkup}";
        if (helperMarkup == null) return s;
        return s + "\n\n" + helperMarkup;
    }
    public static string GetMarkupHotkeyBase(CommandInfo commandInfo)
    {
        var bindings = commandInfo.Bindings;
        if (bindings.Count == 0) return null;
        return string.Join(", ", bindings.Select(e => e.MarkupString));
    }
    public static string GetMarkupHotkeyString(CommandInfo commandInfo)
    {
        var hotkeyText = GetMarkupHotkeyBase(commandInfo);

        if (hotkeyText == null)
            return $"<faded>(Unbound - Right click to set)</c>";

        return $"({hotkeyText})";
    }
}