using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DrumGame.Game.Channels;
using DrumGame.Game.Interfaces;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Stores;

public class KeyboardMapping : ICanBind<KeyboardMapping>, IEnumerable<KeyValuePair<InputKey, DrumChannel>>
{
    Dictionary<InputKey, DrumChannel> _inner;
    Dictionary<InputKey, DrumChannel> Inner { get => _inner ??= LoadDefaults(); }

    bool defaultLoaded = true;

    public Dictionary<InputKey, DrumChannel> LoadDefaults()
    {
        _inner = new Dictionary<InputKey, DrumChannel>();
        _inner.Add(InputKey.J, DrumChannel.Snare);
        _inner.Add(InputKey.H, DrumChannel.Snare);
        _inner.Add(InputKey.G, DrumChannel.BassDrum);
        _inner.Add(InputKey.F, DrumChannel.BassDrum);
        _inner.Add(InputKey.T, DrumChannel.SmallTom);
        _inner.Add(InputKey.Y, DrumChannel.SmallTom);
        _inner.Add(InputKey.U, DrumChannel.MediumTom);
        _inner.Add(InputKey.I, DrumChannel.MediumTom);
        _inner.Add(InputKey.O, DrumChannel.LargeTom);
        _inner.Add(InputKey.P, DrumChannel.LargeTom);
        _inner.Add(InputKey.LShift, DrumChannel.HiHatPedal);
        _inner.Add(InputKey.D, DrumChannel.HalfOpenHiHat);
        _inner.Add(InputKey.K, DrumChannel.HalfOpenHiHat);
        _inner.Add(InputKey.S, DrumChannel.Ride);
        _inner.Add(InputKey.L, DrumChannel.Ride);
        _inner.Add(InputKey.V, DrumChannel.Crash);
        _inner.Add(InputKey.B, DrumChannel.Crash);
        _inner.Add(InputKey.N, DrumChannel.Crash);
        return _inner;
    }

    public Action Changed { get; set; }
    public static KeyboardMapping Parse(string str)
    {
        var mapping = new KeyboardMapping();
        if (string.IsNullOrWhiteSpace(str)) return mapping;
        var bindings = str.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var dict = mapping._inner ??= new(); // make sure we start with a completely empty dictionary instead of the defaults
        foreach (var binding in bindings)
        {
            var spl = binding.IndexOf("=");
            var key = Enum.Parse<InputKey>(binding[0..spl]);
            var channel = Enum.Parse<DrumChannel>(binding[(spl + 1)..]);
            dict.Add(key, channel);
        }
        mapping.defaultLoaded = false;
        return mapping;
    }
    public static KeyboardMapping Default => null;

    public DrumChannel GetChannel(InputKey key) => Inner.GetValueOrDefault(key, DrumChannel.None);
    public DrumChannel GetChannel(KeyDownEvent ev) => GetChannel((InputKey)ev.Key);
    public void Remove(InputKey key)
    {
        defaultLoaded = false;
        Inner.Remove(key);
        Changed?.Invoke();
    }

    public void Set(InputKey key, DrumChannel channel)
    {
        defaultLoaded = false;
        if (channel == DrumChannel.None)
            Inner.Remove(key);
        else
            Inner[key] = channel;
        Changed?.Invoke();
    }

    public override string ToString()
    {
        if (defaultLoaded) return null;
        return string.Join(',', Inner.Select(e => $"{e.Key}={e.Value}"));
    }

    public IEnumerator<KeyValuePair<InputKey, DrumChannel>> GetEnumerator() => Inner.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}