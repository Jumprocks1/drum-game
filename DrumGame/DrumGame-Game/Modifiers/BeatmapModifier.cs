using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Utils;
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
    // the instances in this array should be thought of as the "loaded" modifiers
    // configuration changes can be made to these and stored in the settings file
    // new instances for these modifiers are created for replay data (since the replays may have different configurations)
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
    // this also includes unselected modifiers whose values are not the default
    // note, the instances in activeModifiers MUST match the instances from the static Modifiers array for this to work
    // if they don't match, it will assume all are inactive
    public static string SerializeAllModifiers(List<BeatmapModifier> activeModifiers)
    {
        var first = true;
        var o = new StringBuilder();
        foreach (var modifier in Modifiers)
        {
            var active = activeModifiers?.Contains(modifier) ?? false;
            if (active || (modifier.CanConfigure && !modifier.IsDefault))
            {
                if (!first) o.Append(';');
                if (!active) o.Append('!');
                o.Append(modifier.Serialize());
                first = false;
            }
        }
        return o.ToString();
    }
    public static List<BeatmapModifier> ParseModifiers(string modifiers, bool createNewInstances)
    {
        if (string.IsNullOrWhiteSpace(modifiers)) return null;
        var spl = modifiers.Split(";");
        if (spl.Length == 0) return null;
        var res = new List<BeatmapModifier>();
        foreach (var s in spl)
        {
            var active = !s.StartsWith('!');
            if (createNewInstances && !active) continue;
            var r = Parse(active ? s : s[1..], createNewInstances);
            if (r != null && active)
                res.Add(r);
        }
        return res;
    }
    public static BeatmapModifier Parse(string modifier, bool createNewInstance)
    {
        var spl = modifier.Split(' ', 2);
        var abbr = spl[0];
        foreach (var e in Modifiers)
        {
            if (e.Abbreviation == abbr)
            {
                if (spl.Length == 1) return e;
                if (createNewInstance)
                    return e.ModifierFromData(spl[1]);
                e.ApplyDataSafe(spl[1]);
                return e;
            }
        }
        // don't throw error for backwards compatible
        // ie. replay in new version sent to an older version
        Logger.Log($"No modifier found with abbreviation: {abbr}", level: LogLevel.Error);
        return null;
    }
    public BeatmapModifier ModifierFromData(string data)
    {
        var instance = (BeatmapModifier)Activator.CreateInstance(GetType());
        instance.ApplyDataSafe(data);
        return instance;
    }
    // it's relatively important to wrap this with a try since an error here will likely prevent the game from booting
    void ApplyDataSafe(string data)
    {
        try
        {
            ApplyData(data);
        }
        catch (Exception e) { Logger.Error(e, $"Error applying {FullName} modifier data: {data}"); }
    }
    public virtual void ApplyData(string data) { }
    public abstract string Abbreviation { get; }
    public virtual string AbbreviationMarkup => Abbreviation;
    public string Key => Abbreviation;
    // shown when hovering a replay or hovering the "Mods" button. Can include config if desired
    public virtual string MarkupDisplay => FullName;
    public abstract string MarkupDescription { get; }
    public abstract string FullName { get; } // don't put any config in this
    public abstract bool AllowSaving { get; }

    public void Modify(BeatmapPlayer player)
    {
        player.Beatmap.DisableSaving |= !AllowSaving;
        ModifyInternal(player);
    }

    // override this to configure the modifier
    // recommend using Util.Palette.Request in order to configure
    // to save configuration to replays + ini config, you must also override SerializeData and ApplyData
    public virtual void Configure() { }
    bool? _canConfigure; // simple cache
    public bool CanConfigure => _canConfigure ??= GetType().GetMethod(nameof(Configure)).DeclaringType != typeof(BeatmapModifier);
    public virtual void Reset()
    {
        var t = GetType();
        var fresh = Activator.CreateInstance(t);
        // only resets fields, if you need to reset properties, override this method
        foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            field.SetValue(this, field.GetValue(fresh));
        TriggerChanged();
    }
    protected void TriggerChanged() => Util.SelectorLoader?.SelectorState.ModifierConfigured(this);
    // kind of expensive, don't call every frame
    public virtual bool IsDefault
    {
        get
        {
            var t = GetType();
            var fresh = Activator.CreateInstance(t);
            foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (!field.GetValue(this).Equals(field.GetValue(fresh))) return false;
            return true;
        }
    }

    protected abstract void ModifyInternal(BeatmapPlayer player);

    // make sure to not use `;` at all
    // try to keep these short, data gets saved to replay info in the database
    // this does not get called if CanConfigure is false or IsDefault is true
    protected virtual string SerializeData() => null;

    public string Serialize()
    {
        if (!CanConfigure || IsDefault) return Abbreviation;
        var data = SerializeData();
        if (string.IsNullOrWhiteSpace(data)) return Abbreviation;
        return Abbreviation + " " + data;
    }
    public override string ToString() => Serialize();
}