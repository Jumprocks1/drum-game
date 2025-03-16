using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Components;
using DrumGame.Game.Media;
using DrumGame.Game.Modals;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using NAudio.Wave;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.IO.Stores;

namespace DrumGame.Game.Input;

public class InputOffsetWizard : RequestModal
{
    const double ChirpTimeMs = 1000;
    const double MaxOffset = 500; // if the calculated offset is greater than this, assume something is wrong
    MemoryAudioRecorder AudioRecorder;
    SpriteText TrackPosition;
    SpriteText RecentVolume;
    SpriteText RunInfo;
    SpriteText DrumInfo;
    SpriteText Instructions;
    SpriteText Result;
    DrumButton StartButton;

    InputOffsetWizardInputHandler InputHandler;

    static double ToDb(double amp) => Math.Log10(amp) * 20;
    static double FromDb(double db) => Math.Pow(10, db / 20);

    public InputOffsetWizard() : base(new RequestConfig
    {
        Title = "Input Offset Wizard",
    })
    {
        var button = new DrumButton
        {
            Text = "Select Line In",
            AutoSize = true,
            Action = () =>
            {
                var devices = AlsaAudioRecorder.GetDeviceNames().AsList();
                if (devices.Count > 0)
                {
                    Util.CommandController.NewContext().GetString(devices, selected =>
                    {
                        nextPlotFrame = 0;
                        lastNoise = 0;
                        silenceDetected = false;

                        AudioRecorder?.Dispose();
                        AudioRecorder = new MemoryAudioRecorder(selected, new AlsaAudioRecorder(), TimeSpan.FromSeconds(5));
                        AudioRecorder.Start();
                    }, "Select Recording Devices");
                }
            }
        };
        Add(button);
        Add(StartButton = new DrumButton
        {
            Text = "Start Track",
            X = 130,
            AutoSize = true,
        });
        var silenceDbInput = new DrumTextBoxTooltip
        {
            Text = $"{ToDb(SilenceCutoff):0.0}",
            CommitOnFocusLost = true,
            Height = 30,
            Width = 50,
            X = 270,
            MarkupTooltip = "Silence cutoff level in dB.\nRecommend -60dB or lower, but for analog inputs it may need to be set higher"
        };
        silenceDbInput.OnCommit += (_, __) =>
        {
            if (double.TryParse(silenceDbInput.Current.Value, out var value))
                SilenceCutoff = FromDb(value);
            silenceDbInput.Text = $"{ToDb(SilenceCutoff):0.0}";
        };
        Add(silenceDbInput);
        Add(TrackPosition = new SpriteText
        {
            Y = 35
        });
        Add(RecentVolume = new SpriteText
        {
            Y = 55
        });
        Add(RunInfo = new SpriteText { Y = 75 });
        Add(DrumInfo = new SpriteText { Y = 95 });
        Add(Result = new SpriteText { Y = 115 });
        Add(Instructions = new SpriteText { Y = 135, Colour = DrumColors.BrightGreen });
        StopOtherTracks();
    }

    double SilenceCutoff = 0.001;

    // main state
    int nextPlotFrame = 0;
    double maxSample = 0; // not used for any logic, purely for the user to verify their line-in is working
    bool silenceDetected = false; // used to block starting the track
    int lastNoise; // just used to update silenceDetected
    int? chirpLocation;
    int? drumSampleLocation;
    DrumChannelEvent lastDrumEvent;

    void OnDrumTrigger(DrumChannelEvent ev)
    {
        lastDrumEvent = ev;
    }
    void StartTrack()
    {
        TrackStore ??= Util.DrumGame.Audio.GetTrackStore(new StaticStore(_ => WaveFile()));
        Track ??= TrackStore.Get("test.wav");
        TrackClock ??= new TrackClock(Track);
        if (InputHandler == null)
        {
            InputHandler = new(TrackClock);
            InputHandler.OnTrigger += OnDrumTrigger;
        }
        TrackClock.Seek(0);
        TrackClock.Start();

        chirpLocation = null;
        drumSampleLocation = null;
    }
    void UpdateStartButton()
    {
        StartButton.Action = silenceDetected ? StartTrack : null;
        StartButton.MarkupTooltip = silenceDetected ? null : "Please select a recording device and then wait for silence";
    }
    void UpdatePlotData()
    {
        var frameStart = nextPlotFrame;
        var samples = AudioRecorder.ReadAll(frameStart);
        if (samples == null || samples.Length == 0) return;
        nextPlotFrame += samples.Length;

        if (chirpLocation == null && Track != null && Track.IsRunning)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                if (samples[i] > SilenceCutoff)
                {
                    chirpLocation = i + frameStart;
                    silenceDetected = false;
                    break;
                }
            }
        }

        if (chirpLocation != null && silenceDetected)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                if (samples[i] > SilenceCutoff)
                {
                    drumSampleLocation = i + frameStart;
                    silenceDetected = false;
                    break;
                }
            }
        }


        // we update silence detected down here so that it doesn't stop the drum detection
        var max = samples.Max();
        maxSample = Math.Max(maxSample, max);
        if (max > SilenceCutoff)
        {
            lastNoise = nextPlotFrame;
            silenceDetected = false;
        }
        const double MinSilenceSeconds = 0.3;
        if (nextPlotFrame - lastNoise > AudioRecorder.SampleRate * MinSilenceSeconds) silenceDetected = true;
    }
    // if this throws any exceptions, it stops scheduling itself
    void StopOtherTracks()
    {
        try
        {
            var tracks = Util.Get<IEnumerable<object>>(Util.DrumGame.Audio.TrackMixer, "activeChannels");
            foreach (var track in tracks)
            {
                if (track is Track t && t.IsRunning && t != Track) t.Stop();
            }
            Scheduler.AddDelayed(StopOtherTracks, 100);
        }
        catch { }
    }
    protected override void Update()
    {
        TrackPosition.Text = $"Track position: {(Track?.CurrentTime ?? 0) / 1000:0.0}";
        var dt = Clock.ElapsedFrameTime;
        TrackClock?.Update(dt);
        maxSample *= Math.Exp(-dt / 1000 * 3);

        var runInfo = "";
        if (chirpLocation != null)
        {
            runInfo = $"Chirp at: {chirpLocation}";
            if (drumSampleLocation != null)
                runInfo = $"{runInfo}, drum at: {drumSampleLocation}";
        }
        if (AudioRecorder?.InitInfo?.SampleRate is int sr)
            runInfo = $"SR: {sr} - {runInfo}";
        RunInfo.Text = runInfo;
        double? recommendedAdjustment = null;
        var result = "";
        if (lastDrumEvent != null)
        {
            DrumInfo.Text = $"{lastDrumEvent.Channel} at {lastDrumEvent.Time / 1000:0.000}";
            if (drumSampleLocation is int drumSample && chirpLocation is int chirpSample)
            {
                var diffAudio = (double)(drumSample - chirpSample) * 1000 / AudioRecorder.SampleRate;
                var diffOffset = lastDrumEvent.Time - ChirpTimeMs;
                var diff = diffOffset - diffAudio;
                if (Math.Abs(diff) < MaxOffset)
                {
                    recommendedAdjustment = diff;
                    result = $"Recommended offset adjustment: {diff:+0;-0;0}ms";
                }
            }
        }
        Result.Text = result;

        if (AudioRecorder == null)
            Instructions.Text = "Select recording device";
        else if (TrackClock == null || !TrackClock.IsRunning)
            if (!silenceDetected) Instructions.Text = "Wait for silence";
            else Instructions.Text = "Start track";
        else if (chirpLocation == null)
            Instructions.Text = "Wait for chirp";
        else if (!silenceDetected)
            Instructions.Text = "Wait for silence";
        else if (recommendedAdjustment != null)
            Instructions.Text = "Adjust offset or hit drum to repeat assessment";
        else
            Instructions.Text = "Hit drum";


        if (AudioRecorder?.InitInfo != null)
            UpdatePlotData();
        var maxDb = Math.Log10(maxSample) * 20;
        RecentVolume.Text = $"Peak: {maxDb:0.0}dB {(silenceDetected ? "(Silence)" : null)}";
        UpdateStartButton();
        base.Update();
    }
    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (InputHandler != null && InputHandler.Handle(e))
            return true;
        return base.OnKeyDown(e);
    }

    Track Track;
    TrackClock TrackClock;
    ITrackStore TrackStore;
    protected override void Dispose(bool isDisposing)
    {
        TrackStore?.Dispose();
        TrackClock?.Dispose();
        InputHandler?.Dispose();
        Track?.Dispose();
        AudioRecorder?.Dispose();
        AudioRecorder = null;
        base.Dispose(isDisposing);
    }

    public class StaticStore : IResourceStore<byte[]>
    {
        public Func<string, byte[]> GetData;
        public StaticStore(Func<string, byte[]> getData)
        {
            GetData = getData;
        }
        public void Dispose() { }
        public byte[] Get(string name) => GetData(name);
        public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IEnumerable<string> GetAvailableResources() => [];
        public Stream GetStream(string name) => new MemoryStream(GetData(name));
    }
    static byte[] WaveFile()
    {
        const int sampleRate = 44100;
        var lengthSeconds = 60;
        var sampleLength = sampleRate * lengthSeconds;
        var tone = 440;
        var volume = 0.3f;
        var chirpLength = 5; // in full tone cycles
        // TODO this just writes 0 after the start, there's has to be a way to just have an endless stream of 0's or something
        // extra 1000 is for headers
        using var s = new MemoryStream(sampleLength * 4 + 1000);
        using (var writer = new CustomWaveFileWriter(s, WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1)))
        {
            for (var i = 0; i < sampleLength; i++)
            {
                var t = (float)i / sampleRate - (float)ChirpTimeMs / 1000;
                var cycle = t * tone;
                if (cycle > chirpLength || cycle < 0)
                    writer.WriteSample(0f);
                else
                {
                    var decay = t / chirpLength;
                    writer.WriteSample(MathF.Sin(cycle * MathF.Tau) * volume * (1 - decay));
                }
            }
        }
        var res = s.ToArray();
        return res;
    }
}