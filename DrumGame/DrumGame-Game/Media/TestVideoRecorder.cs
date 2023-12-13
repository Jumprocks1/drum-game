using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using DrumGame.Game.Components;
using DrumGame.Game.Utils;
using osu.Framework.Configuration;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;

namespace DrumGame.Game.Media;

public class TestVideoRecorder : IVideoRecorder
{
    public bool DisposeOnEnd = true;
    public FrameSync FrameSync = FrameSync.Unlimited;
    FrameSync OldFrameSync;
    ExecutionMode OldExecutionMode;

    // This is meant to record video without user input
    // This lets it run at whatever speed it wants
    // return true on last frame
    public void Start(Func<bool> requestNextFrame)
    {
        if (recording) return;
        recording = true;
        var frameworkConfig = Util.DrumGame.FrameworkConfigManager;
        var frameSync = frameworkConfig.GetBindable<FrameSync>(FrameworkSetting.FrameSync);
        OldFrameSync = frameSync.Value;
        var executionMode = frameworkConfig.GetBindable<ExecutionMode>(FrameworkSetting.ExecutionMode);
        OldExecutionMode = executionMode.Value;

        // disable videos so we can decode them separately
        // each decoder is run on a separate thread
        var drawableVideos = Util.FindAll<SyncedVideo>(Util.DrumGame).ToList();
        for (var i = 0; i < drawableVideos.Count; i++)
        {
            var video = drawableVideos[i];
            video.Alpha = 0;
            var parent = video.Parent;
            var syncVideo = new SynchronousVideoPlayer(video);
            typeof(CompositeDrawable).GetMethod("AddInternal", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(parent, new object[] { syncVideo });
        }

        frameSync.Value = FrameSync;
        executionMode.Value = ExecutionMode.SingleThread;


        void CaptureFrame() // run this on the input thread
        {
            Util.Host.UpdateThread.Scheduler.Add(() =>
            {
                if (!recording) return; // disposed
                var shouldEnd = requestNextFrame();
                if (shouldEnd)
                {
                    if (DisposeOnEnd) Dispose();
                    else CloseVideo();
                }
                Thread.Sleep(100);
                Util.Host.InputThread.Scheduler.Add(CaptureFrame);
            });
        }

        // at this point we will be on the single thread that we requested
        Util.Host.InputThread.Scheduler.Add(CaptureFrame, true);
    }

    bool recording = false;

    public int FrameRate => 60;

    public void CloseVideo()
    {
        if (recording)
        {
            recording = false;
            var frameworkConfig = Util.DrumGame.FrameworkConfigManager;
            frameworkConfig.GetBindable<FrameSync>(FrameworkSetting.FrameSync).Value = OldFrameSync;
            frameworkConfig.GetBindable<ExecutionMode>(FrameworkSetting.ExecutionMode).Value = OldExecutionMode;
        }
    }

    public void Dispose() => CloseVideo();
}