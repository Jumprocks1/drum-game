using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DrumGame.Game.Channels;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Commands;

public class CommandInfo
{
    string[] _searchTerms;
    public string[] SearchTerms => _searchTerms == null ? _searchTerms = ComputeSearchTerms() : _searchTerms;
    string[] ComputeSearchTerms()
    {
        // var s = Name.ToLower().Split(" ", StringSplitOptions.RemoveEmptyEntries);
        return new[] { Name.ToLower() };
    }
    public bool MatchesSearch(string[] search)
    {
        var s = SearchTerms[0]; // eventually we can use a better way of searching
        return search.All(e => s.Contains(e));
    }
    public bool HasHandlers => Handlers != null && Handlers.Count > 0;
    // handlers will only exist for DefaultCommandInfo, since parameter CommandInfos get passed to the default
    internal List<object> Handlers;
    public Command Command;
    public readonly string Name;
    public string HelperMarkup;
    public List<KeyCombo> Bindings = new();
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
            o.Append("{");
            for (int i = 0; i < Parameters.Length; i++)
            {
                o.Append(Parameters[i]);
                if (i != Parameters.Length - 1) o.Append(",");
            }
            o.Append("}");
            return o.ToString();
        }
        else
        {
            return $"{Command}";
        }
    }
}
