using System;
using osu.Framework.Bindables;

namespace DrumGame.Game.Utils;

public class BindableRange : Bindable<(byte, byte)>
{
    public override void Parse(object input, IFormatProvider provider)
    {
        switch (input)
        {
            case string str:
                str = str[1..^1];
                var spl = str.Split(',', 2, StringSplitOptions.TrimEntries);
                Value = (byte.Parse(spl[0]), byte.Parse(spl[1]));
                break;
            default:
                base.Parse(input, provider);
                break;
        }
    }

    public BindableRange((byte, byte) defaultValue) : base(defaultValue) { }

    protected override BindableRange CreateInstance() => new BindableRange((0, 0));
}
