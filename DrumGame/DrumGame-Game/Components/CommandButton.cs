using System;
using DrumGame.Game.Commands;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Components;

public class CommandButton : DrumButton, IHasCommandInfo
{
    string IHasMarkupTooltip.MarkupTooltip => MarkupTooltip ?? IHasCommandInfo.GetMarkupTooltip(CommandInfo);
    SDL2.SDL.SDL_SystemCursor? IHasCursor.Cursor => Cursor ?? (((IHasCommandInfo)this).DisableClick ? null : SDL2.SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_HAND);
    public CommandInfo CommandInfo { get; }
    public CommandButton(Command command) : this(Util.CommandController[command]) { }
    public CommandButton(CommandInfo commandInfo)
    {
        Enabled.Value = commandInfo != null;
        CommandInfo = commandInfo;
    }
    public Action AfterActivate;
}