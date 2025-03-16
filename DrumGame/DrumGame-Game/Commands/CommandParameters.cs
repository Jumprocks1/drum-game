using System;
using System.Globalization;
using System.Linq;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Channels;
using DrumGame.Game.Modifiers;

namespace DrumGame.Game.Commands;

public static class CommandParameters
{
    public static object Parse(string value, Type t)
    {
        if (t.IsEnum) return Enum.Parse(t, value);
        if (t == typeof(string)) return string.IsNullOrWhiteSpace(value) ? null : value;
        t = Nullable.GetUnderlyingType(t) ?? t;
        if (t == typeof(BeatmapModifier)) // this should probably be generic/static interface, oh well
            return BeatmapModifier.Get(value);
        return Convert.ChangeType(value, t, CultureInfo.InvariantCulture);
    }
    public static object ParseOrDefault(string value, Type t)
        => TryParse(value, t, out var o) ? o : default;
    public static bool TryParse(string value, Type t, out object o)
    {
        try
        {
            o = Parse(value, t);
            return true;
        }
        catch
        {
            o = default;
            return false;
        }
    }
    public static object[] Parse(string paramString, Type[] types) =>
        paramString.Split(',').Select((e, i) => Parse(e, types[i])).ToArray();

    public static string TypeString(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(double)) return "number";
        else if (type == typeof(DrumChannel)) return "channel";
        else if (type == typeof(NotePreset)) return "preset";
        else if (type == typeof(string)) return "string";
        else if (type.IsAssignableTo(typeof(BeatmapModifier))) return "modifier";
        else if (type.IsEnum) return type.Name;
        throw new NotImplementedException();
    }
}