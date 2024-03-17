using System;
using DrumGame.Game.Media;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Stores.DB;
using DrumGame.Game.Timing;
using NAudio.Wave;
using osu.Framework.Allocation;
using osu.Framework.Timing;
using System.IO;

namespace DrumGame.Game.Beatmaps.Replay;

public class BeatmapReplayPlayer : BeatmapPlayer
{
    MusicNotationBeatmapReplayDisplay ReplayDisplay => (MusicNotationBeatmapReplayDisplay)Display;
    public BeatmapReplay Replay => ReplayDisplay.Replay;
    public ReplayInfo ReplayInfo => ReplayDisplay.ReplayInfo;
    public BeatmapReplayPlayer(Beatmap beatmap, MusicNotationBeatmapReplayDisplay replayDisplay)
        : base(beatmap, replayDisplay, BeatmapOpenMode.Replay)
    {
    }

    protected override bool AutoStart => false;

    protected override bool ShouldTriggerEndScreen =>
        Mode == BeatmapPlayerMode.Replay &&
            (Track.CurrentTime > endTime + EndTimeDelay || Track.CurrentTime >= Track.EndTime);

    [BackgroundDependencyLoader]
    void load()
    {
        Mode = BeatmapPlayerMode.Replay;
        Command.RegisterHandlers(this);
    }

    protected override void Dispose(bool isDisposing)
    {
        Command.RemoveHandlers(this);
        AudioRecorder?.Dispose();
        VideoRecorder?.Dispose();
        base.Dispose(isDisposing);
    }

    protected override void TriggerEndScreen()
    {
        AudioRecorder?.Stop();
        base.TriggerEndScreen();
    }
    IVideoRecorder VideoRecorder;

    [CommandHandler]
    public void RecordVideo()
    {
        VideoRecorder?.Dispose();
        var oldClock = Clock;
        var startTime = -Track.LeadIn;
        var manualClock = new ManualClock { CurrentTime = startTime };
        Clock = new FramedClock(manualClock); // this seems to be all we need to get a nice CLOCK
        VideoRecorder = new VideoRecorder();
        var nextFrame = 0;
        var manualTrack = new TrackManual(Track.EndTime);
        SwapTrack(manualTrack);
        Track.Seek(manualClock.CurrentTime);
        var firstFrame = true;
        // var infoText = new SpriteText // helpful for debugging
        // {
        //     Colour = Colour4.Black,
        //     Text = "Frame -1",
        //     Y = 400,
        //     Depth = -10
        // };
        // AddInternal(infoText);
        var framesAterEnd = 3 * 60;
        VideoRecorder.Start(() =>
        {
            if (firstFrame)
            {
                firstFrame = false;
                Track.Start();
            }
            // infoText.Text = $"Frame {nextFrame}";
            manualClock.CurrentTime = startTime + (double)nextFrame * 1000 / VideoRecorder.FrameRate;
            manualTrack.Seek(manualClock.CurrentTime);
            nextFrame += 1;
            if (Track.AtEnd) framesAterEnd -= 1;
            return framesAterEnd <= 0;
        });
    }

    AsioAudioRecorder AudioRecorder;
    [CommandHandler]
    public bool SetRecordingDevices(CommandContext context)
    {
        var devices = AsioOut.GetDriverNames();
        context.GetString(devices, selected =>
        {
            AudioRecorder = new AsioAudioRecorder(selected);
            AudioRecorder.StartRecording(Resources.GetDirectory("recordings").FullName);
        }, "Select Recording Devices");
        return true;
    }
    [CommandHandler]
    public new bool OpenFile(CommandContext context)
    {
        if (!context.TryGetParameter<string>(out var filename))
            return false;

        var file = new FileInfo(filename);
        return (Display as MusicNotationBeatmapReplayDisplay)?.TryLoadVideos(new[] { file }) ?? false;
    }

    [CommandHandler]
    public void Save()
    {
        Replay.Save(Resources, ReplayInfo.Path);
    }
}