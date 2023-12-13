using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Stores.DB;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Logging;

namespace DrumGame.Game.Beatmaps.Replay;

public class MusicNotationBeatmapReplayDisplay : MusicNotationBeatmapDisplay
{
    public readonly BeatmapReplay Replay;
    public readonly ReplayInfo ReplayInfo;
    public MusicNotationBeatmapReplayDisplay(BeatmapReplay replay, ReplayInfo replayInfo)
    {
        Replay = replay;
        ReplayInfo = replayInfo;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        Util.CommandController.RegisterHandlers(this);
        Replay.Player = Player;
        Track.RegisterEvents(Replay);
        if (Replay.Video != null)
        {
            ToggleVolumeControls(); // hide volume controls for replay
            StatusContainer.Alpha = 0; // hide status text (zoom, speed, etc.)
            ZoomLevel = 0.8;
            LoadCamera();
        }
        if (Beatmap.Video != null)
            AuxDisplay.LoadVideo();
        else if (Beatmap.Image != null)
            AuxDisplay.ShowImage();
    }

    SyncedVideo Camera;
    void LoadCamera()
    {
        Camera = AuxDisplay.LoadCamera(Replay.Video, Replay.VideoOffset);
        if (Camera != null) Camera.Offset.ValueChanged += e => Replay.VideoOffset = e.NewValue;
    }



    [CommandHandler]
    public void LoadReplayVideo()
    {
        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos, Environment.SpecialFolderOption.DoNotVerify);
        var fileList = new DirectoryInfo(videos).GetFiles();
        TryLoadVideos(fileList);
    }
    public bool TryLoadVideos(IEnumerable<FileInfo> files)
    {
        var replayEndTime = ReplayInfo.CompleteTimeLocal;
        var trackEndLength = Math.Min(Beatmap.EndTime() + BeatmapPlayer.EndTimeDelay, Track.EndTime);
        // can't really use ReplayInfo.StartTimeLocal since we don't know where in the song they started
        // we do know where CompleteTime occurs at, so that's why we use this
        var startTime = replayEndTime - TimeSpan.FromMilliseconds(trackEndLength);

        var format = "yyyy-MM-dd HH-mm-ss";
        var enUS = new CultureInfo("en-US");

        // algorithm:
        //   sort videos by name descending (so we check recent videos first)
        //   find first video whose date is before the end time for the replay
        //   write time should be after the replay end
        foreach (var file in files.OrderByDescending(e => e.Name))
        {
            if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(file.Name), format, enUS, DateTimeStyles.None, out var date))
            {
                if (date < replayEndTime)
                {
                    if (file.LastWriteTime > replayEndTime)
                    {
                        Logger.Log($"Found video: {file.Name}", level: LogLevel.Important);
                        Replay.Video = file.FullName;
                        Replay.VideoOffset = (startTime - date).TotalMilliseconds;
                        LoadCamera();
                        return true;
                    }
                    else
                    {
                        Logger.Log($"Found video: {file.Name}, write time not correct", level: LogLevel.Important);
                    }
                }
            }
        }
        return false;
    }

    protected override void Dispose(bool isDisposing)
    {
        Util.CommandController.RemoveHandlers(this);
        Track.UnregisterEvents(Replay);
        base.Dispose(isDisposing);
    }
}
