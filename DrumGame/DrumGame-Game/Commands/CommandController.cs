using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DrumGame.Game.Channels;
using DrumGame.Game.Midi;
using DrumGame.Game.Utils;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Logging;

namespace DrumGame.Game.Commands;

public delegate bool CommandHandlerWithContext(CommandContext context);
public delegate void CommandHandlerSimple();

// this contains all parameter info for a single command
// could consider restructuring this so that there was a class for a single parameter,
//   then this class would basically be a list of those new classes
public class ParameterInfo
{
    public Type[] Types;
    public Func<Command, object[], string> GetName;
    public string[] ParameterNames;
    public string[] ParameterTooltips;
    public string ParameterName(int i) => ParameterNames?[i];
    public string ParameterTooltip(int i) => ParameterTooltips?[i];
    public ParameterInfo(Type[] types)
    {
        Types = types;
    }
}
public class CommandController
{
    public CommandPaletteContainer Palette;
    // the 3 arrays below here are expected to be indexed with a Command enum
    // parameter info is it's own array since they apply to all CommandInfo's of a specific Command
    // could consider merging these 3 arrays into a new type
    public readonly ParameterInfo[] ParameterInfo;
    // Default commands represent the commands with no parameters (users will have to fill in parameters)
    public readonly CommandInfo[] DefaultCommandInfos;
    public CommandInfo this[Command command] => DefaultCommandInfos[(int)command];
    public readonly List<CommandInfo>[] ParameterCommands;
    public readonly Dictionary<KeyCombo, List<CommandInfo>> KeyBindings = new(256);
    readonly List<Command> PriorityCommands = new(); // we don't allow prioritizing CommandInfos for now
    public List<CommandInfo> OrderedCommands = new(256); // 227 used as of 9/10/2023
    public void ReRegister()
    {
        // ParameterInfo is just an array so it gets fixed up in RegisterCommands
        // DefaultCommandInfos also gets overwritten just fine in RegisterCommands
        Array.Clear(ParameterCommands); // ParameterCommands is an array of Lists, so it needs to be reset (since new ones are added to the list)
        KeyBindings.Clear();
        OrderedCommands.Clear();
        var oldHandlers = new List<object>[DefaultCommandInfos.Length];
        for (var i = 0; i < DefaultCommandInfos.Length; i++)
            if (DefaultCommandInfos[i] != null) oldHandlers[i] = DefaultCommandInfos[i].Handlers;
        CommandList.RegisterCommands(this);
        for (var i = 0; i < DefaultCommandInfos.Length; i++)
            if (DefaultCommandInfos[i] != null) DefaultCommandInfos[i].Handlers = oldHandlers[i];
    }
    public CommandController()
    {
        Util.CommandController = this;
        // arrays are much more efficient than dictionaries when there's a fix number of inputs
        // in this case, currently there are less than 100 commands, which absolutely means the array will always be better
        // this is less true for a sparse array when there's like 10000 items, but that is not the case here
        var commandCount = (int)Command.MAX_VALUE;
        DefaultCommandInfos = new CommandInfo[commandCount];
        ParameterCommands = new List<CommandInfo>[commandCount];
        ParameterInfo = new ParameterInfo[commandCount];
        // this takes 6-7ms as of 10/14/2021
        CommandList.RegisterCommands(this);
    }
    public ParameterInfo SetParameterInfo(Command command, params Type[] types) => ParameterInfo[(int)command] = new ParameterInfo(types);
    public ParameterInfo SetParameterInfo(Command command, ParameterInfo info) => ParameterInfo[(int)command] = info;
    public CommandInfo RegisterCommand(Command command, string name = null, params KeyCombo[] keys)
    {
        var c = new CommandInfo(command, name ?? command.ToString().FromPascalCase(), keys);
        c.Handlers = new(); // since this will be a DefaultCommandInfo, we must add handlers
        DefaultCommandInfos[(int)command] = c;
        RegisterCommandInfo(c);
        return c;
    }
    public CommandInfo RegisterCommand(Command command, KeyCombo key, string name = null)
        => RegisterCommand(command, name, key);
    public CommandInfo RegisterCommand(Command command, params KeyCombo[] keys)
        => RegisterCommand(command, null, keys);
    public void RegisterCommandInfo(CommandInfo c)
    {
        OrderedCommands.Add(c);
        List<CommandInfo> target;
        if (c.Parameters != null)
        {
            target = ParameterCommands[(int)c.Command];
            if (target == null)
            {
                ParameterCommands[(int)c.Command] = target = new List<CommandInfo>();
            }
            target.Add(c);
            if (ParameterInfo[(int)c.Command] == null)
                SetParameterInfo(c.Command, [.. c.Parameters.Select(e => e.GetType())]);
        }
        foreach (var key in c.Bindings)
        {
            if (!KeyBindings.TryGetValue(key, out target))
            {
                KeyBindings[key] = target = new List<CommandInfo>();
            }
            target.Add(c);
        }
    }

    // this should work I think
    public void RemoveCommandInfo(CommandInfo c)
    {
        OrderedCommands.Remove(c);
        if (c.Parameters != null)
            ParameterCommands[(int)c.Command]?.Remove(c);
        foreach (var key in c.Bindings)
        {
            if (KeyBindings.TryGetValue(key, out var target))
                target.Remove(c);
        }
    }

    private void RegisterHandlerObject(Command command, object handler)
    {
        var target = DefaultCommandInfos[(int)command];
        if (target == null)
        {
            throw new Exception($"Attempt to add a handler to a command that is not registered: {command}");
        }
        target.Handlers.Add(handler);
    }
    public void RegisterHandler(Command command, CommandHandlerWithContext handler)
        => RegisterHandlerObject(command, handler);
    // Make sure to call RemoveHandler, there should be equal references for RegisterHandler and RemoveHandler
    public void RegisterHandler(Command command, CommandHandlerSimple handler)
        => RegisterHandlerObject(command, handler);

    private void RemoveHandlerObject(Command command, object handler)
    {
        var target = DefaultCommandInfos[(int)command];
        if (target != null)
        {
            target.Handlers.Remove(handler);
        }
    }
    public void RemoveHandler(Command command, CommandHandlerWithContext handler)
        => RemoveHandlerObject(command, handler);
    public void RemoveHandler(Command command, CommandHandlerSimple handler)
        => RemoveHandlerObject(command, handler);

    public object CreateDelegate(object instance, MethodInfo method) => method.ReturnType == typeof(void) ?
        Delegate.CreateDelegate(typeof(CommandHandlerSimple), instance, method) as CommandHandlerSimple :
        Delegate.CreateDelegate(typeof(CommandHandlerWithContext), instance, method) as CommandHandlerWithContext;

    public void RegisterHandlers<T>(T target) where T : class
    {
        var type = typeof(T);
        foreach (var method in type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance))
        {
            var attribute = method.GetCustomAttribute<CommandHandlerAttribute>();
            if (attribute != null)
            {
                RegisterHandlerObject(attribute.GetCommand(method), CreateDelegate(target, method));
            }
        }
    }
    public void RemoveHandlers<T>(T target) where T : class
    {
        var type = typeof(T);
        foreach (var method in type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance))
        {
            var attribute = method.GetCustomAttribute<CommandHandlerAttribute>();
            if (attribute != null)
            {
                RemoveHandlerObject(attribute.GetCommand(method), CreateDelegate(target, method));
            }
        }
    }

    public List<CommandInfo> GetParameterCommands(Command command) => ParameterCommands[(int)command] ??= new();
    public CommandInfo GetParameterCommand(Command command, object parameter) => ParameterCommands[(int)command]?
        .Find(e => e.Parameters.Length > 0 && e.Parameters[0] == parameter) ?? CommandInfo.From(command, parameter);

    public bool HandleEvent(KeyDownEvent e) => ActivateKeyCombo(new KeyCombo(e), new CommandContext { KeyEvent = e });
    public bool HandleEvent(ScrollEvent e) => ActivateKeyCombo(new KeyCombo(e));
    // If the MIDI key doesn't have any successful activation, we try again with the DrumChannel for the MIDI key
    public bool OnMidiNote(MidiNoteOnEvent e)
    {
        var inputKey = e.InputKey;
        var context = new CommandContext { Midi = true };
        var res = ActivateKeyCombo(inputKey, context); // first we try the raw MIDI key
        if (res) return true;
        var drumChannelKey = e.DrumChannel.InputKey();
        // if the MIDI key failed, and the current drum channel key is different, then try again
        if (drumChannelKey == inputKey) return false; // don't want to try activating again when the first time failed
        return ActivateKeyCombo(drumChannelKey, context);
    }
    public bool ActivateKeyCombo(KeyCombo keyCombo, CommandContext context = null)
    {
        if (keyCombo.Key == InputKey.None) return false;
        if (KeyBindings.TryGetValue(keyCombo, out var commands))
        {
            if (commands.Count > 1 && PriorityCommands.Count > 0)
            {
                // this can be expensive to use if commands/PriorityCommands are large
                // generally commands will only have a count of 1 or 2 and PriorityCommands should by default have 0
                for (var i = PriorityCommands.Count - 1; i >= 0; i--)
                {
                    var find = commands.FindLast(e => e.Command == PriorityCommands[i]);
                    if (find != null && ActivateCommand(find, context)) return true;
                }
            }
            for (var i = commands.Count - 1; i >= 0; i--) // iterate backwards so commands registered last are prioritized
            {
                if (ActivateCommand(commands[i], context)) return true;
            }
        }
        return false;
    }
    // we could also try an array of ints for these methods
    // they would just +-1 to the array
    // we would have to sort the bound commands before activating, this seems a bit tricky/expensive
    public void AddPriority(Command command) => PriorityCommands.Add(command);
    public void ReducePriority(Command command) => PriorityCommands.RemoveAt(PriorityCommands.LastIndexOf(command));
    public void SetBinding(CommandInfo command, int replace, KeyCombo key)
    {
        if (replace > -1)
        {
            var oldBinding = command.Bindings[replace];
            command.Bindings.RemoveAt(replace);
            KeyBindings[oldBinding].Remove(command);
        }
        if (key.Key != InputKey.None)
        {
            command.Bindings.Add(key);
            if (!KeyBindings.TryGetValue(key, out List<CommandInfo> target))
            {
                KeyBindings[key] = target = new List<CommandInfo>();
            }
            target.Add(command);
        }
    }

    public bool IsHeld(Command command, InputState state = null) => IsHeld(this[command], state);
    public bool IsHeld(CommandInfo commandInfo, InputState state = null)
    {
        foreach (var binding in commandInfo.Bindings)
        {
            if (binding.IsHeld(state)) return true;
        }
        return false;
    }
    public bool ActivateCommand(Command command) => ActivateCommand(DefaultCommandInfos[(int)command]);
    public bool ActivateCommand(Command command, CommandContext context) => ActivateCommand(DefaultCommandInfos[(int)command], context);
    public bool ActivateCommand(Command command, params object[] parameters) =>
        ActivateCommand(new CommandInfo(command, DefaultCommandInfos[(int)command].Name) { Parameters = parameters });
    public event Action<CommandInfo, CommandContext> AfterCommandActivated;
    public bool ActivateCommand(CommandInfo commandInfo, CommandContext context = null)
    {
        if (commandInfo != null)
        {
            var defaultInfo = DefaultCommandInfos[(int)commandInfo.Command];
            if (defaultInfo != null)
            {
                context ??= new CommandContext();
                context.Palette = Palette;
                context.CommandInfo = commandInfo;
                for (var i = defaultInfo.Handlers.Count - 1; i >= 0; i--) // iterate backwards so "deeper" keybinds are prioritized
                {
                    var handler = defaultInfo.Handlers[i];
                    var res = false;
                    if (handler is CommandHandlerSimple chT)
                    {
                        chT();
                        res = true;
                    }
                    else if (handler is CommandHandlerWithContext chCtx)
                    {
                        res = chCtx(context);
                    }
                    if (res)
                    {
                        // Could log the declaring type of the delegate
                        AfterCommandActivated?.Invoke(commandInfo, context);
                        Logger.Log($"Executed command {commandInfo.Name}", level: LogLevel.Debug);
                        return true;
                    }
                }
            }
        }
        Logger.Log($"No handler found for command {commandInfo?.Name}", level: LogLevel.Debug);
        return false;
    }

    public CommandContext NewContext() => new(Palette, new CommandInfo(Command.None, null));
}
[AttributeUsage(AttributeTargets.Method)]
public class CommandHandlerAttribute : Attribute
{
    public Command Command;
    public CommandHandlerAttribute(Command command)
    {
        Command = command;
    }
    public CommandHandlerAttribute()
    {
    }
    // This Enum.Parse seems to have a large cost the first time it's called, but afterwords it is fast
    public Command GetCommand(MethodInfo methodInfo) => Command == Command.None ? Enum.Parse<Command>(methodInfo.Name) : Command;
}
