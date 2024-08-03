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
    public static string GetMarkupTooltipIgnoreUnbound(Command command) => GetMarkupTooltipIgnoreUnbound(Util.CommandController[command]);
    public static string GetMarkupTooltipNoModify(Command command) => GetMarkupTooltipNoModify(Util.CommandController[command]);
    public static string GetMarkupHotkeyBase(Command command) => GetMarkupHotkeyBase(Util.CommandController[command]);
    public static string GetMarkupHotkeyString(Command command) => GetMarkupHotkeyString(Util.CommandController[command], true);
}

public interface IHasCommandInfo : IHasMarkupTooltip, IHasCursor
{
    SDL2.SDL.SDL_SystemCursor? IHasCursor.Cursor => DisableClick ? null : SDL2.SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_HAND;
    CommandInfo CommandInfo { get; }
    bool DisableClick => false;
    string IHasMarkupTooltip.MarkupTooltip => GetMarkupTooltip(CommandInfo);

    public static string GetMarkupTooltip(CommandInfo commandInfo, bool modify = true)
        => commandInfo == null ? null : GetMarkupTooltip(commandInfo.Name, GetMarkupHotkeyString(commandInfo, modify), commandInfo.HelperMarkup);
    public static string GetMarkupTooltipNoModify(CommandInfo commandInfo) => GetMarkupTooltip(commandInfo, false);
    public static string GetMarkupTooltipIgnoreUnbound(CommandInfo commandInfo)
    {
        if (commandInfo == null) return null;
        var hotkeyText = GetMarkupHotkeyBase(commandInfo);
        return GetMarkupTooltip(commandInfo.Name, hotkeyText == null ? null : $"({hotkeyText})");
    }
    public static string GetMarkupTooltip(string name, string hotkeyMarkup, string helperMarkup = null)
    {
        var s = hotkeyMarkup == null ? $"<command>{name}</>" : $"<command>{name}</> {hotkeyMarkup}";
        if (helperMarkup == null) return s;
        return s + "\n\n" + helperMarkup;
    }
    public static string GetMarkupHotkeyBase(CommandInfo commandInfo)
    {
        var bindings = commandInfo.Bindings;
        if (bindings.Count == 0) return null;
        return string.Join(", ", bindings.Select(e => e.MarkupString));
    }
    // modify should be set when in a context where right clicking will allow editing the command this tooltip is for
    public static string GetMarkupHotkeyString(CommandInfo commandInfo, bool modify)
    {
        var hotkeyText = GetMarkupHotkeyBase(commandInfo);

        if (hotkeyText == null)
        {
            if (modify)
                return $"<faded>(Unbound - Right click to set)</c>";
            else
                return $"<faded>(Unbound)</c>";
        }

        return $"({hotkeyText})";
    }
}