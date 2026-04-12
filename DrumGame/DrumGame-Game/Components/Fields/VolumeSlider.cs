using System;
using DrumGame.Game.Utils;
using osu.Framework.Bindables;

namespace DrumGame.Game.Components.Fields;

public class VolumeSlider : BasicSlider<double>
{
    public static bool PreferDb => Util.ConfigManager.PreferDecibelSlider.Value;

    public VolumeSlider(BindableNumber<double> target) : base(target)
    {
        FillLeft = true;
    }

    public const double DefaultMinDb = -50;
    double MinDb => Value.MinValue > 0 ? ValueToDb(Value.MinValue) : DefaultMinDb;
    double MaxDb => ValueToDb(Value.MaxValue);

    // -Inf and MinDb both share x = 0, which is a bit unfortunate
    // I corrected this, but didn't feel the complexity/risk was worth the few pixels of adjustment
    // Changes are stashed
    protected override double XToValue(float x)
    {
        if (!PreferDb) return base.XToValue(x);
        if (x <= 0) return 0;
        var db = MinDb + x * (MaxDb - MinDb);
        return DbToValue(Math.Round(db * 5) / 5);
    }
    protected override float ValueToX(double value)
    {
        if (!PreferDb) return base.ValueToX(value);
        return ValueToDbX(value, MinDb, MaxDb);
    }

    public static float ValueToDbX(double value, double min = DefaultMinDb, double max = 0)
    {
        if (value <= 0) return 0;
        var db = ValueToDb(value);
        return (float)((db - min) / (max - min));
    }
    public static string FormatAsDb(double value, bool forceDecimal = true)
    {
        if (value == 0) return "-∞dB";
        if (forceDecimal)
        {
            if (value == 1) return "-0.0dB";
            return $"{ValueToDb(value):+0.0;-0.0}dB";
        }
        else
        {
            if (value == 1) return "-0dB";
            return $"{ValueToDb(value):+0.#;-0.#}dB";
        }
    }
    public static double ValueToDb(double value) => value == 0 ? double.NegativeInfinity : Math.Log10(value) * 20;
    public static double DbToValue(double db) => double.IsNegativeInfinity(db) ? 0 : Math.Pow(10, db / 20);

    public static double AdjustVolumeLevel(double current, bool increase)
    {
        var sign = increase ? 1 : -1;
        if (Util.ConfigManager.PreferDecibelSlider.Value)
        {
            if (increase && current == 0)
                return DbToValue(DefaultMinDb);
            var newDb = Math.Round(ValueToDb(current)) + sign;
            if (newDb < DefaultMinDb)
                return 0;
            else
                return DbToValue(newDb);
        }
        else
        {
            return (Math.Round(current / 0.02) + sign) * 0.02;
        }
    }
}