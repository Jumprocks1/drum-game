using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Beatmaps.Display.Components;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Containers;
using DrumGame.Game.Skinning;
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
    Colour4 TextColor = Colour4.White;
    void ApplyState(ManiaCanvas.Data data)
    {
        var time = Track.CurrentTime;
        var heightOfDisplayInMs = 1 / ScrollRate;
        var overdraw = ManiaConfig.ChipThickness * 2; // the "perfect" overdraw is ChipThickness / 2, we add extra for safety

        var startTime = time - heightOfDisplayInMs * (ManiaConfig.JudgementLinePosition + overdraw);
        var endTime = time + heightOfDisplayInMs * (1 - ManiaConfig.JudgementLinePosition + overdraw);

        data.VisibleChips = Chips.FindRangeContinuous(startTime, endTime);
        data.VisibleLines = Lines.FindRangeContinuous(startTime, endTime);
        data.Offset = ScoreEventContainer.Y;
        data.RecentJudgements ??= new ManiaCanvas.Data.JudgementEvent[Lanes.Length];
        data.TrackTime = time;
        data.UpdateTime = Clock.CurrentTime;

        var start = data.VisibleChips.Start;
        var end = data.VisibleChips.End;
        data.HiddenChips.Clear();
        for (var i = start; i < end; i++)
            if (Chips[i].Hide) data.HiddenChips.Add(i);

        for (var i = 0; i < Lanes.Length; i++)
            data.RecentJudgements[i] = Lanes[i].RecentJudgement;
    }

    void DrawCanvas(ManiaCanvas.Node node, ManiaCanvas.Data data)
    {
        foreach (var lane in Lanes)
        {
            node.Color = lane.Config.BorderColor;
            node.Box(lane.X, 0, -lane.LeftBorderWidth, 1);
            lane.DrawBackground(node);
            node.ResetShader();
        }
        var lane1 = Lanes[0];
        node.Color = lane1.Config.BorderColor;
        node.Box(1, 0, -lane1.LeftBorderWidth, 1);

        var defaultMatrix = node.Matrix;
        node.Translate(new Vector2(0, node.State.Offset));

        var end = node.State.VisibleLines.End;
        for (var i = node.State.VisibleLines.Start; i < end; i++)
            Lines[i].Draw(node);

        var start = data.VisibleChips.Start;
        end = data.VisibleChips.End;
        // these frick up our blending, be careful
        // we split into 2 loops to prevent swapping blend modes
        // swapping blend modes forces a flush of the draw buffer
        node.Alpha = 1;
        var nextHidden = 0;
        var hidden = data.HiddenChips;
        for (var i = start; i < end; i++)
        {
            if (nextHidden < hidden.Count && hidden[nextHidden] == i) { nextHidden++; continue; }
            if (PracticeMode != null)
            {
                var contained = Chips[i].HitTime > PracticeMode.StartTime && Chips[i].HitTime < PracticeMode.EndTime;
                node.Alpha = contained ? 1 : 1 - PracticeMode.Config.OverlayStrength;
            }
            Chips[i].DrawAdornment(node);
        }
        nextHidden = 0;
        for (var i = start; i < end; i++)
        {
            if (nextHidden < hidden.Count && data.HiddenChips[nextHidden] == i) { nextHidden++; continue; }
            if (PracticeMode != null)
            {
                var contained = Chips[i].HitTime > PracticeMode.StartTime && Chips[i].HitTime < PracticeMode.EndTime;
                node.Alpha = contained ? 1 : 1 - PracticeMode.Config.OverlayStrength;
            }
            Chips[i].DrawChip(node);
        }
        node.Alpha = 1;

        node.Matrix = defaultMatrix;
        var time = node.Time;
        var judgements = ManiaConfig.Judgements;
        for (var i = 0; i < data.RecentJudgements.Length; i++)
        {
            var lane = Lanes[i];
            var judgement = data.RecentJudgements[i];
            if (judgement != null)
            {
                var texture = judgements.TextureForJudgement(judgement.Rating);
                if (texture != null)
                {
                    node.Time = data.UpdateTime - judgement.UpdateTime;
                    if (texture.FrameDuration == 0 || (int)(node.Time / texture.FrameDuration) < texture.FrameCount)
                    {
                        if (texture.FragmentShader != null)
                        {
                            node.SetLaneParameters(lane);
                            node.SetJudgementParameters(judgement);
                            texture.DrawCentered(node, lane.X + lane.Width / 2, 1 - lane.Config.JudgementTextPosition, 1, 0);
                            node.Flush(); // required since setting the uniform doesn't flush
                        }
                        else
                        {
                            texture.DrawCentered(node, lane.X + lane.Width / 2, 1 - lane.Config.JudgementTextPosition, 1, 0);
                        }
                    }
                }
            }
        }
        node.Time = time;
        node.ResetShader();
    }

    public Lane[] Lanes;

    static ManiaSkinInfo ManiaConfig => Util.Skin.Mania;
    static bool HasShutter => ManiaConfig.Shutter != null && ManiaConfig.Shutter.Height != 0;
    public double ScrollRate => ManiaSkinInfo.ScrollRate;
    public List<BeatLine> Lines = new();
    public class BeatLine : IComparable<double>
    {
        float Y;
        float Height;
        double HitTime;
        bool Measure;
        public void Draw(ManiaCanvas.Node node)
        {
            node.Color = Measure ? ManiaConfig.MeasureLineColor : ManiaConfig.BeatLineColor;
            node.Box(0, Y, 1, Height);
        }
        public int CompareTo(double other) => HitTime.CompareTo(other);
        public BeatLine((double Time, bool Measure) line)
        {
            HitTime = line.Time;
            Measure = line.Measure;
            Height = line.Measure ? ManiaConfig.MeasureLineThickness : ManiaConfig.BeatLineThickness;
            Y = (float)(-HitTime * ManiaSkinInfo.ScrollRate) + 1 - Height / 2;
        }
    }
    public class Chip : IComparable<double>
    {
        public readonly Lane Lane;
        public readonly IChipInfo ChipInfo;
        public readonly float X;
        public readonly float Y;
        public readonly float Width; // width of the parent lane. Could actually just make this => Lane.Width
        public readonly float Height;
        public readonly float ChipWidth; // 0-1, typically 1. Like 0.8 for ghost notes.
        public readonly DrumChannel Channel;
        public readonly NoteModifiers Modifiers;
        public bool Hide; // hide chip after it is hit
        public void DrawAdornment(ManiaCanvas.Node node)
        {
            var adornment = ChipInfo.Adornment;
            if (adornment != null)
            {
                node.SetLaneParameters(Lane);
                node.SetNoteParameters(this, true);
                adornment.DrawCentered(node, X, Y, Width, Height);
                node.Flush();
            }
        }
        public void DrawChip(ManiaCanvas.Node node)
        {
            if (ChipInfo.Chip == null)
            {
                node.Color = ChipInfo.Color;
                node.ResetShader();
                node.CenterBox(X, Y, Width * ChipWidth, Height);
            }
            else
            {
                // probably don't actually need to set this, we'll see if it ever gets used, remove if not
                node.SetLaneParameters(Lane);
                node.SetNoteParameters(this, false);
                ChipInfo.Chip.DrawCentered(node, X, Y, Width * ChipWidth, Height);
                node.Flush(); // required since setting the uniform doesn't flush
            }
        }
        public Chip(ManiaBeatmapDisplay display, HitObjectRealTime ho)
        {
            Channel = ho.Channel;
            Modifiers = ho.Data.Modifiers;
            Lane = display.GetLane(ho.Data);
            var laneConfig = Lane.Config;
            HitTime = ho.Time;
            Width = Lane.Width;
            Height = ManiaConfig.ChipThickness;
            X = Lane.X + Width / 2;
            Y = (float)(-HitTime * display.ScrollRate) + 1;
            ChipWidth = ho.Data.Modifiers.HasFlag(NoteModifiers.Ghost) ? Util.Skin.Mania.GhostNoteWidth : 1f;
            if (ho.Data.Preset != null)
                ChipWidth *= ho.Data.Preset.Size;

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
        public SkinTexture Background => Config.Background;
        public ManiaJudgementErrorNumbers ErrorNumbers;
        public ManiaIcon Icon;
        public ColourInfo BackgroundColor;
        public float X;
        public float LeftBorderWidth;
        public float Width;
        public ManiaCanvas.Data.JudgementEvent RecentJudgement;
        public Lane(LaneInfo laneConfig)
        {
            Config = laneConfig;
            BackgroundColor = laneConfig.Color.MultiplyAlpha(0.05f);
        }
        public void DrawBackground(ManiaCanvas.Node node)
        {
            if (Background != null)
            {
                node.SetLaneParameters(this);
                Background.Draw(node, X, 0, Width, 1);
                node.Flush(); // required since setting the uniform doesn't flush
            }
            else
            {
                node.Color = BackgroundColor;
                node.ResetShader();
                node.Box(X, 0, Width, 1);
            }
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

    public LaneContainer LaneContainer = new();
    Container ScoreEventContainer = new();
    ManiaTimeline Timeline;

    // these are sorted for extra safety, don't use with OriginalObjectIndex
    // could use an array here but half our extension methods only work with lists, oops
    List<Chip> Chips = [];
    Chip[] OriginalChips; // has default ordering. Safe to use with OriginalObjectIndex

    void LoadChips()
    {
        var hitObjects = Beatmap.GetRealTimeHitObjects();
        Chips.Clear();
        Chips.EnsureCapacity(hitObjects.Count);
        OriginalChips = new Chip[hitObjects.Count];
        for (var i = 0; i < hitObjects.Count; i++)
        {
            var chip = new Chip(this, hitObjects[i]);
            Chips.Add(chip);
            OriginalChips[i] = chip;
        }
        Chips.Sort((a, b) => a.HitTime.CompareTo(b.HitTime));

        Lines.Clear();
        foreach (var line in Beatmap.BeatLinesMs())
            Lines.Add(new(line));
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        Track.AfterSeek += AfterSeek;
        SkinManager.RegisterTarget(SkinAnchorTarget.LaneContainer, LaneContainer);
        AddInternal(Timeline = new(this));
        SkinManager.RegisterTarget(SkinAnchorTarget.PositionIndicator, Timeline);
        AddInternal(new SongInfoPanel(Beatmap, true));
        AddInternal(new ManiaVideo(Player));
        LaneContainer.Add(new ManiaCanvas(this)
        {
            RelativeSizeAxes = Axes.Both,
            Draw = DrawCanvas,
            ApplyState = ApplyState,
            Relative = true
        });
        LoadLanes();
        LoadChips();
        LaneContainer.Add(ScoreEventContainer = new NoMaskContainer
        {
            RelativeSizeAxes = Axes.Both,
            RelativePositionAxes = Axes.Y,
            // if we have shutter, these have to appear below the icons and the shutter
            Depth = HasShutter ? -1f : -5
        });
        LaneContainer.Add(new Box
        {
            Colour = ManiaConfig.JudgementLineColor,
            Y = -ManiaConfig.JudgementLinePosition + (float)(-ManiaConfig.JudgementLineOffset * ScrollRate),
            Height = ManiaConfig.JudgementLineThickness,
            RelativeSizeAxes = Axes.Both,
            RelativePositionAxes = Axes.Y,
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.CentreLeft,
            Name = "Cursor"
        });
        if (HasShutter)
        {
            var d = ManiaConfig.Shutter.Texture?.MakeSprite() ?? new Box { Colour = ManiaConfig.BackgroundColor };
            d.RelativeSizeAxes = Axes.Both;
            d.RelativePositionAxes = Axes.Both;
            d.Height = ManiaConfig.JudgementLinePosition;
            d.Anchor = Anchor.BottomLeft;
            d.Origin = Anchor.TopLeft;
            d.Y = -ManiaConfig.JudgementLinePosition;
            d.Depth = -2; // icons are -3
            LaneContainer.Add(d);
        }
        AddInternal(LaneContainer);
        AddInternal(ModeText = new CommandText(Command.SwitchMode) { Colour = TextColor, Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft, X = 3 });
        Player.ModeChanged += UpdateModeText;
        UpdateModeText(Player.Mode);
    }
    SpriteText ModeText;

    void UpdateModeText(BeatmapPlayerMode mode)
    {
        ModeText.Text = mode == BeatmapPlayerMode.Replay ? string.Empty : $"{Player.Mode} Mode ";
    }

    protected override void SkinChanged()
    {
        base.SkinChanged();
        PrimaryLaneLookup.Clear();
        SecondaryLaneLookup.Clear();
        LaneContainer.RemoveAll(e => e is ManiaIcon, true);
        LoadLanes();
        LoadChips();
    }

    void LoadLanes()
    {
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
                icon.Depth = -3;
                icon.X = laneObject.X;
                icon.Width = laneObject.Width;
                icon.Height = ManiaConfig.JudgementLinePosition;
                icon.Anchor = Anchor.BottomLeft;
                icon.Origin = Anchor.BottomLeft;
                LaneContainer.Add(icon);
            }
            x += lane.Width;
        }
    }
    protected override void Update()
    {
        if (Dragging) UpdateDrag();
        ScoreEventContainer.Y = (float)(Track.CurrentTime * ScrollRate - ManiaConfig.JudgementLinePosition);
    }

    SpriteText StatsText;
    public override void EnterPlayMode()
    {
        if (StatsText == null && !HideJudgements)
            AddInternal(StatsText = new StatsText { Colour = TextColor, X = 3 });
        base.EnterPlayMode();
    }
    public override void LeavePlayMode()
    {
        base.LeavePlayMode();
        if (StatsText != null)
        {
            RemoveInternal(StatsText, true);
            StatsText = null;
        }
    }

    public override void HandleScoreChange()
    {
        if (StatsText != null)
            StatsText.Text = $"{Scorer.Accuracy}  {Scorer.ReplayInfo.Combo}x";
    }


    void AfterSeek(double t)
    {
        if (Chips != null && HiddenChips)
        {
            foreach (var chip in Chips) chip.Hide = false;
            HiddenChips = false;
        }
    }
    bool HiddenChips; // set if there are any chips in Chips list with Hide == true
    public override void DisplayScoreEvent(ScoreEvent e)
    {
        if (HideJudgements) return;
        Chip chip = null;
        var hitTime = e.Time ?? e.ObjectTime ?? 0;
        if (e.OriginalObjectIndex >= 0 && e.OriginalObjectIndex < OriginalChips.Length && ManiaConfig.Judgements.HideHitChips)
        {
            chip = OriginalChips[e.OriginalObjectIndex];
            chip.Hide = true;
        }
        HiddenChips = true;

        var lane = chip?.Lane ?? GetLane(e.Channel);
        if (lane == null) return;

        if (e.InputEvent != null)
            lane.Icon.Hit(e.InputEvent.ComputedVelocity);


        if (Util.Skin.Mania.Judgements.ErrorNumbers.Show && e.HitError is double he)
        {
            var errorNumberLane = lane;
            if (ManiaConfig.Judgements.ErrorNumbers.SingleLane is DrumChannel singleLane)
                errorNumberLane = GetLane(singleLane);
            if (errorNumberLane.ErrorNumbers == null)
                LaneContainer.Add(errorNumberLane.ErrorNumbers = new(errorNumberLane));
            errorNumberLane.ErrorNumbers.DisplayError(he);
        }

        if (ManiaConfig.Judgements.Chips)
        {
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
            ScoreEventContainer.Add(h);
            h.FadeOut(1000).Expire();
        }
        if (ManiaConfig.Judgements.Textures && e.Rating != HitScoreRating.Ignored)
            lane.RecentJudgement = new(e, Clock.CurrentTime);
    }

    // we don't use this since it's hard to get the lane from the DrumChannelEvent
    // instead we use DisplayScoreEvent which has the DrumChannelEvent anyways
    public override void OnDrumTrigger(DrumChannelEvent ev) { }

    public override void ReloadNoteRange(AffectedRange range)
    {
        LoadChips();
    }

    protected override void Dispose(bool isDisposing)
    {
        Track.AfterSeek -= AfterSeek;
        Player.ModeChanged -= UpdateModeText;
        SkinManager.UnregisterTarget(LaneContainer);
        SkinManager.UnregisterTarget(Timeline);
        base.Dispose(isDisposing);
    }
}