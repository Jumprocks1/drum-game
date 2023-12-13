using System;
using System.IO;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Notation;
using DrumGame.Game.Stores;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Layout;
using osu.Framework.Logging;
using osuTK;

namespace DrumGame.Game.Beatmaps.Display;

// Could use a FlowContainer instead, but my brain is too small to understand how that all works
public class BeatmapAuxDisplay : Container
{
    CommandController CommandController => Util.CommandController;
    Beatmap Beatmap => Display.Beatmap;
    BeatClock Track => Display.Track;
    BeatmapDisplay Display;

    public BeatmapAuxDisplay(BeatmapDisplay display)
    {
        Display = display;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        CommandController.RegisterHandlers(this);
    }

    bool validLayout = false; // could use a LayoutValue for this instead, but I prefer the override
    protected override bool OnInvalidate(Invalidation invalidation, InvalidationSource source)
    {
        if (invalidation.HasFlagFast(Invalidation.DrawSize)) validLayout = false;
        return base.OnInvalidate(invalidation, source);
    }


    // FlowContainer uses this, so we use it too *shrug*
    protected override void UpdateAfterChildren()
    {
        if (!validLayout)
        {
            validLayout = true;
            if (HasCamera)
            {
                var bottomLeft = (Drawable)Video ?? Image;
                var videoAndInput = InputDisplay != null && bottomLeft != null;
                var areaSize = ChildSize;
                var cameraSize = CameraFeed.DrawSize;
                if (InputDisplay != null)
                {
                    InputDisplay.Anchor = Anchor.TopLeft;
                    InputDisplay.Origin = Anchor.TopLeft;
                    InputDisplay.RelativeSizeAxes = Axes.Y;
                    InputDisplay.Width = areaSize.X - cameraSize.X;
                    InputDisplay.Height = videoAndInput ? 0.5f : 1f;
                }
                if (bottomLeft != null)
                {
                    bottomLeft.Anchor = Anchor.TopLeft;
                    bottomLeft.Origin = Anchor.TopCentre;
                    bottomLeft.RelativeSizeAxes = Axes.Both;
                    bottomLeft.Width = (areaSize.X - cameraSize.X) / areaSize.X;
                    bottomLeft.Height = videoAndInput ? 0.5f : 1f;
                    bottomLeft.Y = videoAndInput ? ChildSize.Y / 2f : 0f;
                    bottomLeft.X = (areaSize.X - cameraSize.X) / 2;
                }
                if (Replay != null) Replay.Alpha = 0;
            }
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        CommandController.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }


    // this is the video from the beatmap, such as an anime OP
    public SyncedVideo Video { get; private set; }
    public void LoadVideo()
    {
        if (Video == null)
        {
            if (Beatmap.Video != null)
            {
                var videoPath = Beatmap.FullAssetPath(Beatmap.Video);
                if (!File.Exists(videoPath))
                {
                    Logger.Log($"{videoPath} not found", level: LogLevel.Error);
                    return;
                }
                AddInternal(Video = new SyncedVideo(Track, videoPath)
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    FillMode = FillMode.Fit
                });
                Video.Offset.Value = Beatmap.VideoOffset;
                Video.Offset.BindValueChanged(e => Beatmap.VideoOffset = e.NewValue);
            }
        }
    }
    Sprite Image;
    public void ShowImage()
    {
        if (Video != null) ToggleVideo();
        var path = Util.MapStorage.GetFullPath(Beatmap.Image);
        var texture = Util.Resources.LargeTextures.Get(path);
        AddInternal(Image = new Sprite
        {
            RelativeSizeAxes = Axes.Both,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            FillMode = FillMode.Fit,
            Texture = texture
        });
        validLayout = false;
    }
    [CommandHandler]
    public void ToggleVideo()
    {
        if (Video == null) LoadVideo();
        else
        {
            RemoveInternal(Video, true);
            Video = null;
            validLayout = false;
        }
    }

    public BeatmapPlayerInputDisplay InputDisplay;
    public void SetInputHandler(bool show)
    {
        if (show) Add(InputDisplay = new BeatmapPlayerInputDisplay());
        else this.Destroy(ref InputDisplay);
        validLayout = false;
    }

    NoteContainer Replay;
    public void SetReplay(NoteContainer replay)
    {
        if (replay == null) this.Destroy(ref Replay);
        else Add(Replay = replay);
        validLayout = false;
    }


    bool HasCamera => CameraFeed != null && CameraFeed.VideoSize != Vector2.Zero;

    public SyncedVideo LoadCamera(string path, double offset, Action onLoad = null)
    {
        RemoveCamera();
        if (path == null) return null;
        AddInternal(CameraFeed = new SyncedVideo(Track, path)
        {
            RelativeSizeAxes = Axes.Both,
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            FillMode = FillMode.Fit,
            Depth = -1
        });
        CameraFeed.Offset.Value = offset;
        CameraFeed.SizeLoaded += () => validLayout = false;
        if (onLoad != null) CameraFeed.SizeLoaded += onLoad;
        CameraFeed.Command = Command.SetCameraOffset;
        CommandController.RegisterHandler(Command.SetCameraOffset, SetCameraOffset);
        return CameraFeed;
    }

    public void RemoveCamera()
    {
        if (CameraFeed != null)
        {
            CommandController.RemoveHandler(Command.SetCameraOffset, SetCameraOffset);
            RemoveInternal(CameraFeed, true);
            CameraFeed = null;
        }
    }

    private SyncedVideo CameraFeed; // eventually this could also be made into the 3d modal viewer
    [CommandHandler]
    public bool AddCameraFeed(CommandContext context)
    {
        if (CameraFeed == null)
        {
            context.GetFile(e => LoadCamera(e, 0), "Video File", "Drag and drop a video file to load it as a camera feed.");
        }
        else
        {
            RemoveCamera();
        }
        validLayout = false;
        return true;
    }

    public bool SetCameraOffset(CommandContext context)
    {
        context.GetNumber(CameraFeed.Offset, "Setting Camera Feed Offset", "Offset");
        return true;
    }
}