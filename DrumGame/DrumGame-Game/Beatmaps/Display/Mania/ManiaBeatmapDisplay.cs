using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Beatmaps.Display.Components;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using DrumGame.Game.Components;
using DrumGame.Game.Containers;
using DrumGame.Game.Stores.Skins;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace DrumGame.Game.Beatmaps.Display.Mania;

using LaneInfo = ManiaSkinInfo.ManiaSkinInfo_Lane;

// Benchmark:
// With legacy components
// Setup: 0.01 scroll on through the fire and flames
// I got 60 UPS and 30 FPS while playing
// It goes to about half that at 0.005 scroll

// With new canvas, I get 60 FPS and >1000 UPS on 0.005 scroll

public partial class ManiaBeatmapDisplay : BeatmapDisplay
{
    public class ManiaCanvasData
    {
        public (int Start, int End) VisibleChips;
        public (int Start, int End) VisibleLines;
        public float Offset;
    }
    void ApplyState(ManiaCanvasData data)
    {
        var time = Track.CurrentTime;
        var heightOfDisplayInMs = 1 / ScrollRate;
        var overdraw = ManiaConfig.ChipThickness * 2; // the "perfect" overdraw is ChipThickness / 2, we add extra for safety

        var startTime = time - heightOfDisplayInMs * (ManiaConfig.JudgementPosition + overdraw);
        var endTime = time + heightOfDisplayInMs * (1 - ManiaConfig.JudgementPosition + overdraw);

        data.VisibleChips = Chips.FindRangeContinuous(startTime, endTime);
        data.VisibleLines = Lines.FindRangeContinuous(startTime, endTime);
        data.Offset = ScrollingOverlay.Y;
    }

    void DrawCanvas(Canvas<ManiaCanvasData>.Node node, ManiaCanvasData data)
    {
        foreach (var lane in Lanes)
        {
            node.Color = lane.Config.BorderColor;
            node.Box(lane.X, 0, -lane.LeftBorderWidth, 1);
            node.Color = lane.BackgroundColor;
            node.Box(lane.X, 0, lane.Width, 1);
        }
        var lane1 = Lanes[0];
        node.Color = lane1.Config.BorderColor;
        node.Box(1, 0, -lane1.LeftBorderWidth, 1);

        node.Translate(new Vector2(0, node.State.Offset));

        node.Color = ManiaConfig.BeatLineColor;
        var end = node.State.VisibleLines.End;
        for (var i = node.State.VisibleLines.Start; i < end; i++)
            Lines[i].Draw(node);

        var start = data.VisibleChips.Start;
        end = data.VisibleChips.End;
        // these frick up our blending, be careful
        // we split into 2 loops to prevent swapping blend modes
        // swapping blend modes forces a flush of the draw buffer
        for (var i = start; i < end; i++)
            Chips[i].DrawAdornment(node);
        for (var i = start; i < end; i++)
            Chips[i].DrawChip(node);
    }

    public Lane[] Lanes;

    static ManiaSkinInfo ManiaConfig => Util.Skin.Mania;
    public double ScrollRate => ManiaSkinInfo.ScrollRate;
    public List<BeatLine> Lines = new();
    public class BeatLine : IComparable<double>
    {
        float Y;
        float Height;
        double HitTime;
        public void Draw(Canvas<ManiaCanvasData>.Node node) => node.Box(0, Y, 1, Height);
        public int CompareTo(double other) => HitTime.CompareTo(other);
        public BeatLine((double Time, bool Measure) line)
        {
            HitTime = line.Time;
            Height = line.Measure ? ManiaConfig.MeasureLineThickness : ManiaConfig.BeatLineThickness;
            Y = (float)(-HitTime * ManiaSkinInfo.ScrollRate) + 1 - Height / 2;
        }
    }
    public class Chip : IComparable<double>
    {
        public LaneInfo Lane;
        public IChipInfo ChipInfo;
        float X;
        float Y;
        float Width;
        float Height;
        float ChipWidth;
        public void DrawAdornment(Canvas<ManiaCanvasData>.Node node) => ChipInfo.Adornment?.Draw(node, X, Y, Width, Height);
        public void DrawChip(Canvas<ManiaCanvasData>.Node node)
        {
            if (ChipInfo.Chip == null)
            {
                node.Color = ChipInfo.Color;
                node.CenterBox(X, Y, Width * ChipWidth, Height);
            }
            else
            {
                ChipInfo.Chip?.Draw(node, X, Y, Width * ChipWidth, Height);
            }
        }
        public Chip(ManiaBeatmapDisplay display, HitObjectRealTime ho)
        {
            var lane = display.GetLane(ho.Data);
            var laneConfig = lane.Config;
            HitTime = ho.Time;
            Width = lane.Width;
            Height = ManiaConfig.ChipThickness;
            X = lane.X + Width / 2;
            Y = (float)(-HitTime * display.ScrollRate) + 1;
            ChipWidth = ho.Data.Modifiers.HasFlag(NoteModifiers.Ghost) ? 0.7f : 1f;

            var channel = ho.Channel;
            if (laneConfig.Channel != channel)
                ChipInfo = laneConfig.Secondary.FirstOrDefault(e => e.Channel == channel);
            ChipInfo ??= laneConfig;
            ChipInfo.Adornment?.PrepareForDraw();
            ChipInfo.Chip?.PrepareForDraw();
        }
        public readonly double HitTime; // in ms
        public int CompareTo(double other) => HitTime.CompareTo(other);
    }
    public class Lane
    {
        public readonly LaneInfo Config;
        public ManiaIcon Icon;
        public ColourInfo BackgroundColor;
        public float X;
        public float LeftBorderWidth;
        public float Width;
        public Lane(LaneInfo laneConfig)
        {
            Config = laneConfig;
            BackgroundColor = laneConfig.Color.MultiplyAlpha(0.05f);
        }
    }
    Dictionary<DrumChannel, Lane> PrimaryLaneLookup = new();
    Dictionary<DrumChannel, Lane> SecondaryLaneLookup = new();
    public Lane GetLane(HitObjectData data)
    {
        var channel = data.Channel;
        if (channel == DrumChannel.BassDrum && data.Modifiers.HasFlag(NoteModifiers.Left))
            return PrimaryLaneLookup[DrumChannel.HiHatPedal];
        if (channel == DrumChannel.Crash && data.Modifiers.HasFlag(NoteModifiers.Left))
            return PrimaryLaneLookup[DrumChannel.China];
        if ((channel == DrumChannel.China || channel == DrumChannel.Splash) && data.Modifiers.HasFlag(NoteModifiers.Right))
            return PrimaryLaneLookup[DrumChannel.Crash];
        if (PrimaryLaneLookup.TryGetValue(channel, out var o))
            return o;
        return GetLane(channel);
    }
    public Lane GetLane(DrumChannelEvent ev) => GetLane(ev.Channel);
    public Lane GetLane(DrumChannel channel)
    {
        if (PrimaryLaneLookup.TryGetValue(channel, out var o) || SecondaryLaneLookup.TryGetValue(channel, out o))
            return o;
        return null;
    }
    public ManiaBeatmapDisplay()
    {
        AddInternal(new Box { RelativeSizeAxes = Axes.Both, Colour = ManiaConfig.BackgroundColor, Depth = 1000 });
    }

    Container LaneContainer = new();
    Container ScrollingOverlay = new();

    List<Chip> Chips;

    void LoadChips()
    {
        Chips = new();
        foreach (var hitObject in Beatmap.GetRealTimeHitObjects())
            Chips.Add(new Chip(this, hitObject));
        Chips.Sort((a, b) => a.HitTime.CompareTo(b.HitTime));

        foreach (var line in Beatmap.BeatLinesMs())
            Lines.Add(new(line));
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        LaneContainer.RelativeSizeAxes = Axes.Both;
        LaneContainer.Origin = Anchor.TopCentre;
        LaneContainer.Anchor = Anchor.TopCentre;
        LaneContainer.Width = ManiaConfig.Width;
        LaneContainer.Add(new Canvas<ManiaCanvasData>
        {
            RelativeSizeAxes = Axes.Both,
            Draw = DrawCanvas,
            ApplyState = ApplyState,
            Relative = true
        });
        var lanes = Util.Skin.Mania.Lanes.LaneList;
        Lanes = new Lane[lanes.Length];
        var totalWeight = lanes.Sum(e => e.LeftBorder + e.Width) + lanes[0].LeftBorder;
        var x = 0f;
        for (var i = 0; i < lanes.Length; i++)
        {
            var lane = lanes[i];
            x += lane.LeftBorder;
            var laneObject = new Lane(lane)
            {
                X = x / totalWeight,
                Width = lane.Width / totalWeight,
                LeftBorderWidth = lane.LeftBorder / totalWeight
            };
            Lanes[i] = laneObject;
            var channel = lane.Channel;
            PrimaryLaneLookup[channel] = laneObject;
            if (lane.Secondary != null)
                foreach (var s in lane.Secondary)
                    SecondaryLaneLookup[s.Channel] = laneObject;
            ManiaIcon icon = null;
            if (lane.Icon != null)
                icon = new SpriteManiaIcon(lane);
            else if (channel == DrumChannel.Snare || channel == DrumChannel.SmallTom || channel == DrumChannel.MediumTom || channel == DrumChannel.LargeTom)
                icon = new ManiaDrumIcon(lane);
            else if (channel.IsFoot())
                icon = new ManiaFootIcon(lane);
            else if (channel.IsCymbal())
                icon = new ManiaCymbalIcon(lane);
            if (icon != null)
            {
                laneObject.Icon = icon;
                icon.Depth = -1;
                icon.X = laneObject.X;
                icon.Width = laneObject.Width;
                icon.Height = ManiaConfig.JudgementPosition;
                icon.Anchor = Anchor.BottomLeft;
                icon.Origin = Anchor.BottomLeft;
                LaneContainer.Add(icon);
            }
            x += lane.Width;
        }
        LoadChips();
        LaneContainer.Add(ScrollingOverlay = new NoMaskContainer
        {
            RelativeSizeAxes = Axes.Both,
            RelativePositionAxes = Axes.Y,
            Depth = -5
        });
        LaneContainer.Add(new Box
        {
            Colour = ManiaConfig.JudgementColor,
            Y = -ManiaConfig.JudgementPosition,
            Height = ManiaConfig.JudgementThickness,
            RelativeSizeAxes = Axes.Both,
            RelativePositionAxes = Axes.Y,
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.CentreLeft,
        });
        AddInternal(LaneContainer);
    }

    protected override void Update()
    {
        if (Dragging) UpdateDrag();
        ScrollingOverlay.Y = (float)(Track.CurrentTime * ScrollRate - ManiaConfig.JudgementPosition);
    }

    SpriteText StatsText;
    public override void EnterPlayMode()
    {
        if (StatsText == null)
            AddInternal(StatsText = new StatsText { Colour = Colour4.White, X = 3 });
        base.EnterPlayMode();
    }
    public override void LeavePlayMode()
    {
        base.LeavePlayMode();
        RemoveInternal(StatsText, true);
        StatsText = null;
    }

    public override void HandleScoreChange()
    {
        StatsText.Text = $"{Scorer.Accuracy}  {Scorer.ReplayInfo.Combo}x";
    }

    public override void DisplayScoreEvent(ScoreEvent e)
    {
        var lane = GetLane(e.Channel);
        var hitTime = e.Time ?? e.ObjectTime ?? 0;

        var h = new Box
        {
            Width = lane.Width,
            Height = ManiaConfig.ChipThickness,
            X = lane.X,
            Y = (float)(-hitTime * ScrollRate),
            Depth = -4,
            Colour = e.Colour,
            RelativePositionAxes = Axes.Both,
            RelativeSizeAxes = Axes.Both,
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.CentreLeft
        };
        ScrollingOverlay.Add(h);
        h.FadeOut(1000).Expire();
    }

    public override void OnDrumTrigger(DrumChannelEvent ev)
    {
        GetLane(ev).Icon.Hit(ev.ComputedVelocity);
    }

    public override void ReloadNoteRange(AffectedRange range)
    {
        LoadChips();
    }
}