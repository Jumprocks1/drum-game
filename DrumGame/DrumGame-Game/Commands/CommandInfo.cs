using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DrumGame.Game.Channels;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Commands;

public class CommandInfo
{
    string _filterString;
    public string FilterString => _filterString ??= MakeFilterString();
    string MakeFilterString() // this should only ever be called once per CommandInfo
    {
        if (SearchTags == null)
        {
            SearchTags = Util.CommandController[Command].SearchTags;
            return Name.ToLower();
        }
        return $"{Name.ToLower()} {SearchTags}";
    }
    public bool MatchesSearch(string[] search) => search.All(FilterString.Contains);
    public bool HasHandlers => Handlers != null && Handlers.Count > 0;
    // handlers will only exist for DefaultCommandInfo, since parameter CommandInfos get passed to the default
    internal List<object> Handlers;
    public Command Command;
    public readonly string Name;
    // These should be lowercase (or they won't work). Spaces can safely be used as they are split when matching
    public string SearchTags;
    public string HelperMarkup { get => HelperMarkupAction?.Invoke(); set => HelperMarkupAction = () => value; }
    public Func<string> HelperMarkupAction;
    public readonly List<KeyCombo> Bindings = new();
    public CommandInfo(Command command, string name)
    {
        Command = command;
        Name = name;
    }
    public object[] Parameters;
    public object Parameter { set => Parameters = new object[] { value }; get => Parameters[0]; }
    public CommandInfo WithParameter(object parameter) // adds a parameter and returns a new command info 
    {
        object[] p;
        if (Parameters == null)
        {
            p = new object[] { parameter };
        }
        else
        {
            p = new object[Parameters.Length + 1];
            Parameters.CopyTo(p, 0);
            p[^1] = parameter;
        }
        return new CommandInfo(Command, Name) { Parameters = p };
    }
    public bool ParametersEqual(object[] other)
    {
        if (other == null) return Parameters == null;
        if (Parameters == null) return other == null;
        if (other.Length != Parameters.Length) return false;
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (Parameters[i] == null ? Parameters[i] != other[i] :
                !Parameters[i].Equals(other[i])) return false;
        }
        return true;
    }
    public bool TryGetParameter<T>(int i, out T value)
    {
        if (Parameters != null && Parameters.Length > i)
        {
            if (Parameters[i] is T t)
            {
                value = t;
                return true;
            }
            else if (Parameters[i] is string s && CommandParameters.TryParse(s, typeof(T), out var o))
            {
                value = (T)o;
                return true;
            }
        }
        value = default;
        return false;
    }
    public CommandInfo(Command command, string name, params KeyCombo[] keys)
    {
        this.Command = command;
        Name = name;
        Bindings.AddRange(keys);
    }
    public static CommandInfo From(Command command, DrumChannel channel, params object[] parameters)
    {
        var res = From(command, parameters);
        res.Bindings.Add(channel);
        return res;
    }
    public static CommandInfo From(Command command, params object[] parameters)
    {
        var getName = Util.CommandController.ParameterInfo[(int)command]?.GetName;
        var name = getName != null ? getName(command, parameters) : $"{command.ToString().FromPascalCase()} {{{string.Join(",", parameters)}}}";
        return new CommandInfo(command, name) { Parameters = parameters };
    }
    public override string ToString()
    {
        if (Parameters != null && Parameters.Length > 0)
        {
            var o = new StringBuilder(Command.ToString());
            o.Append('{');
            for (var i = 0; i < Parameters.Length; i++)
            {
                o.Append(Parameters[i]);
                if (i != Parameters.Length - 1) o.Append(',');
            }
            o.Append('}');
            return o.ToString();
        }
        else
        {
            return $"{Command}";
        }
    }

    public static CommandInfo FromString(string s)
    {
        var paramIndex = s.IndexOf('{');
        if (paramIndex == -1)
            return Util.CommandController[Enum.Parse<Command>(s)];

        var enumString = s[..paramIndex];
        var commandE = Enum.Parse<Command>(enumString);
        var paramString = s[(paramIndex + 1)..s.IndexOf('}')];
        var parameters = CommandParameters.Parse(paramString, Util.CommandController.ParameterInfo[(int)commandE]?.Types);

        var parameterCommands = Util.CommandController.ParameterCommands[(int)commandE];
        if (parameterCommands != null)
        {
            foreach (var c in parameterCommands)
            {
                if (c.ParametersEqual(parameters))
                    return c;
            }
        }

        return From(commandE, parameters);

    }
}
