using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;

namespace DrumGame.Game.Utils;

public class BindableColor : Bindable<Colour4>
{
    public override void Parse(object input)
    {
        switch (input)
        {
            case string str:
                // Input string: (R, G, B, A) = (0.62, 0.57, 0.57, 1.00)
                var span = str.AsSpan();
                var a = str.LastIndexOf(',');
                var b = str.LastIndexOf(',', a - 1);
                var g = str.LastIndexOf(',', b - 1);
                var r = str.LastIndexOf('(', g - 1);
                Value = new Colour4(
                    float.Parse(span[(r + 1)..g]),
                    float.Parse(span[(g + 1)..b]),
                    float.Parse(span[(b + 1)..a]),
                    float.Parse(span[(a + 1)..str.LastIndexOf(')')])
                );
                break;
            default:
                base.Parse(input);
                break;
        }
    }

    public BindableColor(Colour4 defaultValue = default) : base(defaultValue) { }

    protected override Bindable<Colour4> CreateInstance() => new BindableColor();
}
