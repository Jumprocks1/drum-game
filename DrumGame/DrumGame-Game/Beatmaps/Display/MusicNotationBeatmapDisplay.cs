using System;
using System.Collections.Generic;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Display.Components;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Containers;
using DrumGame.Game.Input;
using DrumGame.Game.Notation;
using DrumGame.Game.Stores;
using DrumGame.Game.Skinning;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osuTK;
using osuTK.Input;

namespace DrumGame.Game.Beatmaps.Display;

public partial class MusicNotationBeatmapDisplay : BeatmapDisplay
{
    Colour4 TextColor = Util.Skin.Notation.NotationColor;
    public bool SmoothScroll;
    public const int ContainerSize = 4; // in beats, does not have to be 4 (not related to measures)
    public const float TopbarHeight = 50;
    public const float NoteContainerTopPadding = 1f;
    public const float NoteContainerBottomPadding = 1f;
    public const float NoteContainerHeight = 1f + NoteContainerTopPadding + NoteContainerBottomPadding;
    public static readonly double ScaleStep = Math.Pow(2, 1.0 / 12.0);
    public const float DefaultStaffHeight = 80;
    private float _staffHeight = DefaultStaffHeight;
    public float StaffHeight => _staffHeight;
    double _zoomLevel = 1;
    public double ZoomLevel
    {
        get => _zoomLevel; set
        {
            if (_zoomLevel == value) return;
            _zoomLevel = value;
            UpdateLayout();
        }
    }
    public float BeatWidth => StaffHeight * Font.Spacing / 4;
    // the size of these is arbitary and not related to music measures
    List<Container> NoteGroupContainers;
    protected FileSystemResources Resources { get; private set; }
    public MusicFont Font;
    (int, int) DisplayRange = (0, 0);
    public float Inset = 4;
    internal NoteContainer NoteContainer;
    Container AnnotationsContainer;
    [Resolved] DrumInputManager InputManager { get; set; }
    public bool SnapIndicator;
    internal Box SongCursor;
    public bool _songCursorVisible = true;
    public bool SongCursorVisible
    {
        get => _songCursorVisible; set
        {
            _songCursorVisible = value;
            if (SongCursor != null) SongCursor.Alpha = value ? 1 : 0;
        }
    }
    SpriteText ZoomText;
    SpriteText TempoText;
    SpriteText BeatText;
    protected FillFlowContainer StatusContainer;
    SpriteText ModeText;
    SpriteText SnapText;
    Box SnapCursor;
    Box JumpCursor;
    EventContainer EventContainer;
    public BeatmapAuxDisplay AuxDisplay;
    public MusicNotationBeatmapDisplay()
    {
        RelativeSizeAxes = Axes.Both;
        AddInternal(new Box { RelativeSizeAxes = Axes.Both, Colour = Util.Skin.Notation.PlayfieldBackground, Depth = 1000 });
        SmoothScroll = Util.ConfigManager.SmoothScroll.Value;
        Util.ConfigManager.CursorInset.BindValueChanged(InsetChanged, true);
    }
    OutsideNoteContainer OutsideNoteContainer;
    Container NoteGroupContainerContainer;
    public override void ReloadNoteRange(AffectedRange range)
    {
        if (!range.HasChange) return;
        if (range.Everything)
        {
            ReloadNotes(0, (int)(Beatmap.QuarterNotes / ContainerSize) + 1);
        }
        else
        {
            var tickRate = Beatmap.TickRate;

            // computing the container for a given tick range is somewhat difficult unfortunately
            // we have to first compute what group the tick is in, then we compute what container that group is in
            var measureTick = Beatmap.TickFromMeasure(Beatmap.MeasureFromTick(range.Start));
            var measureBeat = (range.Start - measureTick) / tickRate;
            var groupBeat = (measureBeat * tickRate + measureTick) / tickRate;
            var containerStart = Math.Max(0, groupBeat / ContainerSize);

            // have to be careful here since range.End and containerEnd are both exclusive
            var endTick = range.End - 1;
            measureTick = Beatmap.TickFromMeasure(Beatmap.MeasureFromTick(endTick));
            measureBeat = (endTick - measureTick) / tickRate;
            groupBeat = (measureBeat * tickRate + measureTick) / tickRate;
            var containerEnd = Math.Max(0, groupBeat / ContainerSize + 1);

            ReloadNotes(containerStart, containerEnd);
        }
    }
    public void ReloadNotes(int start, int end)
    {
        var hadMeasureLines = MeasureLines != null;
        Beatmap.UpdateLength();
        var length = Beatmap.QuarterNotes;

        var containerCount = Math.Max(end, (int)(length / ContainerSize) + 1);

        NoteGroupContainers ??= new(containerCount);
        if (NoteGroupContainerContainer == null) NoteContainer.Add(NoteGroupContainerContainer = new Container { Depth = -1 });
        var oldContainerCount = NoteGroupContainers.Count;

        // make sure we load all the way through the new containers, very important since length may not be directly tied to `end`
        if (oldContainerCount != containerCount) end = containerCount;

        Logger.Log($"reloading {start} to {end}", level: LogLevel.Debug);

        for (var i = oldContainerCount; i < containerCount; i++)
        {
            NoteGroupContainers.Add(new NoMaskContainer
            {
                X = i * Font.Spacing * ContainerSize,
                Depth = -i
            });
        }

        if (OutsideNoteContainer == null) NoteContainer.Add(OutsideNoteContainer = new OutsideNoteContainer() { Depth = 10 });
        else
        {
            for (var i = start; i < end; i++) OutsideNoteContainer.Clear(i);
        }

        // this will orphan some MeasureLines, but they will be cleared whenever we toggle lines off
        for (var i = start; i < end; i++) NoteGroupContainers[i].Clear();

        var startBeat = start * ContainerSize;
        var endBeat = end * ContainerSize; // exclusive
        var groups = NoteGroup.GetGroupsInRange(Beatmap, startBeat, endBeat);

        foreach (var group in groups)
        {
            var containerI = group.Beat / ContainerSize;
            var container = NoteGroupContainers[containerI];
            OutsideNoteContainer.CurrentTarget = containerI;
            Font.RenderGroup(container, group, containerI * ContainerSize, OutsideNoteContainer);
        }

        if (hadMeasureLines)
        {
            AddMeasureLines(Math.Min(oldContainerCount, start), end);
        }
    }
    public void LoadNotes()
    {
        ReloadNoteRange(true);
        LoadComponents(NoteGroupContainers);
    }
    bool selectionPending = false;
    public BeatSelection Selection;
    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button == MouseButton.Left)
        {
            if (Player is BeatmapEditor ed)
            {
                if (ed.Mode.HasFlagFast(BeatmapPlayerMode.Edit))
                {
                    var beat = ed.SnapBeat(NoteContainer.ToLocalSpace(e.ScreenSpaceMousePosition).X / Font.Spacing);
                    if (e.ShiftPressed) ExpandSelectionTo(beat, false);
                    else StartSelection(beat);
                    selectionPending = true;
                    return true;
                }
                else
                {
                    ClearSelection();
                }
            }
        }
        else if (e.Button == MouseButton.Middle || e.Button == MouseButton.Right)
        {
            LastDragMouse = e.MousePosition;
            DragStart = (Track.CurrentTime, LastDragMouse, Track.IsRunning);
            Dragging = true;
            Track.Stop();
            return true;
        }
        return base.OnMouseDown(e);
    }
    protected override void OnMouseUp(MouseUpEvent e)
    {
        if (e.Button == MouseButton.Left)
        {
            if (Player is BeatmapEditor && selectionPending)
            {
                selectionPending = false;
                if (Selection != null && !Selection.IsComplete) ClearSelection();
            }
        }
        else if (e.Button == MouseButton.Middle || e.Button == MouseButton.Right)
        {
            // Technically this isn't perfect since the track could be paused/started during drag
            // Ideally we would add a bindable lock to Track
            if (Dragging)
            {
                Track.CommitAsyncSeek();
                if (DragStart.Running) Track.Start();
                Dragging = false;
            }
        }
        base.OnMouseUp(e);
    }
    public bool Dragging;
    public (double Time, Vector2 Start, bool Running) DragStart;
    public Vector2 LastDragMouse;
    public void BeatSnapChanged()
    {
        if (Selection != null && Selection.IsComplete && Player is BeatmapEditor ed)
        {
            this.SelectionOverlay?.Update(Beatmap.TickFromBeatSlow(Selection.Start),
                Beatmap.TickFromBeatSlow(Selection.End.Value), ed.TickStride);
        }
    }
    public SongInfoPanel InfoPanel;
    public VolumeControlGroup VolumeControls;
    void UpdateNoteContainerLength() => NoteContainer.BeatCount = Beatmap.QuarterNotes;
    void MeasuresUpdated()
    {
        // we have to reload all notes since changing a measure marking can completely change the note grouping
        // technically we could just reload notes after the measure change,
        //   but measure changes should be added very infrequently, so it isn't a big deal
        // this will also handle updating the measure lines
        ReloadNoteRange(true);
    }
    public const float ModeTextHeight = 20;
    [BackgroundDependencyLoader]
    private void load()
    {
        Util.CommandController.RegisterHandlers(this);
        AddInternal(NoteContainer = new NoteContainer(Font));
        if (!SmoothScroll)
        {
            NoteContainer.Add(JumpCursor = new Box
            {
                Width = 0.5f,
                Colour = Colour4.SeaGreen.MultiplyAlpha(0.25f),
                Origin = Anchor.TopCentre,
                Y = -2,
                Height = 8,
            });
        }
        Beatmap.MeasuresUpdated += MeasuresUpdated;
        Beatmap.LengthChanged += UpdateNoteContainerLength;
        UpdateNoteContainerLength();
        NoteContainer.Add(SongCursor = new Box
        {
            Width = 0.5f,
            Colour = Colour4.CornflowerBlue.MultiplyAlpha(0.5f),
            Alpha = _songCursorVisible ? 1 : 0,
            Origin = Anchor.TopCentre,
            Y = -2,
            Height = 8,
            Depth = -3,
        });
        if (Player is BeatmapEditor)
        {
            NoteContainer.Add(SnapCursor = new Box
            {
                Width = 0.5f,
                Colour = Colour4.PaleVioletRed.MultiplyAlpha(0.5f),
                Alpha = 0,
                Origin = Anchor.TopCentre,
                Y = -2,
                Height = 8
            });
        }
        AddInternal(AuxDisplay = new BeatmapAuxDisplay(this) { RelativeSizeAxes = Axes.Both });
        UpdateLayout();
        AddInternal(new BeatmapTimeline(Beatmap, Track, Player as BeatmapEditor)
        {
            Origin = Anchor.BottomLeft,
            RelativeSizeAxes = Axes.X,
            Anchor = Anchor.BottomLeft
        });
        StatusContainer = new FillFlowContainer
        {
            Origin = Anchor.BottomRight,
            Anchor = Anchor.BottomRight,
            Y = -BeatmapTimeline.Height,
            X = -2,
            AutoSizeAxes = Axes.Both,
        };
        StatusContainer.Add(ZoomText = new CommandText(Command.SetZoom) { Colour = TextColor });
        StatusContainer.Add(TempoText = new CommandText(Command.SetPlaybackSpeed) { Colour = TextColor });
        StatusContainer.Add(BeatText = new CommandText(Command.SeekToBeat) { Colour = TextColor });
        AddInternal(StatusContainer);
        AddInternal(VolumeControls = new VolumeControlGroup(Player as BeatmapEditor));
        if (Player is BeatmapEditor)
        {
            AddInternal(new CommandIconButton(Command.EditorTools, FontAwesome.Solid.Tools, 40)
            {
                Origin = Anchor.BottomRight,
                Anchor = Anchor.BottomRight,
                Y = -BeatmapTimeline.Height - 20 - VolumeControls.Height,
                Colour = TextColor
            });
        }
        var modeContainer = new FillFlowContainer
        {
            Origin = Anchor.BottomLeft,
            Anchor = Anchor.BottomLeft,
            Y = -BeatmapTimeline.Height,
            AutoSizeAxes = Axes.Both
        };
        modeContainer.Add(ModeText = new CommandText(Command.SwitchMode) { Margin = new() { Left = 2 }, Colour = TextColor });
        SkinManager.RegisterTarget(SkinAnchorTarget.ModeText, modeContainer);
        if (Player is BeatmapEditor) modeContainer.Add(SnapText = new CommandText(Command.SetEditorSnapping) { Colour = TextColor });
        AddInternal(modeContainer);
        AddInternal(InfoPanel = new SongInfoPanel(Beatmap));
        AddInternal(EventContainer = new());
        LoadNotes();
        Beatmap.AnnotationsUpdated += LoadAnnotations;
        LoadAnnotations();
        LogEvent("Beatmap loaded");
    }
    public void Add(Drawable drawable) => AddInternal(drawable);
    public void Remove(Drawable drawable, bool dispose) => RemoveInternal(drawable, dispose);
    void LoadAnnotations()
    {
        if (AnnotationsContainer == null)
        {
            AnnotationsContainer = new Container { Depth = -10 };
            NoteContainer.Add(AnnotationsContainer);
        }
        else
        {
            AnnotationsContainer.Clear();
        }
        foreach (var annotation in Beatmap.Annotations)
        {
            AnnotationsContainer.Add(new SpriteText
            {
                Text = annotation.Text,
                Colour = TextColor,
                X = (float)annotation.Time * Font.Spacing,
                Y = -4,
                Scale = new osuTK.Vector2(1f / 20),
                Origin = Anchor.BottomLeft
            });
        }
    }
    protected override void SkinChanged()
    {
        base.SkinChanged();
        ReloadNoteRange(true);
    }
    protected override void Dispose(bool isDisposing)
    {
        SkinManager.UnregisterTarget(SkinAnchorTarget.ModeText);
        Util.CommandController.RemoveHandlers(this);
        Beatmap.MeasuresUpdated -= MeasuresUpdated;
        Beatmap.LengthChanged -= UpdateNoteContainerLength;
        Beatmap.AnnotationsUpdated -= LoadAnnotations;
        Util.ConfigManager.CursorInset.ValueChanged -= InsetChanged;
        foreach (var e in NoteGroupContainers) e.Dispose();
        base.Dispose(isDisposing);
    }
    public void LogEvent(EventLog eventLog) => EventContainer.Add(eventLog);
    public virtual void UpdateLayout()
    {
        _staffHeight = (float)(DefaultStaffHeight * _zoomLevel * Util.ConfigManager.Get<double>(DrumGameSetting.ZoomMultiplier));
        var y = StaffHeight * NoteContainerTopPadding + TopbarHeight;
        NoteContainer.Y = y;
        NoteContainer.Scale = new osuTK.Vector2(StaffHeight / 4); // set scale to size of a single staff cell
        AuxDisplay.Padding = new MarginPadding
        {
            Top = TopbarHeight + StaffHeight * NoteContainerHeight,
            Bottom = BeatmapTimeline.Height
        };
        ApplyWaveFormStaffHeight();
    }
    BeatmapWaveform Waveform;
    public void ApplyWaveFormStaffHeight()
    {
        if (Waveform != null)
        {
            Waveform.StaffHeight = StaffHeight;
            Waveform.X = Inset / 4 * Font.Spacing * StaffHeight;
        }
    }
    private int Floor(double v) => (int)Math.Floor(v);
    protected override void Update()
    {
        if (Dragging)
        {
            var pos = ToLocalSpace(InputManager.CurrentState.Mouse.Position);
            if (LastDragMouse != pos)
            {
                Track.Stop(); // make sure we didn't start the track back up
                var d = pos.X - DragStart.Start.X;
                Track.Seek(Beatmap.MillisecondsFromBeat(Beatmap.BeatFromMilliseconds(DragStart.Time) - d * 4 / Font.Spacing / StaffHeight), true);
                LastDragMouse = pos;
            }
        }
        var currentQuarterNote = Track.CurrentBeat;
        var totalString = Beatmap.QuarterNotes.ToString();
        var beatString = Math.Max(0, (int)(currentQuarterNote + Beatmap.BeatEpsilon)).ToString().PadLeft(totalString.Length, '0');
        var tempo = Track.PlaybackSpeed.Value;
        var zoom = StaffHeight / DefaultStaffHeight;
        ZoomText.Text = $"Z:{zoom * 100:F0}%";
        TempoText.Text = $" {tempo:0.00}x ";
        BeatText.Text = $" {beatString} / {totalString}";
        ModeText.Text = Player.Mode == BeatmapPlayerMode.Replay ? string.Empty : $"{Player.Mode} Mode ";
        if (Player is BeatmapEditor ed)
        {
            if (Selection != null && selectionPending)
            {
                UpdateSelection(ed.SnapBeat(NoteContainer.ToLocalSpace(InputManager.CurrentState.Mouse.Position).X / Font.Spacing));
            }
            SnapText.Text = ed.BeatSnap.HasValue ? $" {ed.BeatSnap} snap" : " 0 snap";
            SnapCursor.Alpha = SnapIndicator ? 1 : 0;
            if (SnapIndicator)
            {
                var snapTarget = ed.SnapTarget;
                SnapCursor.X = (float)(snapTarget * Font.Spacing);
            }
        }
        var container = (currentQuarterNote - Inset) / ContainerSize;
        if (!SmoothScroll)
        {
            var visibleBeatsAfterInset = DrawWidth / BeatWidth - Inset;
            // the number of beats we jump each time
            var jumpMultiple = Math.Floor((visibleBeatsAfterInset - Inset) / 4) * 4;
            if (jumpMultiple <= 0) jumpMultiple = 4;
            var currentAnchor = Math.Floor(currentQuarterNote / jumpMultiple) * jumpMultiple;
            container = (currentAnchor - Inset) / ContainerSize;

            var nextJumpTarget = currentAnchor + jumpMultiple;
            JumpCursor.X = (float)(nextJumpTarget * Font.Spacing);
        }

        // we subtract 0.25 here to make sure we don't prune a container too early
        // if a container has a note at beat 3.99, this note will go past the measure's bounds.
        // Technically we would want to subtract the largest possible width of a note here, which is ~1 staff space, but 0.25 is more than enough
        var overdraw = 0.25; // in container units
        var newStart = Math.Max(0, Floor(container - overdraw));
        var newEnd = Math.Clamp(Floor(DrawWidth / BeatWidth / ContainerSize + container + overdraw) + 1, 0, NoteGroupContainers.Count);
        for (var i = DisplayRange.Item1; i < DisplayRange.Item2; i++)
            if (i < newStart || i >= newEnd) // old measure not in new measures
                NoteGroupContainerContainer.Remove(NoteGroupContainers[i], false);

        for (var i = newStart; i < newEnd; i++)
            if (i < DisplayRange.Item1 || i >= DisplayRange.Item2) // new measure not in old measures
                NoteGroupContainerContainer.Add(NoteGroupContainers[i]);

        NoteContainer.X = (float)(-container * Font.Spacing * StaffHeight / 4 * ContainerSize);
        SongCursor.X = (float)(currentQuarterNote * Font.Spacing);
        DisplayRange = (newStart, newEnd);
    }

    [CommandHandler]
    public bool AdjustZoom(CommandContext context)
    {
        context.GetNumber(e => ZoomLevel *= Math.Pow(ScaleStep, e), "Adjust Zoom", "Steps");
        return true;
    }

    [CommandHandler]
    public bool SetZoom(CommandContext context)
    {
        context.GetNumber(e => ZoomLevel = e, "Set Zoom", current: StaffHeight / DefaultStaffHeight);
        return true;
    }
    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key == Key.Escape && Selection != null)
        {
            ClearSelection();
            return true;
        }
        return base.OnKeyDown(e);
    }
    // this is pretty bad
    protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
    {
        var baseDep = base.CreateChildDependencies(parent);
        Resources = baseDep.Get<FileSystemResources>();
        Font = baseDep.Get<MusicFont>();
        if (Font == null)
        {
            var dependencies = new DependencyContainer(baseDep);
            Font = baseDep.Get<Lazy<MusicFont>>().Value;
            Font.Spacing = Beatmap.SpacingMultiplier.HasValue ?
                MusicFont.DefaultSpacing * Beatmap.SpacingMultiplier.Value :
                MusicFont.DefaultSpacing;
            Font.Spacing = (float)(Font.Spacing * Util.ConfigManager.Get<double>(DrumGameSetting.NoteSpacingMultiplier));
            dependencies.Cache(Font);
            return dependencies;
        }
        else
        {
            return baseDep;
        }
    }

    [CommandHandler]
    public void ToggleWaveform()
    {
        if (Waveform == null)
        {
            if (Player.CurrentAudioPath == null) return;
            AuxDisplay.Add(Waveform = new BeatmapWaveform(this));
            ApplyWaveFormStaffHeight();
        }
        else
        {
            AuxDisplay.Destroy(ref Waveform);
        }
    }

    public List<MeasureLine> MeasureLines = null;
    public void AddMeasureLines(int start, int end)
    {
        // this takes 2ms for TTFAF, but this is all just caused by the MeasureLine instantiation
        // I could not measure any noticable difference caused by the math for BeatsPerMeasure
        var containerSize = Beatmap.TickRate * ContainerSize;
        var startTick = start * containerSize;
        var endTick = end * containerSize;
        var beats = MeasureChange.DefaultBeats;
        var j = 0;
        var nextMeasureChange = Beatmap.MeasureChanges.Count > j ? Beatmap.MeasureChanges[j].Time : int.MaxValue;
        var gap = Beatmap.TickFromBeat(beats);
        // note that since gap is only updated when playing a measure line,
        //    putting a timing change in the middle of a measure will result
        //    in inaccurate lines. We will want to restrict the user in the future
        for (var i = 0; i < endTick; i += gap)
        {
            while (i >= nextMeasureChange)
            {
                beats = Beatmap.MeasureChanges[j].Beats;
                gap = Beatmap.TickFromBeat(beats);
                j += 1;
                nextMeasureChange = Beatmap.MeasureChanges.Count > j ? Beatmap.MeasureChanges[j].Time : int.MaxValue;
            }
            if (i < startTick) continue; // TODO optimize
            var container = i / containerSize;
            var line = new MeasureLine(NoteGroupContainers[container])
            {
                X = ((float)i / Beatmap.TickRate - container * ContainerSize) * Font.Spacing
            };
            MeasureLines.Add(line);
        }
    }
    [CommandHandler]
    public void ToggleMeasureLines()
    {
        if (MeasureLines != null)
        {
            foreach (var line in MeasureLines)
            {
                if (line.LineParent != null)
                {
                    line.LineParent.Remove(line, false);
                    line.LineParent = null;
                }
                line.Dispose();
            }
            MeasureLines = null;
        }
        else
        {
            if (NoteGroupContainers == null)
            {
                MeasureLines = new List<MeasureLine>();
            }
            else
            {
                MeasureLines = new List<MeasureLine>(NoteGroupContainers.Count);
                AddMeasureLines(0, NoteGroupContainers.Count);
            }
        }
    }
    [CommandHandler] public void ToggleSongCursor() => SongCursorVisible = !SongCursorVisible;
    void InsetChanged(ValueChangedEvent<double> e) => Inset = (float)(SmoothScroll ? e.NewValue : e.NewValue / 2);
    public (double, double) CurrentView()
    {
        var left = Track.CurrentBeat - Inset;
        return (left, DrawWidth / StaffHeight / Font.Spacing * 4 + left);
    }
    public override void PullView(ViewTarget target)
    {
        if (target == null) return;
        var (left, right) = CurrentView();
        var sLeft = target.Left;
        var sRight = target.Right;
        if (left <= sLeft && right >= sRight) return; // already in view
        Track.Seek(Beatmap.MillisecondsFromBeat(sLeft));
    }
    public ScoreTopBar ScoreTopBar;
    public override void EnterPlayMode()
    {
        AddInternal(ScoreTopBar = new ScoreTopBar(Scorer));
        AuxDisplay.SetInputHandler(true);
        base.EnterPlayMode();
    }
    public override void HandleScoreChange() => ScoreTopBar.HandleScoreChange();
    public override void LeavePlayMode()
    {
        AuxDisplay.SetInputHandler(false);
        RemoveInternal(ScoreTopBar, true);
        ScoreTopBar = null;
        base.LeavePlayMode();
    }
    public override void DisplayScoreEvent(ScoreEvent e)
    {
        if (HideJudgements) return;
        var xTime = e.Time ?? e.ObjectTime ?? 0;
        var h = new Circle
        {
            Colour = e.Colour,
            Width = 1.5f,
            Height = 1.5f,
            Origin = Anchor.Centre,
            X = (float)Beatmap.BeatFromMilliseconds(xTime) * Font.Spacing + 0.5f,
            Y = (float)Util.Skin.Notation.Channels[e.Channel].Position / 2,
            Depth = -2 // make sure we're on top of notes
        };
        NoteContainer.Add(h);
        h.FadeOut(1000).Expire();
    }

    public override void OnDrumTrigger(DrumChannelEvent ev) => AuxDisplay.InputDisplay?.Hit(ev);
}
public class MeasureLine : Box
{
    public MeasureLine(Container parent)
    {
        Colour = Util.Skin.Notation.MeasureLineColor;
        Width = 0.5f;
        Height = 8;
        Y = -2;
        Depth = -20;
        Origin = Anchor.TopCentre;
        LineParent = parent;
        parent.Add(this);
    }
    public Container LineParent;
}
public class ViewTarget
{
    public double Left;
    public double Right;
    public ViewTarget(double left, double right)
    {
        Left = left;
        Right = right;
    }
    public static implicit operator ViewTarget(double s) => new(s, s);
    public static implicit operator ViewTarget(BeatSelection s) => s == null ? null : new(s.Left, s.Right);
}
