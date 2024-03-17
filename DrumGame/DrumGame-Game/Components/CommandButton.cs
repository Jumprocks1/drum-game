using System;
using DrumGame.Game.Commands;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Components;

public class CommandButton : DrumButton, IHasCommandInfo
{
    public CommandInfo CommandInfo { get; }
    public CommandButton(Command command) : this(Util.CommandController[command]) { }
    public CommandButton(CommandInfo commandInfo)
    {
        Enabled.Value = commandInfo != null;
        CommandInfo = commandInfo;
    }
    public Action AfterActivate;
}