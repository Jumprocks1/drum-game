using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Channels;
using osu.Framework.Bindables;

namespace DrumGame.Game.Beatmaps.Scoring;

public class BindableChannelEquivalents : Bindable<ChannelEquivalents>
{
    public override void Parse(object input)
    {
        switch (input)
        {
            case string str:
                Value = new ChannelEquivalents(str);
                break;
            default:
                base.Parse(input);
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
            new Equiv(DrumChannel.ClosedHiHat, DrumChannel.Rim),
            new Equiv(DrumChannel.Ride, DrumChannel.RideBell),
            new Equiv(DrumChannel.RideBell, DrumChannel.Ride),
            new Equiv(DrumChannel.HalfOpenHiHat, DrumChannel.ClosedHiHat),
            new Equiv(DrumChannel.ClosedHiHat, DrumChannel.HalfOpenHiHat),
            new Equiv(DrumChannel.HalfOpenHiHat, DrumChannel.OpenHiHat),
            new Equiv(DrumChannel.OpenHiHat, DrumChannel.HalfOpenHiHat),
        });

    // if we wanted performances, we could do a boolean 2D array
    const string sep = "->";

    public bool AllowTrigger(DrumChannel input, DrumChannel map) => Map?.Contains(new Equiv(input, map)) ?? false;

    public void ResetToDefault() => Map = Default.Map;
    public void Replace(DrumChannel input1, DrumChannel map1, DrumChannel input2, DrumChannel map2)
    {
        Map.Remove(new Equiv(input1, map1));
        Map.Add(new Equiv(input2, map2));
    }
    public void Add(DrumChannel input, DrumChannel map) => Map.Add(new Equiv(input, map));

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