using System;
using DrumGame.Game.Commands;
using DrumGame.Game.Containers;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Graphics.Video;
using osu.Framework.Input.Events;
using osu.Framework.Timing;
using osuTK;
using osuTK.Input;

namespace DrumGame.Game.Components;

public class SyncedVideo : Video, IHasCommand, IHasContextMenu
{
    public bool DisableClick => true;
    public readonly TrackClock Track;
    public readonly string Path;
    public BindableDouble Offset = new BindableDouble(0);
    public event Action SizeLoaded;
    public bool IsSizeLoaded => VideoSize != Vector2.Zero;
    OffsetClock OffsetClock;
    public SyncedVideo(TrackClock track, string path) : base(path, false)
    {
        Path = path;
        Track = track;
        var offsetClock = OffsetClock = new OffsetClock(track);
        Clock = new FramedClock(offsetClock, false);
        Offset.BindValueChanged(_ =>
        {
            offsetClock.Offset = Offset.Value;
            Seek(offsetClock.CurrentTime);
        }, true);
        track.OnSeekCommit += e =>
        {
            Clock.ProcessFrame(); // this updates the framed clock to the new position, which is required before we seek the video
            Seek(offsetClock.CurrentTime);
        };
        Util.LoadFFmpeg();
    }

    protected override void Update()
    {
        base.Update();
        if (SizeLoaded != null) // something is waiting for our size
        {
            if (VideoSize != Vector2.Zero)
            {
                OffsetClock.Offset = Offset.Value; // make sure offset is correct, can be removed if we fix video not loading
                SizeLoaded();
                SizeLoaded = null;
            }
            else if (!OffsetClock.IsRunning && !Buffering)
            {
                // the video class will only push a frame if the frame's time is after the current playback position
                // the decoder only loads future frames, so if we are paused, the video will never load
                //   as the current playback position is always less than the next frame
                // if we are buffering, then we don't want to keep advancing the clock for no reason
                OffsetClock.Offset += 1;
            }
        }
    }

    public Vector2 VideoSize => GetCurrentDisplaySize();
    double dragChange;
    double dragOffset;
    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button == MouseButton.Left && offsetCanBeDragged) return true;
        return base.OnMouseDown(e);
    }
    protected override bool OnDragStart(DragStartEvent e)
    {
        if (e.Button == MouseButton.Left && offsetCanBeDragged)
        {
            dragChange = 0;
            dragOffset = Offset.Value;
            return true;
        }
        return base.OnDragStart(e);
    }
    protected override void OnDrag(DragEvent e)
    {
        var d = e.MouseDownPosition - e.MousePosition;
        var c = d.Y - d.X;
        dragChange = c;
        Offset.Value = dragOffset + c;
    }

    bool offsetCanBeDragged;
    string IHasMarkupTooltip.MarkupTooltip
    {
        get
        {
            if (!offsetCanBeDragged) return IHasCommand.GetMarkupTooltipNoModify(Command);
            var res = IsDragged ? $"{dragChange:+0;-#}ms" : "Hold left click to adjust offset";
            if (Command != Command.None)
                return $"{IHasCommand.GetMarkupTooltipNoModify(Command)}\n{res}";
            return res;
        }
    }
    public Command Command { get; set; }

    public MenuItem[] ContextMenuItems
    {
        get
        {
            var builder = ContextMenuBuilder.New(this);
            if (!offsetCanBeDragged)
                builder.Add("Enable Offset Adjustment", video => video.offsetCanBeDragged = true);
            else
                builder.Add("Disable Offset Adjustment", video => video.offsetCanBeDragged = false);
            builder.Add("Set Video Offset", video =>
            {
                Util.Palette.RequestNumber("Setting Video Offset", "Video offset", Offset.Value, e => Offset.Value = e);
            });
            if (Track is BeatClock beatClock)
                builder.Add("Set Video Offset to YouTube Offset", e => Offset.Value = beatClock.Beatmap.YouTubeOffset);

            return builder.Build();
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        Track.OnSeekCommit -= Seek;
        base.Dispose(isDisposing);
    }
}

