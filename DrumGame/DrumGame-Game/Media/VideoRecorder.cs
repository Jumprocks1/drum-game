using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DrumGame.Game.Components;
using DrumGame.Game.Utils;
using FFMediaToolkit.Encoding;
using FFMediaToolkit.Graphics;
using osu.Framework.Configuration;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osuTK.Graphics.ES30;
using LogLevel = osu.Framework.Logging.LogLevel;

namespace DrumGame.Game.Media;

// will need a way to flip this probably
public class VideoFrame
{
    public readonly int Width;
    public readonly int Height;
    public byte[] Bytes;
    public readonly int Index;
    public VideoFrame(int width, int height, byte[] bytes, int index)
    {
        Width = width;
        Height = height;
        Bytes = bytes;
        Index = index;
    }
    public void FlipX()
    {
        var rowSize = Width * 4; // 4 bytes per pixel
        var temp = new byte[rowSize];
        for (var topRow = 0; topRow < Height / 2; topRow++)
        {
            var bottomRow = Height - topRow - 1;
            System.Buffer.BlockCopy(Bytes, topRow * rowSize, temp, 0, rowSize);
            System.Buffer.BlockCopy(Bytes, bottomRow * rowSize, Bytes, topRow * rowSize, rowSize);
            System.Buffer.BlockCopy(temp, 0, Bytes, bottomRow * rowSize, rowSize);
        }
    }
    public Span<byte> Span => Bytes.AsSpan(0, Width * Height * 4);
}

public class VideoRecorder : IVideoRecorder
{
    object videoLock = new();
    public int FrameRate => 60;
    public bool DisposeOnEnd = true;
    public FrameSync FrameSync = FrameSync.Unlimited;
    string OutputPath;
    MediaOutput VideoFile;
    FrameSync OldFrameSync;
    ExecutionMode OldExecutionMode;

    // This is meant to record video without user input
    // This lets it run at whatever speed it wants
    // return true on last frame
    public void Start(Func<bool> requestNextFrame)
    {
        Util.LoadFFmpeg();
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

        Util.Host.InputThread.Scheduler.Add(() =>
        {
            // at this point we will be on the single thread that we requested

            var settings = GetSettings();
            OutputPath = Util.Resources.GetAbsolutePath("video.mp4");
            if (File.Exists(OutputPath)) File.Delete(OutputPath);
            VideoFile = MediaBuilder.CreateContainer(OutputPath).WithVideo(settings).Create();

            var FrameQueue = new ConcurrentBag<VideoFrame>();

            var nextFrame = 0;
            int? finalFrame = null;

            void CaptureFrame() // run this on the input thread
            {
                Util.Host.UpdateThread.Scheduler.Add(() =>
                {
                    if (VideoFile == null) return; // disposed
                    var shouldEnd = requestNextFrame();
                    // run record data at the start of the next Input thread
                    // we have to make sure we run the screenshot after the draw cycle runs
                    // if we schedule on the draw thread, the first frame will be from BEFORE requesting the first frame
                    Util.Host.InputThread.Scheduler.Add(() =>
                    {
                        while (FrameQueue.Count > 10) Thread.Sleep(10);
                        var newIndex = nextFrame++;
                        // this will capture the current GL state (which is after the latest draw thread run)
                        TakeScreenshotPBO(FrameQueue.Add, newIndex);
                        // while the previous screenshot is finishing, we can schedule another
                        if (shouldEnd) finalFrame = newIndex;
                        else CaptureFrame();
                    });
                });
            }
            CaptureFrame();

            // This code is rock solid I think
            Task.Run(() =>
            {
                var localFrames = new List<VideoFrame>();

                var targetIndex = 0;

                bool Consume(VideoFrame frame)
                {
                    frame.FlipX();

                    var data = new ImageData(frame.Span, ImagePixelFormat.Rgba32, frame.Width, frame.Height);
                    lock (videoLock)
                    {
                        if (VideoFile == null) return true;
                        VideoFile.Video.AddFrame(data);
                    }
                    Buffers.ReturnBuffer(frame.Bytes);
                    targetIndex += 1;
                    for (var i = 0; i < localFrames.Count; i++)
                    {
                        frame = localFrames[i];
                        if (localFrames[i].Index == targetIndex)
                        {
                            localFrames.RemoveAt(i);
                            return Consume(frame);
                        }
                    }
                    return finalFrame != null && finalFrame == frame.Index;
                }

                while (true)
                {
                    if (FrameQueue.TryTake(out var frame))
                    {
                        if (frame.Index == targetIndex)
                        {
                            if (Consume(frame)) break;
                        }
                        else localFrames.Add(frame);
                    }
                    else Thread.Sleep(1);
                }
                if (DisposeOnEnd) Dispose();
                else CloseVideo();
            });
        }, true);
    }

    public void CloseVideo()
    {
        if (VideoFile != null)
        {
            // don't want to dispose mid frame
            lock (videoLock)
            {
                VideoFile?.Dispose();
                VideoFile = null;
                Logger.Log($"Recording complete {OutputPath}", level: LogLevel.Important);
            }
            var frameworkConfig = Util.DrumGame.FrameworkConfigManager;
            frameworkConfig.GetBindable<FrameSync>(FrameworkSetting.FrameSync).Value = OldFrameSync;
            frameworkConfig.GetBindable<ExecutionMode>(FrameworkSetting.ExecutionMode).Value = OldExecutionMode;
        }
    }

    public VideoEncoderSettings GetSettings()
    {
        var w = Util.Host.Window.ClientSize.Width;
        var h = Util.Host.Window.ClientSize.Height;
        // w,h have to be divisible by 2 for some reason
        var settings = new VideoEncoderSettings(width: w + w % 2, height: h + h % 2, framerate: FrameRate, codec: VideoCodec.H264);
        settings.EncoderPreset = EncoderPreset.Fast;
        settings.CRF = 17;
        return settings;
    }

    bool disposed = false;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Util.Host.DrawThread.Scheduler.Add(GLDispose);
        CloseVideo();
    }

    BufferBag Buffers = new BufferBag();
    List<(int size, int gl)> PBOs = new();
    List<(int size, int gl)> AvailablePBOs = new();
    void GLDispose()
    {
        Buffers = null;
        AvailablePBOs = null;
        foreach (var pbo in PBOs) GL.DeleteBuffer(pbo.gl);
        PBOs = null;
    }

    // gets and binds a new PBO
    (int size, int gl) GetPBO(int size)
    {
        for (var i = 0; i < AvailablePBOs.Count; i++)
        {
            var pbo = AvailablePBOs[i];
            if (pbo.size >= size)
            {
                AvailablePBOs.RemoveAt(i);
                GL.BindBuffer(BufferTarget.PixelPackBuffer, pbo.gl);
                return pbo;
            }
        }
        var gl = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.PixelPackBuffer, gl);
        // can try different usage hints after we get the full pipeine set up
        GL.BufferData(BufferTarget.PixelPackBuffer, size, (IntPtr)0, BufferUsageHint.StreamRead);
        var newPbo = (size, gl);
        PBOs.Add(newPbo);
        return newPbo;
    }

    // this method should be good to go
    // just note that the output is flipped
    public void TakeScreenshotPBO(Action<VideoFrame> callback, int index)
    {
        if (disposed) return;
        var Window = Util.Host.Window;
        int width = Window.ClientSize.Width;
        int height = Window.ClientSize.Height;

        var bytes = 4 * width * height;

        var pbo = GetPBO(bytes);
        GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr)null);
        // GL.BindBuffer(BufferTarget.PixelPackBuffer, 0); // probably don't need to do this
        var sync = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None);
        void Check()
        {
            if (disposed) return;
            var res = new int[1];
            GL.GetSync(sync, SyncParameterName.SyncStatus, 1, out _, res);
            var e = (EsVersion30)res[0];
            if (e == EsVersion30.Signaled)
            {
                GL.DeleteSync(sync);
                var pixelData = Buffers.GetBuffer(bytes);

                GL.BindBuffer(BufferTarget.PixelPackBuffer, pbo.gl);
                var map = GL.MapBufferRange(BufferTarget.PixelPackBuffer, (IntPtr)0, bytes, BufferAccessMask.MapReadBit);

                Marshal.Copy(map, pixelData, 0, bytes);
                callback(new VideoFrame(width, height, pixelData, index));

                GL.UnmapBuffer(BufferTarget.PixelPackBuffer);
                AvailablePBOs.Add(pbo);
            }
            else Util.Host.DrawThread.Scheduler.Add(Check, true);
        }
        Check();
    }
}