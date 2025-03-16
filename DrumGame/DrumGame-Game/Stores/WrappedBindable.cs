using System;
using DrumGame.Game.Interfaces;
using osu.Framework.Bindables;

namespace DrumGame.Game.Stores;

public static class WrappedBindable
{
    public static WrappedBindable<T, K> C<T, K>(Bindable<K> target, Func<K, T> from, Func<T, K> to) => new(target, from, to);
}

// Warning, make sure From/To have consistent references so they don't cause infinite conversion cycles
public class WrappedBindable<T, K> : Bindable<T>
{
    Func<K, T> From;
    Func<T, K> To;
    Bindable<K> Target;
    public WrappedBindable(Bindable<K> target, Func<K, T> from, Func<T, K> to)
    {
        From = from;
        To = to;
        Target = target;

        Target.BindValueChanged(TargetChanged, true);
        ValueChanged += SelfChanged;
    }

    void SelfChanged(ValueChangedEvent<T> e) => Target.Value = To(e.NewValue);
    void TargetChanged(ValueChangedEvent<K> e) => Value = From(e.NewValue);

    public override void UnbindEvents()
    {
        Target.ValueChanged -= TargetChanged;
        base.UnbindEvents();
    }
}
