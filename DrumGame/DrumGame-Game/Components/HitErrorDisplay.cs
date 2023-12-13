using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace DrumGame.Game.Components;

public class HitErrorDisplay : CompositeDrawable, IHasMarkupTooltip
{
    const float BackgroundHeight = 0.25f;
    const float TickHeight = 0.6f; // height of hit ticks, average will have height 1
    const float TickWidth = 2; // absolute units
    const float AverageTickWidth = 3;
    const float AnimationDuration = 150; // how long for average bar to move
    static Colour4 TickColour => Util.Skin.Notation.NotationColor;

    const int MaxTickCount = 20; // how many ticks will be shown (and included in the average)

    // this functions as a queue
    (float Error, Box Box)[] HitErrors = new (float Error, Box Tick)[MaxTickCount];
    int TickCount = 0;
    int HitErrorHead = 0; // where the next box will be inserted at

    Box AverageTick;
    public readonly HitWindows Windows;
    public void Clear()
    {
        for (int i = 0; i < TickCount; i++) HitErrors[i].Box.Alpha = 0;
        AverageTick.MoveToX(0f, AnimationDuration);
        TickCount = 0;
        HitErrorHead = 0;
    }
    public void AddTick(float hitError)
    {
        Box box;
        if (TickCount == 20)
        {
            box = HitErrors[HitErrorHead].Box;
            TickCount -= 1;
        }
        else
        {
            AddInternal(box = new Box
            {
                Width = TickWidth,
                Height = TickHeight,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Y,
                RelativePositionAxes = Axes.Both,
                Colour = TickColour,
                Alpha = 0.5f
            });
        }
        box.X = hitError / Windows.HitWindow * 0.5f;
        HitErrors[HitErrorHead] = (hitError, box);
        TickCount += 1;

        var total = 0f;
        var weight = 0f;
        for (int i = 0; i < TickCount; i++)
        {
            var age = HitErrorHead - i;
            if (age < 0) age += MaxTickCount;
            var w = (float)(MaxTickCount - age) / MaxTickCount;
            HitErrors[i].Box.Alpha = w * 0.75f;
            weight += w;
            total += HitErrors[i].Error * w;
        }
        HitErrorHead = HitErrorHead == MaxTickCount - 1 ? 0 : HitErrorHead + 1;

        var avg = total / weight;
        AverageTick.MoveToX(avg / Windows.HitWindow * 0.5f, AnimationDuration);
    }
    public void DrawBox(Colour4 colour, float width, bool right)
    {
        var anchor = right ? Anchor.CentreLeft : Anchor.CentreRight;
        AddInternal(new Box
        {
            Width = width,
            Height = BackgroundHeight,
            Anchor = Anchor.Centre,
            Origin = anchor,
            RelativeSizeAxes = Axes.Both,
            RelativePositionAxes = Axes.Both,
            Colour = colour,
        });
    }
    public HitErrorDisplay(HitWindows windows)
    {
        Windows = windows;
        var total = Windows.HitWindow * 2;
        DrawBox(Util.HitColors.EarlyMiss, Windows.HitWindow / total, false);
        DrawBox(Util.HitColors.LateMiss, Windows.HitWindow / total, true);
        DrawBox(Util.HitColors.EarlyBad, Windows.BadWindow / total, false);
        DrawBox(Util.HitColors.LateBad, Windows.BadWindow / total, true);
        DrawBox(Util.HitColors.EarlyGood, Windows.GoodWindow / total, false);
        DrawBox(Util.HitColors.LateGood, Windows.GoodWindow / total, true);
        DrawBox(Util.HitColors.EarlyPerfect, Windows.PerfectWindow / total, false);
        DrawBox(Util.HitColors.LatePerfect, Windows.PerfectWindow / total, true);
        AddInternal(AverageTick = new Box
        {
            Width = AverageTickWidth,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.Y,
            RelativePositionAxes = Axes.Both,
            Colour = TickColour,
            Depth = -10
        });
    }

    public string MarkupTooltip
    {
        get
        {
            var r = $"{MarkupText.Color("Pefect", Util.HitColors.Perfect)}: ±{Windows.PerfectWindow}ms";
            r += $"\n{MarkupText.Color("Good", Util.HitColors.Good)}: ±{Windows.GoodWindow}ms";
            r += $"\n{MarkupText.Color("Bad", Util.HitColors.Bad)}: ±{Windows.BadWindow}ms";
            r += $"\n{MarkupText.Color("Miss", Util.HitColors.Miss)}: ±{Windows.HitWindow}ms";
            return r;
        }
    }
}
