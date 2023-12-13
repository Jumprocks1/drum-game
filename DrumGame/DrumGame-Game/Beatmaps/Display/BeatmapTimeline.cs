using System;
using System.IO;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Input;

namespace DrumGame.Game.Beatmaps.Display;

public class BeatmapTimeline : CompositeDrawable
{
    FontUsage Font => FrameworkFont.Regular;
    public static Colour4 BackgroundColour = new Colour4(4, 4, 4, 220);
    public static Colour4 TimelineColour = Colour4.Honeydew;
    TimelineThumb Thumb;
    Container timelineBarContainer;
    SpriteText TimeText;
    TimelineMarks TimelineMarks;
    readonly TrackClock Track;
    readonly Beatmap Beatmap;
    CommandIconButton playbackIcon;
    bool playingIconPlay;
    BeatmapEditor Editor;
    public new const float Height = 42;
    public const float TimelineAreaHeight = 30;
    public BeatmapTimeline(Beatmap beatmap, TrackClock track, BeatmapEditor editor = null)
    {
        Editor = editor;
        Beatmap = beatmap;
        Track = track;
        base.Height = Height;
        Beatmap.TempoUpdated += Reload;
        Beatmap.MeasuresUpdated += Reload;
        Beatmap.OffsetUpdated += Reload;
        Beatmap.BookmarkUpdated += Reload;
    }
    protected override void Dispose(bool isDisposing)
    {
        Beatmap.TempoUpdated -= Reload;
        Beatmap.MeasuresUpdated -= Reload;
        Beatmap.OffsetUpdated -= Reload;
        Beatmap.BookmarkUpdated -= Reload;
        if (Editor != null)
        {
            Editor.OnHistoryChange -= UpdateEditorInfo;
        }
        base.Dispose(isDisposing);
    }
    public void Reload() => TimelineMarks.Reload();
    [BackgroundDependencyLoader]
    private void load(CommandController command)
    {
        var barThicknesss = 5f;
        var thumbSize = 15f;
        AddInternal(new Box
        {
            Colour = BackgroundColour,
            RelativeSizeAxes = Axes.Both,
            Depth = 10
        });
        AddInternal(timelineBarContainer = new Container
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.X,
            Y = Height - TimelineAreaHeight / 2,
            Height = barThicknesss,
            Padding = new MarginPadding
            {
                Left = TimelineAreaHeight / 2,
                Right = TimelineAreaHeight / 2,
            },
            Children = new Drawable[] {
                        new Circle { // timeline bar
                            RelativeSizeAxes = Axes.Both,
                            Colour = TimelineColour
                        },
                        Thumb = new TimelineThumb(thumbSize) {X = 0}
                    }
        });
        // I couldn't get this to trigger before loading unfortunately,
        //   so not sure if it's necessary
        Track.AfterLoad(() => timelineBarContainer.Add(TimelineMarks = new TimelineMarks(Beatmap, Track)));
        AddInternal(TimeText = new CommandText(Command.SeekToTime)
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Font = Font.With(size: 20),
            Y = 1
        });
        AddInternal(playbackIcon = new CommandIconButton(Command.TogglePlayback,
            (playingIconPlay = Track.IsRunning) ? FontAwesome.Solid.Pause : FontAwesome.Solid.Play, 12)
        {
            X = 4,
            Y = 4,
        });
        if (Editor != null)
        {
            AddInternal(FileText = new CommandFileText(Command.RevealInFileExplorer)
            {
                Font = Font.With(size: 14),
                X = 20,
                Y = 2
            });
            Editor.OnHistoryChange += UpdateEditorInfo;
            UpdateEditorInfo();
        }
    }
    void UpdateEditorInfo()
    {
        var name = Beatmap.Source.MapStoragePath ?? Path.GetFileName(Beatmap.Source.Filename);
        FileText.Text = name + (Editor.Dirty ? " *" : "");
        FileText.AbsolutePath = Beatmap.Source.AbsolutePath;
    }
    class CommandFileText : CommandText, IHasMarkupTooltip
    {
        public string AbsolutePath;
        string IHasMarkupTooltip.MarkupTooltip => $"{MarkupText.Escape(AbsolutePath)}\n{IHasCommand.GetMarkupTooltip(Command)}";
        public CommandFileText(Command command) : base(command)
        {
        }
    }
    CommandFileText FileText;
    WarningIcon Warning;
    protected override void Update()
    {
        var time = Track.CurrentTime;
        var playing = Track.IsRunning;
        if (playing != playingIconPlay)
        {
            playingIconPlay = playing;
            playbackIcon.Icon = playing ? FontAwesome.Solid.Pause : FontAwesome.Solid.Play;
        }
        if (!IsDragged)
            Thumb.X = (float)Track.Percent;
        TimeText.Text = Track.TimeFraction();
        if (Warning == null && Track.Virtual)
        {
            AddInternal(Warning = new WarningIcon(20)
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 2,
                X = -60,
                Depth = 9
            });
        }
        else if (Warning != null && !Track.Virtual)
        {
            RemoveInternal(Warning, true);
            Warning = null;
        }
        base.Update();
    }
    protected override bool OnDragStart(DragStartEvent e)
    {
        if (e.Button == MouseButton.Left) return true;
        return base.OnDragStart(e);
    }
    private void SetScrubPosition(float x)
    {
        x = Math.Clamp(x, 0, 1);
        Thumb.X = x;
        Track.Seek(x * (Track.EndTime + Track.LeadIn) - Track.LeadIn, true);
    }
    protected override bool OnMouseDown(MouseDownEvent e)
    {
        SetScrubPosition((this.Parent.ToSpaceOfOtherDrawable(e.MousePosition, timelineBarContainer).X - timelineBarContainer.Padding.Left) /
            timelineBarContainer.RelativeToAbsoluteFactor.X);
        return true;
    }
    protected override void OnMouseUp(MouseUpEvent e) => Track.CommitAsyncSeek();
    protected override void OnDrag(DragEvent e)
    {
        if (this.IsDisposed) return;
        SetScrubPosition((this.Parent.ToSpaceOfOtherDrawable(e.MousePosition, timelineBarContainer).X - timelineBarContainer.Padding.Left) /
            timelineBarContainer.RelativeToAbsoluteFactor.X);
        base.OnDrag(e);
    }
    protected override void OnDragEnd(DragEndEvent e)
    {
        Track.CommitAsyncSeek();
        base.OnDragEnd(e);
    }
    private class WarningIcon : CommandIconButton, IHasMarkupTooltip
    {
        string IHasMarkupTooltip.MarkupTooltip => $"Audio failed to load. Using virtual track for playback. Please drag + drop an mp3 file to load audio.\nClick for more options\n{IHasCommand.GetMarkupTooltip(Command)}";
        public WarningIcon(float size) : base(Command.FixAudio, FontAwesome.Solid.ExclamationCircle, size)
        {
            Colour = DrumColors.BrightRed;
        }
    }
}

