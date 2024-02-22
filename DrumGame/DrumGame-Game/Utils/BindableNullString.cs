using System;
using osu.Framework.Bindables;

namespace DrumGame.Game.Utils;

// this bindable is basically the same as a Bindable<string> except it prefers setting it's value to null when parsing instead of empty
public class BindableNullString : Bindable<string>
{
    public override void Parse(object input, IFormatProvider provider)
    {
        switch (input)
        {
            case string str:
                Value = string.IsNullOrWhiteSpace(str) ? null : str;
                break;
            default:
                base.Parse(input, provider);
                break;
        }
    }
    public BindableNullString(string defaultValue = default) : base(defaultValue) { }
    protected override Bindable<string> CreateInstance() => new BindableNullString();
}
