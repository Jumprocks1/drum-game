using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Channels;
using osu.Framework.Bindables;

namespace DrumGame.Game.Input;

public class BindableMidiMapping : Bindable<MidiMapping>
{
    public override void Parse(object input, IFormatProvider provider)
    {
        switch (input)
        {
            case string str:
                Value = new MidiMapping(str);
                break;
            default:
                base.Parse(input, provider);
                break;
        }
    }
    public BindableMidiMapping(MidiMapping defaultValue = default) : base(defaultValue) { }
    protected override Bindable<MidiMapping> CreateInstance() => new BindableMidiMapping();
}

public class MidiMapping : IEnumerable<(byte InputNote, DrumChannel Map)>
{
    public static MidiMapping Default => null;
    const string sep = "->";
    public void Clear() { Map?.Clear(); Order?.Clear(); }
    public void Replace(byte input1, byte input2, DrumChannel map2)
    {
        LoadOrder();
        Map.Remove(input1);
        if (input1 != input2 && Map.ContainsKey(input2))
        {
            Order.Remove(input1);
            return;
        }
        var i = Order.IndexOf(input1);
        if (i >= 0) Order[i] = input2;
        Map[input2] = map2;
    }
    public void Add(byte input, DrumChannel map)
    {
        LoadOrder();
        Map[input] = map;
        if (!Order.Contains(input)) Order.Add(input);
    }
    public void Remove(byte value)
    {
        Order?.Remove(value);
        Map.Remove(value);
    }

    void LoadOrder()
    {
        Order ??= Map.Select(e => e.Key).OrderBy(e => e).ToList();
    }
    Dictionary<byte, DrumChannel> Map = new();
    // temporarily overrides the default Map ordering
    List<byte> Order;
    public int Count => Map.Count;
    public bool HasMappingOverride(byte note) => Map.ContainsKey(note);
    public DrumChannel MapNote(byte note) => Map.TryGetValue(note, out var o) ? o : DrumChannel.None;
    public MidiMapping(string config)
    {
        if (string.IsNullOrWhiteSpace(config)) return;
        foreach (var rule in config.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var spl = rule.Split(sep, 2);
            Map.Add(byte.Parse(spl[0]), Enum.Parse<DrumChannel>(spl[1]));
        }
    }
    public override string ToString() => Map == null ? "" : string.Join(',', Map.Select(e => e.Key + sep + e.Value));

    IEnumerator<(byte, DrumChannel)> GetEnumerator()
    {
        if (Order != null) return Order.Select(e => (e, Map[e])).GetEnumerator();
        else return Map.OrderBy(e => e.Key).Select(e => (e.Key, e.Value)).GetEnumerator();
    }

    IEnumerator<(byte, DrumChannel)> IEnumerable<(byte InputNote, DrumChannel Map)>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}