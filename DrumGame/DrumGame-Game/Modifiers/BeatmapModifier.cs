using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DrumGame.Game.Beatmaps;
using osu.Framework.Logging;

namespace DrumGame.Game.Modifiers;

// I think this could be help in some cases, but I haven't needed it yet:
// public abstract class BeatmapModifier<T> where T : BeatmapModifier<T>
// {
//     public static T Instance;
// }
public abstract class BeatmapModifier
{
    // we can't lazy initialize this anymore because it's used in CommandList
    // we have to hard code the array since I don't like the reflected order
    public readonly static BeatmapModifier[] Modifiers = [
        new DoubleBassModifier(),
        new HiddenModifier(),
        new SinglePedalModifier(),
        new AutoplayModifier(),
        new JudgementHiderModifier(),
    ];
    public static BeatmapModifier Get(string key) => Modifiers.FirstOrDefault(e => e.Key == key);
    public static T Instance<T>() where T : BeatmapModifier => Modifiers.OfType<T>().First();
    public static string Serialize(List<BeatmapModifier> modifiers)
    {
        if (modifiers == null || modifiers.Count == 0) return null;
        return string.Join(';', modifiers.Select(e => e.Serialize()));
    }
    public static List<BeatmapModifier> ParseModifiers(string modifiers)
    {
        if (modifiers == null) return null;
        var spl = modifiers.Split(";");
        if (spl.Length == 0) return null;
        var res = new List<BeatmapModifier>();
        foreach (var s in spl)
        {
            var r = Parse(s);
            if (r != null)
                res.Add(r);
        }
        return res;
    }
    public static BeatmapModifier Parse(string modifier)
    {
        var spl = modifier.Split(' ', 2);
        var abbr = spl[0];
        foreach (var e in Modifiers)
        {
            if (e.Abbreviation == abbr)
            {
                if (spl.Length == 1) return e;
                else return e.NewModifierFromData(spl[1]);
            }
        }
        // don't throw error for backwards compatible
        // ie. replay in new version sent to an older version
        Logger.Log($"No modifier found with abbreviation: {abbr}", level: LogLevel.Error);
        return null;
    }
    public virtual BeatmapModifier NewModifierFromData(string data) => this;
    public abstract string Abbreviation { get; }
    public virtual string AbbreviationMarkup => Abbreviation;
    public string Key => Abbreviation;
    public virtual string MarkupDisplay => FullName;
    public abstract string MarkupDescription { get; }
    public abstract string FullName { get; }
    public abstract bool AllowSaving { get; }

    public void Modify(BeatmapPlayer player)
    {
        player.Beatmap.DisableSaving |= !AllowSaving;
        ModifyInternal(player);
    }

    protected abstract void ModifyInternal(BeatmapPlayer player);

    // make sure to not use `;` at all
    protected virtual string SerializeData() { return null; }

    public string Serialize()
    {
        var data = SerializeData();
        if (data == null) return Abbreviation;
        return Abbreviation + " " + data;
    }
    public override string ToString() => Serialize();
}