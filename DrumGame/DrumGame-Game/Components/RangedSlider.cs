using osu.Framework.Bindables;

namespace DrumGame.Game.Components;

public class RangedSlider : BasicSlider<double>
{
    public RangedSlider(BindableNumber<double> target) : base(target)
    {
    }
    protected override void UpdateThumb(double x)
    {
        base.UpdateThumb((x - Value.MinValue) / Value.MaxValue);
    }
    protected override double XToValue(float x) => x * (Value.MaxValue - Value.MinValue) + Value.MinValue;
}

