using System;
using System.Linq.Expressions;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Beatmaps.Display.Mania;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Skinning;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;

namespace DrumGame.Game.Components;

// Don't really like how I handled the different layouts for this :(
// this whole file is a bit messy
public class HitErrorDisplay : AdjustableSkinElement, IHasMarkupTooltip
{
    public override void LayoutChanged()
    {
        ClearInternal(true);
        HitErrors = new (float Error, Box Tick)[MaxTickCount];
        TickCount = 0;
        HitErrorHead = 0;
        Generate();
    }
    public Axes SecondaryAxis => Layout == ElementLayout.Vertical ? Axes.X : Axes.Y;
    public override ElementLayout[] AvailableLayouts => [ElementLayout.Vertical];

    const float BackgroundHeight = 0.25f;
    const float TickHeight = 0.6f; // height of hit ticks, average will have height 1
    const float TickWidth = 2; // absolute units
    const float AverageTickWidth = 3;
    const float AnimationDuration = 150; // how long for average bar to move

    Colour4 TickColour => Mania ? new(244, 244, 244, 255) : Util.Skin.Notation.NotationColor;

    const int MaxTickCount = 20; // how many ticks will be shown (and included in the average)

    // this functions as a queue
    (float Error, Box Box)[] HitErrors = new (float Error, Box Tick)[MaxTickCount];
    int TickCount = 0;
    int HitErrorHead = 0; // where the next box will be inserted at

    Box AverageTick;
    public HitWindows Windows => Display.Scorer?.HitWindows;
    public void Clear()
    {
        for (var i = 0; i < TickCount; i++)
        {
            RemoveInternal(HitErrors[i].Box, true);
            HitErrors[i] = default;
        }

        if (Vertical) AverageTick.MoveToX(0f, AnimationDuration);
        else AverageTick.MoveToY(0f, AnimationDuration);

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
                Width = Vertical ? TickHeight : TickWidth,
                Height = Vertical ? TickWidth : TickHeight,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = SecondaryAxis,
                RelativePositionAxes = Axes.Both,
                Colour = TickColour,
                Alpha = 0.5f
            });
        }
        if (Vertical)
            box.Y = -hitError / Windows.HitWindow * 0.5f;
        else
            box.X = hitError / Windows.HitWindow * 0.5f;
        HitErrors[HitErrorHead] = (hitError, box);
        TickCount += 1;

        var total = 0f;
        var weight = 0f;
        for (var i = 0; i < TickCount; i++)
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
        if (Vertical)
            AverageTick.MoveToY(-avg / Windows.HitWindow * 0.5f, AnimationDuration);
        else
            AverageTick.MoveToX(avg / Windows.HitWindow * 0.5f, AnimationDuration);
    }
    public void DrawBox(Colour4 colour, float width, bool right)
    {
        var anchor = Vertical ?
            (right ? Anchor.BottomCentre : Anchor.TopCentre) :
            (right ? Anchor.CentreLeft : Anchor.CentreRight);
        AddInternal(new Box
        {
            Width = Vertical ? BackgroundHeight : width,
            Height = Vertical ? width : BackgroundHeight,
            Anchor = Anchor.Centre,
            Origin = anchor,
            RelativeSizeAxes = Axes.Both,
            RelativePositionAxes = Axes.Both,
            Colour = colour,
        });
    }


    public override AdjustableSkinData DefaultData() => Mania ? new()
    {
        Width = 40,
        Height = 200,
        Anchor = Anchor.BottomRight,
        Origin = Anchor.BottomLeft,
        AnchorTarget = SkinAnchorTarget.PositionIndicator,
        Layout = ElementLayout.Vertical
    } : new()
    {
        Width = 200,
        Anchor = Anchor.TopCentre,
        Height = 40
    };
    public override Expression<Func<Skin, AdjustableSkinData>> SkinPathExpression =>
        Mania ? e => e.Mania.HitErrorDisplay : e => e.Notation.HitErrorDisplay;
    bool Mania;
    void HandleSeek(double _) => Clear();
    BeatmapDisplay Display;
    bool Vertical => Layout == ElementLayout.Vertical;
    public HitErrorDisplay(BeatmapDisplay display) : base(true)
    {
        Display = display;
        Mania = display is ManiaBeatmapDisplay;
        InitializeSkinData();
        Display.Track.OnSeekCommit += HandleSeek;
        Display.Scorer.OnScoreEvent += HandleScoreEvent;
        Generate();
    }

    public void Generate()
    {
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
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativePositionAxes = Axes.Both,
            Colour = TickColour,
            Depth = -10,
            RelativeSizeAxes = SecondaryAxis
        });
        if (Vertical) AverageTick.Height = AverageTickWidth;
        else AverageTick.Width = AverageTickWidth;
    }

    protected override void Dispose(bool isDisposing)
    {
        Display.Scorer.OnScoreEvent -= HandleScoreEvent;
        Display.Track.OnSeekCommit -= HandleSeek;
        base.Dispose(isDisposing);
    }

    void HandleScoreEvent(ScoreEvent e)
    {
        if (!e.Ignored)
        {
            if (e.HitError.HasValue) // this filters out rolls
                AddTick((float)e.HitError.Value);
        }
    }
    public string MarkupTooltip => Windows?.MarkupTooltip;
}
