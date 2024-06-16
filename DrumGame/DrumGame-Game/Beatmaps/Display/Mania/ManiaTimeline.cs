using System;
using System.Linq.Expressions;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Skinning;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK.Input;

namespace DrumGame.Game.Beatmaps.Display.Mania;

public class ManiaTimeline : AdjustableSkinElement, IHasMarkupTooltip
{
    public class PositionData : AdjustableSkinData
    {
        public Colour4 AfterColor = new(0.5f, 0.5f, 0.5f, 0.2f);
        public Colour4 BeforeColor = new(0.5f, 0.5f, 0.5f, 0.5f);
        public Colour4 CursorColor = DrumColors.Yellow;
    }
    public override Expression<Func<Skin, AdjustableSkinData>> SkinPathExpression => e => e.Mania.PositionIndicator;
    public new PositionData SkinData => (PositionData)base.SkinData;

    public override PositionData DefaultData() => new()
    {
        AnchorTarget = SkinAnchorTarget.LaneContainer,
        Origin = Anchor.TopLeft,
        Anchor = Anchor.TopRight,
        RelativeSizeAxes = Axes.Y,
        Height = 1,
        Width = 20,
    };

    Box Before;
    Box After;
    Box Cursor;

    ManiaBeatmapDisplay Display;
    TrackClock Track => Display.Track;

    public ManiaTimeline(ManiaBeatmapDisplay display)
    {
        Display = display;
        AddInternal(Before = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.BottomLeft,
            Colour = SkinData.BeforeColor
        });
        AddInternal(After = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = SkinData.AfterColor
        });
        AddInternal(Cursor = new Box
        {
            RelativeSizeAxes = Axes.X,
            RelativePositionAxes = Axes.Y,
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.CentreLeft,
            Height = 4,
            Colour = SkinData.CursorColor
        });
    }

    public void SetPercent(float percent)
    {
        Cursor.Y = -(float)percent;
        Before.Height = (float)percent;
        After.Height = 1 - (float)percent;
    }

    protected override void Update()
    {
        if (!IsDragged) SetPercent((float)Track.Percent);
        base.Update();
    }


    protected override bool OnMouseDown(MouseDownEvent e)
    {
        SetScrubPosition(e);
        return true;
    }
    protected override void OnMouseUp(MouseUpEvent e) => Track.CommitAsyncSeek();
    protected override bool OnDragStart(DragStartEvent e)
    {
        if (e.Button == MouseButton.Left) return true;
        return base.OnDragStart(e);
    }
    private void SetScrubPosition(MouseEvent e)
    {
        var t = Math.Clamp(1 - e.MousePosition.Y / DrawHeight, 0, 1);
        SetPercent(t);
        Track.Seek(t * (Track.EndTime + Track.LeadIn) - Track.LeadIn, true);
    }

    public string MarkupTooltip
    {
        get
        {
            var y = Math.Clamp(1 - ToLocalSpace(Util.Mouse.Position).Y / DrawHeight, 0, 1);
            var time = y * (Track.EndTime + Track.LeadIn) - Track.LeadIn;
            var beat = Display.Beatmap.BeatFromMilliseconds(time);
            return $"<brightGreen>Time:</c> {Util.FormatTime(time)}\n<brightGreen>Beat:</c> {beat:0}\n\nDrag to seek";
        }
    }
    protected override void OnDrag(DragEvent e)
    {
        if (IsDisposed) return;
        SetScrubPosition(e);
        base.OnDrag(e);
    }
    protected override void OnDragEnd(DragEndEvent e)
    {
        Track.CommitAsyncSeek();
        base.OnDragEnd(e);
    }
}