using System;
using DrumGame.Game.Skinning;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace DrumGame.Game.Beatmaps.Display.Notation;

public class BarJudgementDisplay : CompositeDrawable
{
    public BarJudgementDisplay(NotationJudgementInfo.BarInfo barInfo, double proportion)
    {
        var w = (float)Math.Clamp(proportion, -1, 1) * barInfo.MaxWidth;
        // make sure w / h is at least 1.62
        var ratio = Math.Max(Math.Abs(w) / barInfo.MaxHeight, barInfo.MinmumAspectRatio);
        var h = Math.Abs(w) / ratio;
        var sign = Math.Sign(w);
        var padding = h * barInfo.Padding;
        AddInternal(new Box
        {
            Colour = barInfo.BackgroundColor,
            Width = w + padding * 2 * sign,
            Height = h + padding * 2,
            X = -padding * sign,
            Origin = Anchor.CentreLeft,
        });
        AddInternal(new Box
        {
            Colour = proportion < 0 ? barInfo.EarlyColor : barInfo.LateColor,
            Width = w,
            Height = h,
            Origin = Anchor.CentreLeft,
        });
    }
}