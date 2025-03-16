using System;
using System.IO;
using DrumGame.Game.API;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Commands;
using DrumGame.Game.Commands.Requests;
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
        if (Util.ConfigManager.Get<bool>(DrumGameSetting.AutoLoadVideo))
        {
            var videoPath = Beatmap.FullAssetPath(Beatmap.Video);
            if (videoPath != null && File.Exists(videoPath))
                LoadVideo();
        }
    }

    bool validLayout = false; // could use a LayoutValue for this instead, but I prefer the override
    protected override bool OnInvalidate(Invalidation invalidation, InvalidationSource source)
    {
        if (invalidation.HasFlagFast(Invalidation.DrawSize)) validLayout = false;
        return base.OnInvalidate(invalidation, source);
    }

    CameraPlaceholder CameraPlaceholder;

    public void InvalidateLayout() => validLayout = false;

    void ValidateLayout()
    {
        if (validLayout) return;
        validLayout = true;

        var areaSize = ChildSize;

        if (Util.Skin.LayoutPreference == Skinning.LayoutPreference.Streaming)
        {
            var cameraSize = new Vector2(areaSize.Y * (float)CameraPlaceholder.AspectRatio, areaSize.Y);

            if (CameraPlaceholder == null)
            {
                Add(CameraPlaceholder = new()
                {
                    RelativeSizeAxes = Axes.Y,
                    Height = 1f
                });
            }
            CameraPlaceholder.Width = cameraSize.X;

            var showInputDisplay = Video == null && InputDisplay != null;
            Drawable rightSide = showInputDisplay ? InputDisplay : Video;
            if (InputDisplay != null)
                InputDisplay.Alpha = showInputDisplay ? 1 : 0;

            if (rightSide != null)
            {
                rightSide.Anchor = Anchor.CentreRight;
                rightSide.Origin = Anchor.CentreRight;
                rightSide.RelativeSizeAxes = Axes.Both;
                rightSide.Width = (areaSize.X - cameraSize.X) / areaSize.X;
                rightSide.Height = 1f;
            }


            return;
        }
        else
        {
            if (CameraPlaceholder != null)
            {
                Remove(CameraPlaceholder, true);
                CameraPlaceholder = null;
            }
        }

        {
            var showInputDisplay = (!HasCamera || Video == null) && InputDisplay != null;
            if (InputDisplay != null)
                InputDisplay.Alpha = showInputDisplay ? 1 : 0;
            if (HasCamera)
            {
                var bottomLeft = (Drawable)Video;
                var videoAndInput = showInputDisplay && bottomLeft != null;
                var cameraSize = CameraFeed.DrawSize;
                if (showInputDisplay)
                {
                    InputDisplay.Anchor = Anchor.TopLeft;
                    InputDisplay.Origin = Anchor.TopLeft;
                    InputDisplay.RelativeSizeAxes = Axes.Y;
                    InputDisplay.Width = areaSize.X - cameraSize.X;
                    InputDisplay.Height = videoAndInput ? 0.5f : 1f;
                }
                if (bottomLeft != null)
                {
                    if (videoAndInput)
                    {
                        bottomLeft.Anchor = Anchor.TopLeft;
                        bottomLeft.Origin = Anchor.TopCentre;
                        bottomLeft.RelativeSizeAxes = Axes.Both;
                        bottomLeft.Width = (areaSize.X - cameraSize.X) / areaSize.X;
                        bottomLeft.Height = 0.5f;
                        bottomLeft.Y = ChildSize.Y / 2f;
                        bottomLeft.X = (areaSize.X - cameraSize.X) / 2;
                    }
                    else
                    {
                        bottomLeft.Anchor = Anchor.CentreLeft;
                        bottomLeft.Origin = Anchor.CentreLeft;
                        bottomLeft.RelativeSizeAxes = Axes.Both;
                        bottomLeft.Width = (areaSize.X - cameraSize.X) / areaSize.X;
                        bottomLeft.Height = 1f;
                        bottomLeft.Y = 0f;
                        bottomLeft.X = 0;
                    }
                }
                if (Replay != null) Replay.Alpha = 0;
            }
        }
    }


    // FlowContainer uses this, so we use it too *shrug*
    protected override void UpdateAfterChildren() => ValidateLayout();

    protected override void Dispose(bool isDisposing)
    {
        CommandController.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }


    // this is the video from the beatmap, such as an anime OP
    public SyncedVideo Video { get; private set; }
    public void LoadVideo() => LoadVideo(Beatmap.FullAssetPath(Beatmap.Video));
    public void LoadVideo(string videoPath)
    {
        if (Video != null) return;
        if (videoPath == null) return;
        if (!File.Exists(videoPath))
        {
            Util.Palette.UserError($"{videoPath} not found");
            return;
        }
        AddInternal(Video = new SyncedVideo(Track, videoPath)
        {
            RelativeSizeAxes = Axes.Both,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            FillMode = FillMode.Fit,
            Depth = -1 // video should be in front of hit display if possible
        });
        Video.Offset.Value = Beatmap.VideoOffset;
        Video.Offset.BindValueChanged(e => Beatmap.VideoOffset = e.NewValue);
        validLayout = false;
    }
    [CommandHandler]
    public void ToggleVideo()
    {
        if (Video == null)
        {
            if (Beatmap.Video == null && Display.Player is BeatmapEditor editor && Beatmap.YouTubeID != null)
            {
                var modal = new ConfirmationModal(() =>
                {
                    var currentFolder = Beatmap.Source.AbsolutePath;
                    var library = Beatmap.Source.Library;
                    var filename = $"{Beatmap.YouTubeID}.mp4";
                    var relativePath = $"video/{filename}";
                    if (!library.IsMain)
                        relativePath = filename;
                    var task = YouTubeDL.DownloadBackground(Beatmap.YouTubeID, Beatmap.FullAssetPath(relativePath), new()
                    {
                        Video = true
                    });
                    task.OnSuccess += _ =>
                    {
                        Schedule(() =>
                        {
                            var old = Beatmap.Video;
                            var oldOffset = Beatmap.VideoOffset;
                            editor.PushChange(() =>
                            {
                                Beatmap.Video = relativePath;
                                Beatmap.VideoOffset = Beatmap.YouTubeOffset;
                            }, () =>
                            {
                                Beatmap.Video = old;
                                Beatmap.VideoOffset = oldOffset;
                            }, $"Set video to {relativePath}");
                            LoadVideo();
                        });
                    };
                },
                "A YouTube video is available, would you like to download it?",
                "This will attempt to use YouTube-DL to download a 720p video")
                {
                    YesText = "Download",
                    NoText = "Cancel"
                };
                Util.Palette.Push(modal);
            }
            else
            {
                LoadVideo();
            }
        }
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
            Depth = -0.9f
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