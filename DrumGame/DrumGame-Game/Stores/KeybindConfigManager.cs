using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DrumGame.Game.Commands;
using DrumGame.Game.Utils;
using osu.Framework.Configuration;
using osu.Framework.Input.Bindings;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace DrumGame.Game.Stores;

public class KeybindConfigManager : ConfigManager
{
    bool dirty = false;
    bool loaded = false; // we don't want to save before loading, since this would overwrite our bindings
    // this mostly tells us what the defaults are, since any overrides are related to changing those defaults
    // to reset binding to default, we just have to remove all overrides (probably in reverse order)
    public List<(CommandInfo Command, bool Add, KeyCombo Key)> Overrides = new();
    public const string FILENAME = @"keybinds.ini";

    public readonly Storage Storage;
    CommandController CommandController => Util.CommandController;

    public event Action KeybindChanged;
    public void AddCustomCommand(Command command, object[] parameters, KeyCombo key)
    {
        var ci = CommandInfo.From(command, parameters);
        ci.Bindings.Add(key);
        CommandController.RegisterCommandInfo(ci);
        Overrides.Add((ci, true, key));
        dirty = true;
        CheckSave();
    }

    public void RevealInFileExplorer() => Storage.PresentFileExternally(FILENAME);
    public void OpenExternally() => Storage.OpenFileExternally(FILENAME);
    public void SetBinding(CommandInfo command, int remove, KeyCombo key, bool trySimplify = false)
    {
        if (command.Bindings.Contains(key)) { Logger.Log($"{command.Command} already has a binding for {key}"); return; }
        var alreadyAdded = false;
        var alreadyRemoved = false;
        if (trySimplify)
        {
            // this isn't quite perfect, but it's good enough for now
            // it has issues if users try readding the default keybinds, since this code doesn't know what the defaults are
            // we could probably set it up so that SetBinding in the command controller returns true/false if it does anything
            // this would help us know what the defaults are here
            for (int i = Overrides.Count - 1; i >= 0; i--)
            {
                var o = Overrides[i];
                if (o.Command == command)
                {
                    if (remove > -1)
                    {
                        var replaceTarget = command.Bindings[remove];
                        if (replaceTarget == o.Key && o.Add)
                        {
                            Overrides.RemoveAt(i);
                            dirty = true;
                            alreadyRemoved = true;
                            continue;
                        }
                    }
                    if (key.Key != InputKey.None)
                    {
                        if (o.Key == key && !o.Add)
                        {
                            Overrides.RemoveAt(i);
                            dirty = true;
                            alreadyAdded = true;
                            continue;
                        }
                    }
                }
            }
        }
        if (remove > -1 && !alreadyRemoved)
        {
            Overrides.Add((command, false, command.Bindings[remove]));
            dirty = true;
        }
        if (key.Key != InputKey.None && !alreadyAdded)
        {
            Overrides.Add((command, true, key));
            dirty = true;
        }
        CommandController.SetBinding(command, remove, key);
        CheckSave();
    }

    public void CheckSave()
    {
        if (dirty && loaded)
        {
            KeybindChanged?.Invoke();
            QueueBackgroundSave();
        }
    }

    public void Reload()
    {
        loaded = false;
        dirty = false;
        Overrides.Clear();
        PerformLoad();
    }

    protected override void PerformLoad()
    {
        using var stream = Storage.GetStream(FILENAME);
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                try
                {
                    int equalsIndex = line.IndexOf('=');
                    if (line.Length == 0 || line[0] == '#' || equalsIndex < 0) continue;

                    var type = line[equalsIndex - 1];
                    var add = type == '+';
                    if (!add && type != '-') continue;
                    var key = line.AsSpan(0, equalsIndex - 1).Trim().ToString();
                    var val = KeyCombo.Parse(line.AsSpan(equalsIndex + 1).Trim().ToString());

                    var paramIndex = key.IndexOf('{');
                    CommandInfo command = null;
                    if (paramIndex > -1)
                    {
                        var enumString = key[..paramIndex];
                        var commandE = Enum.Parse<Command>(enumString);
                        var paramString = key[(paramIndex + 1)..key.IndexOf("}")];
                        var parameters = CommandParameters.Parse(paramString, CommandController.ParameterInfo[(int)commandE]?.Types);

                        var parameterCommands = CommandController.ParameterCommands[(int)commandE];
                        if (parameterCommands != null)
                        {
                            foreach (var c in parameterCommands)
                            {
                                if (c.ParametersEqual(parameters))
                                {
                                    command = c;
                                    break;
                                }
                            }
                        }
                        if (command == null)
                        {
                            if (add) // we can add without finding since we could have a custom parameter
                            {
                                // don't use `command` variable since setting that would trigger code below
                                AddCustomCommand(commandE, parameters, val);
                            }
                            else
                            {
                                throw new KeyNotFoundException($"Cannot remove {key}, command not found");
                            }
                        }
                    }
                    else
                    {
                        command = CommandController[Enum.Parse<Command>(key)];
                    }
                    if (command != null)
                    {
                        if (add)
                        {
                            SetBinding(command, -1, val);
                        }
                        else
                        {
                            var index = command.Bindings.IndexOf(val);
                            if (index > -1) SetBinding(command, index, InputKey.None);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"Failed to parse keybind line: {line}");
                }
            }
        }
        dirty = false;
        loaded = true;
    }

    protected override bool PerformSave()
    {
        if (!dirty || !loaded) return true;
        dirty = false;
        try
        {
            using (var stream = Storage.GetStream(FILENAME, FileAccess.Write, FileMode.Create))
            using (var w = new StreamWriter(stream))
            {
                foreach (var p in Overrides)
                {
                    w.WriteLine($"{p.Command.ToString()}{(p.Add ? "+" : "-")}={p.Key.ToString()}");
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error saving keybinds");
            return false;
        }
        return true;
    }

    public KeybindConfigManager(Storage storage)
    {
        this.Storage = storage;
        Load();
    }
}

