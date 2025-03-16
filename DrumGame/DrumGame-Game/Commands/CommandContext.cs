using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Components.Basic.Autocomplete;
using DrumGame.Game.Modals;
using DrumGame.Game.Utils;
using osu.Framework.Bindables;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Commands;

public class CommandContext
{
    public static CommandContext Basic => new(Util.Palette, null);
    public CommandPaletteContainer Palette;
    public CommandInfo CommandInfo;
    // lets handlers know if this command is triggered from Midi
    // if it is, it should respect DrumGameConfigManager.MidiInputOffset
    public bool Midi;
    public KeyDownEvent KeyEvent;
    public bool Repeat => KeyEvent != null ? KeyEvent.Repeat : false;
    public object Data; // miscelaneous, could be anything

    public CommandContext() { }
    public CommandContext(CommandPaletteContainer palette, CommandInfo commandInfo)
    {
        Palette = palette;
        CommandInfo = commandInfo;
    }
    public RequestModal GetString(Action<string> action, string title, string label = null, string current = null, string description = null)
    {
        if (TryGetParameter(out string s))
        {
            action(s);
            return null;
        }
        return Palette.RequestString(title, label, current ?? string.Empty, action, description);
    }
    public FileRequest GetFile(Action<string> action, string title, string description = null)
    {
        if (TryGetParameter(out string s))
        {
            action(s);
            return null;
        }
        return Palette.RequestFile(title, description, action);
    }

    public RequestModal GetString(IEnumerable<string> items, Action<string> action, string title, string current = null, string description = null)
        => GetItem<BasicAutocompleteOption>(items.Select(e => new BasicAutocompleteOption(e)), e => e.Name, e => action(e.Name), title, null, description);
    public RequestModal GetItem<T>(IEnumerable<T> items, Func<T, string> key, Action<T> action, string title, T current = null, string description = null)
        where T : class, IFilterable
    {
        if (TryGetParameter(out string s) && !string.IsNullOrWhiteSpace(s))
        {
            var found = items.FirstOrDefault(e => key(e) == s) ?? items.FirstOrDefault(e => key(e).StartsWith(s));
            if (found != null)
            {
                action(found);
                return null;
            }
        }
        return Palette.Request(new RequestConfig
        {
            Title = title,
            Description = description,
            Field = new AutocompleteFieldConfig<T> { Options = items.AsArray(), DefaultValue = current, OnCommit = action }
        });
    }
    public RequestModal GetStringFreeSolo(IEnumerable<string> items, Action<string> action,
        string title, string current = null, string description = null)
    {
        if (TryGetParameter(out string s))
        {
            action(s);
            return null;
        }
        return Palette.Request(new RequestConfig
        {
            Title = title,
            Description = description,
            Field = new FreeSoloFieldConfig { Options = items.AsArray(), DefaultValue = current, OnCommit = action }
        });
    }
    public RequestModal GetItem<T>(Action<T> action, string title, T current = default, string description = null) where T : struct, Enum
    {
        if (TryGetParameter<T>(out T found))
        {
            action(found);
            return null;
        }
        return Palette.Request(new RequestConfig
        {
            Title = title,
            Description = description,
            Field = new EnumFieldConfig<T> { OnCommit = action, DefaultValue = current }
        });
    }
    public RequestModal GetItem<T>(Bindable<T> bindable, string title, string description = null) where T : struct, Enum
        => GetItem<T>(e => bindable.Value = e, title, bindable.Value, description);

    public void ActivateCommand(Command command) => Palette.CommandController.ActivateCommand(command);
    public void ActivateCommand(Command command, params object[] parameters) =>
        Palette.CommandController.ActivateCommand(command, parameters);
    public bool GetNumber(Action<double> action, string title, string label = null, double current = 0)
    {
        if (TryGetParameter(out double d))
        {
            action(d);
            return true;
        }
        Palette.RequestNumber(title, label, current, action);
        return true;
    }
    public bool GetNumber(Bindable<double> value, string title, string label = null) =>
        GetNumber(d => value.Value = d, title, label, value.Value);
    public bool HandleRequest(RequestConfig config)
    {
        if (config.OnCommitBasic == null) throw new Exception("Expected OnCommitBasic");
        if (CommandInfo.Parameters != null)
        {
            var unusedParameters = CommandInfo.Parameters.ToList();
            var values = new List<(string Key, object Value)>();
            foreach (var field in config.Fields)
            {
                var valueFound = false;
                for (var i = 0; i < unusedParameters.Count; i++)
                    if (CommandInfo.TryConvertParameter(field.OutputType, unusedParameters[i], out var value))
                    {
                        values.Add((field.Key, value));
                        valueFound = true;
                    }
                if (!valueFound) break;
            }
            if (values.Count == config.Fields.Length)
            {
                config.OnCommitBasic(new(values));
                return true;
            }
        }
        Palette.Request(config);
        return true;
    }
    public bool TryGetParameter<T>(int i, out T value) => CommandInfo.TryGetParameter(i, out value);
    public bool TryGetParameter<T>(out T value)
    {
        if (CommandInfo.Parameters != null)
            for (var i = 0; i < CommandInfo.Parameters.Length; i++)
                if (CommandInfo.TryGetParameter(i, out value))
                    return true;
        value = default;
        return false;
    }
    public void ShowMessage(string message) => Palette?.ShowMessage(message);
}
