using System;
using DrumGame.Game.Interfaces;
using osu.Framework.Bindables;

namespace DrumGame.Game.Stores;

public class ParsableBindable<T> : Bindable<T> where T : ICanBind<T>
{
    public override void Parse(object input, IFormatProvider provider)
    {
        switch (input)
        {
            case string str:
                if (Value != null)
                    Value.Changed = null;
                Value = T.Parse(str);
                Value.Changed = TriggerChange;
                break;
            default:
                base.Parse(input, provider);
                break;
        }
    }
    public ParsableBindable(T defaultValue = default) : base(defaultValue)
    {
        if (Value != null)
            Value.Changed = TriggerChange;
    }

    protected override Bindable<T> CreateInstance() => new ParsableBindable<T>(T.Default);
}