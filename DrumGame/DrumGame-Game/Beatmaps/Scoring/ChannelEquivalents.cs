using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Channels;
using osu.Framework.Bindables;

namespace DrumGame.Game.Beatmaps.Scoring;

public class BindableChannelEquivalents : Bindable<ChannelEquivalents>
{
    public override void Parse(object input, IFormatProvider provider)
    {
        switch (input)
        {
            case string str:
                Value = new ChannelEquivalents(str);
                break;
            default:
                base.Parse(input, provider);
                break;
        }
    }
    public BindableChannelEquivalents(ChannelEquivalents defaultValue = default) : base(defaultValue) { }
    protected override Bindable<ChannelEquivalents> CreateInstance() => new BindableChannelEquivalents();
}


public class ChannelEquivalents : IEnumerable<(DrumChannel Input, DrumChannel Map)>
{
    record struct Equiv(DrumChannel Input, DrumChannel Map);
    public static ChannelEquivalents Default
        => new ChannelEquivalents(new HashSet<Equiv>
        {
            new Equiv(DrumChannel.Snare, DrumChannel.SideStick),
            new Equiv(DrumChannel.Crash, DrumChannel.China),
            new Equiv(DrumChannel.Crash, DrumChannel.Splash),
            new Equiv(DrumChannel.Crash, DrumChannel.RideCrash),
            new Equiv(DrumChannel.ClosedHiHat, DrumChannel.Rim),
            new Equiv(DrumChannel.Ride, DrumChannel.RideBell),
            new Equiv(DrumChannel.RideBell, DrumChannel.Ride),
            new Equiv(DrumChannel.RideCrash, DrumChannel.Ride),
            new Equiv(DrumChannel.RideCrash, DrumChannel.Crash),
            new Equiv(DrumChannel.RideCrash, DrumChannel.China),
            new Equiv(DrumChannel.HalfOpenHiHat, DrumChannel.ClosedHiHat),
            new Equiv(DrumChannel.ClosedHiHat, DrumChannel.HalfOpenHiHat),
            new Equiv(DrumChannel.HalfOpenHiHat, DrumChannel.OpenHiHat),
            new Equiv(DrumChannel.OpenHiHat, DrumChannel.HalfOpenHiHat),
        });

    // if we wanted performances, we could do a boolean 2D array
    const string sep = "->";

    public bool AllowTrigger(DrumChannel input, DrumChannel map) => Map?.Contains(new Equiv(input, map)) ?? false;

    public void AddDefaults(DrumChannel? channel)
    {
        if (channel is DrumChannel c)
            foreach (var e in Default.Map.Where(e => e.Input == c))
                Map.Add(e);
        else
            foreach (var e in Default.Map)
                Map.Add(e);
    }
    public void ResetToDefault() => Map = Default.Map;
    public void ResetToDefault(DrumChannel inputChannel)
    {
        Map.RemoveWhere(e => e.Input == inputChannel);
        foreach (var e in Default.Map)
            if (e.Input == inputChannel)
                Map.Add(e);
    }
    public void Replace(DrumChannel input1, DrumChannel map1, DrumChannel input2, DrumChannel map2)
    {
        Map.Remove(new Equiv(input1, map1));
        Map.Add(new Equiv(input2, map2));
    }
    public void Add(DrumChannel input, DrumChannel map) => Map.Add(new Equiv(input, map));
    public void Remove(DrumChannel input, DrumChannel map) => Map.Remove(new Equiv(input, map));

    // excludes input
    public IEnumerable<DrumChannel> EquivalentsFor(DrumChannel input) => Map.Where(e => e.Input == input).Select(e => e.Map);
    HashSet<Equiv> Map;
    public ChannelEquivalents(string config)
    {
        Map = new();
        foreach (var rule in config.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var spl = rule.Split(sep, 2);
            Map.Add(new Equiv(Enum.Parse<DrumChannel>(spl[0]), Enum.Parse<DrumChannel>(spl[1])));
        }
    }
    public static implicit operator ChannelEquivalents(string config) => new ChannelEquivalents(config);
    ChannelEquivalents(HashSet<Equiv> map) { Map = map; }
    public override string ToString() => Map == null ? "" : string.Join(',', Map.Select(e => e.Input + sep + e.Map));


    IEnumerator<(DrumChannel, DrumChannel)> GetEnumerator()
        => Map.OrderBy(e => e.Input).Select(e => (e.Input, e.Map)).GetEnumerator();


    IEnumerator<(DrumChannel, DrumChannel)> IEnumerable<(DrumChannel Input, DrumChannel Map)>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}