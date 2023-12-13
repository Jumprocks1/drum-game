using System;
using System.Collections.Generic;
using DrumGame.Game.Commands;
using DrumGame.Game.Utils;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Timing;


namespace DrumGame.Game.Timing;

public class TrackClock : IClock, IDisposable
{
    public double RemainingTime => (EndTime - CurrentTime) / Rate;
    public virtual double CurrentTime { get; protected set; }
    public double EffectiveRate => IsRunning ? PlaybackSpeed.Value : 0; // use when calculating offsets
    public double Rate => PlaybackSpeed.Value;
    public event Action RunningChanged;
    public bool IsRunning { get; private set; }
    public double? PendingSeek { get; private set; }
    public double PercentAt(double t) => EndTime == 0 ? 0 : (t + LeadIn) / (EndTime + LeadIn);
    public double Percent => PercentAt(CurrentTime);
    public double EndTime => Track.Length; // This is buffered in TrackBass, so not expensive
    protected int AsyncSeeking = 0; // this is to prevent updating CurrentTime while we are waiting on async seek
    public bool IsAsyncSeeking => AsyncSeeking > 0;
    public bool AtEnd { get; private set; }
    public bool IsLoaded = false;
    // TODO we could even pull this directly with a Bass call so it's not limited by the AudioThread
    // Should be a more precise version of CurrentTime - used for scoring
    public double AbsoluteTime => IsRunning && Track.IsRunning && !IsAsyncSeeking ? Track.CurrentTime + LatencyFactor : CurrentTime;
    static double LatencyCorrection = 40;
    double LatencyFactor => IsRunning ? -LatencyCorrection * (Rate - 1) : 0;
    public Track Track { get; private set; }
    public bool Virtual => Track is TrackVirtual;
    public bool Manual => Track is TrackManual;
    double _leadIn;
    public double LeadIn
    {
        get => _leadIn; set
        {
            _leadIn = value;
            if (CurrentTime < -LeadIn) Seek(-LeadIn);
        }
    }
    public TrackClock(Track track, double leadIn = 0)
    {
        _leadIn = leadIn;
        CurrentTime = -leadIn;
        SetupTrack(track);
        Track = track;
        UpdateLoaded();
        Util.CommandController.RegisterHandlers(this);
    }
    public Track AcquireTrack()
    {
        var track = Track;
        Track = null;
        UnbindEvents(track);
        Dispose();
        return track;
    }
    void InnerRunningChanged()
    {
        if (Track.IsRunning) Start();
        else Stop();
    }
    void UnbindEvents(Track track)
    {
        track.Completed -= OnComplete;
        if (track is YouTubeTrack yt) yt.RunningChanged -= InnerRunningChanged;
    }
    void SetupTrack(Track track)
    {
        track.AddAdjustment(AdjustableProperty.Tempo, PlaybackSpeed);
        // track.Balance.Value = -1;
        track.Completed += OnComplete;
        if (track is YouTubeTrack yt) yt.RunningChanged += InnerRunningChanged;
    }
    public void SwapTrack(Track track)
    {
        if (track == null) return;
        if (track.IsRunning) track.Stop();

        Track.RemoveAdjustment(AdjustableProperty.Tempo, PlaybackSpeed);
        UnbindEvents(Track);
        Track.Dispose();
        SetupTrack(track);
        IsLoaded = track.IsLoaded;
        PendingSeek = null;
        AsyncSeeking = 0;
        Track = track;
        var targetTime = CurrentTime; // track.Length won't be loaded at this point unfortunately
        AtEnd = targetTime == EndTime;
        if (IsRunning && targetTime >= 0)
        {
            track.Seek(targetTime);
            track.Start();
        }
        else
        {
            track.Stop();
            if (targetTime >= 0) PendingSeek = targetTime;
        }
    }
    private void OnComplete()
    {
        AtEnd = true;
        Stop();
    }

    // TODO https://github.com/ppy/osu-framework/issues/4202
    // peppy plans to fix the Length = 0 apparently, which would reduce the need for this event
    private event Action OnLoad;

    public virtual void Update(double dt)
    {
        UpdateLoaded();
        if (IsRunning && AsyncSeeking == 0)
        {
            var looped = false;
            double t;
            // check if we need to start the song after lead in
            if (!Track.IsRunning && !Manual)
            {
                t = CurrentTime + dt * Rate;
                if (t > 0)
                {
                    Track.Seek(0);
                    Track.Start(); // this causes a lag spike unfortunately
                    t = Track.CurrentTime + LatencyFactor;
                }
            }
            else
            {
                t = Track.CurrentTime + LatencyFactor;
            }
            if (LoopEnd.HasValue)
            {
                // may be able to move this to audio thread for better accuracy
                if (t >= LoopEnd.Value)
                {
                    looped = true;
                    Seek(LoopStart.Value, true); // this always updates CurrentTime synchronously
                }
            }
            if (!looped) CurrentTime = t; // careful not to set CurrentTime more than once
        }
    }
    bool saveState = true;
    public void Seek(double time, bool async = false, bool fromHistory = false)
    {
        BeforeSeek?.Invoke(time);
        if (saveState && !fromHistory) PushState();
        if (IsRunning && LoopEnd.HasValue && time > LoopEnd.Value) time = LoopStart.Value;
        time = Math.Clamp(time, -LeadIn, EndTime);
        AtEnd = time >= EndTime;
        if (IsRunning)
        {
            if (AtEnd)
            {
                CurrentTime = time;
                // don't need to seek the track since we aren't allow to `Play` until we have a non-end seek
                Stop();
            }
            else
            {
                if (time < 0)
                {
                    if (CurrentTime >= 0) Track.Stop();
                    CurrentTime = time;
                }
                else
                {
                    var needsStart = CurrentTime < 0;
                    if (async)
                    {
                        var asyncSeek = AsyncSeeking + 1;
                        AsyncSeeking = asyncSeek;
                        CurrentTime = time;
                        Track.SeekAsync(time).ContinueWith(e =>
                        {
                            if (AsyncSeeking == asyncSeek)
                            {
                                AsyncSeeking = 0;
                                if (needsStart && IsRunning) Track.StartAsync();
                            }
                        });
                    }
                    else
                    {
                        Track.Seek(time);
                        if (needsStart) Track.Start();
                        CurrentTime = Track.CurrentTime;
                    }
                }
            }
            SeekCommit(CurrentTime);
        }
        else
        {
            PendingSeek = time;
            CurrentTime = time;
        }
        if (IsRunning && !async && !fromHistory) PushState();
        saveState = !async && !IsRunning;
    }
    public void CommitAsyncSeek() => (Track as YouTubeTrack)?.CommitAsyncSeek();
    protected virtual void SeekCommit(double time)
    {
        OnSeekCommit?.Invoke(time);
    }
    public event Action<double> OnSeekCommit;
    public event Action<double> BeforeSeek;
    public void CommitPendingSeek()
    {
        if (PendingSeek != null)
        {
            if (PendingSeek.Value >= 0)
            {
                Track.Seek(PendingSeek.Value);
                // CurrentTime should already be PendingSeek.Value, but it will likely change slightly after Bass seeking
                CurrentTime = Track.CurrentTime;
            }
            SeekCommit(PendingSeek.Value);
            PendingSeek = null;
        }
    }

    public void Start()
    {
        if (!IsRunning)
        {
            PushState();
            if (AtEnd) return;
            CommitPendingSeek();
            if (CurrentTime >= 0) Track.Start();
            IsRunning = true;
            RunningChanged?.Invoke();
        }
    }
    public void Stop()
    {
        if (IsRunning)
        {
            IsRunning = false;
            if (Track.IsRunning)
            {
                Track.Stop();
                // needed since Latency factor can cause annoying issues
                CurrentTime = Math.Max(Track.CurrentTime, CurrentTime);
            }
            RunningChanged?.Invoke();
        }
    }

    public virtual void Dispose()
    {
        Util.CommandController.RemoveHandlers(this);
        OnLoad = null;
        OnSeekCommit = null;
        BeforeSeek = null;
        if (Track != null)
        {
            Track.Completed -= OnComplete; // probably don't need this?
            Track.Dispose();
        }
    }

    private void UpdateLoaded()
    {
        if (!IsLoaded && Track.IsLoaded)
        {
            AtEnd = CurrentTime == EndTime;
            IsLoaded = true;
            OnLoad?.Invoke();
            OnLoad = null;
        }
    }
    public void AfterLoad(Action action)
    {
        UpdateLoaded();
        if (IsLoaded) action();
        else OnLoad += action;
    }
    public string TimeFraction() => $"{Util.FormatTime(CurrentTime)} / {Util.FormatTime(EndTime)}";

    public double? LoopStart;
    public double? LoopEnd;
    public string LoopSetMessage => $"Loop set from {Util.FormatTime(LoopStart.Value)} to {Util.FormatTime(LoopEnd.Value)}";
    [CommandHandler]
    public bool ABLoop(CommandContext context)
    {
        if (LoopStart.HasValue)
        {
            if (LoopEnd.HasValue)
            {
                LoopStart = LoopEnd = null;
                context.ShowMessage($"Loop cleared");
            }
            else
            {
                if (LoopStart == CurrentTime) // clear
                {
                    LoopStart = null;
                    context.ShowMessage($"Loop cleared");
                }
                else
                {
                    if (LoopStart < CurrentTime) // all good
                    {
                        LoopEnd = CurrentTime;
                    }
                    else  // flip
                    {
                        LoopStart = CurrentTime;
                        LoopEnd = LoopStart;
                    }
                    context.ShowMessage(LoopSetMessage);
                }
            }
        }
        else
        {
            LoopStart = CurrentTime;
            context.ShowMessage($"Loop start set to {Util.FormatTime(CurrentTime)}");
        }
        return true;
    }
    [CommandHandler]
    public bool Close(CommandContext context)
    {
        if (LoopStart.HasValue)
        {
            LoopStart = LoopEnd = null;
            context.ShowMessage($"Loop cleared");
            return true;
        }
        return false;
    }
    [CommandHandler] public void TogglePlayback() { if (IsRunning) Stop(); else Start(); }
    [CommandHandler] public void Pause() => Stop();
    [CommandHandler] public void Play() => Start();
    public Bindable<double> PlaybackSpeed = new Bindable<double>(1);
    [CommandHandler] public bool SetPlaybackSpeed(CommandContext context) => context.GetNumber(PlaybackSpeed, "Set Playback Speed", "Speed");
    [CommandHandler] public void IncreasePlaybackSpeed() => PlaybackSpeed.Value *= 1.10f;
    [CommandHandler] public void DecreasePlaybackSpeed() => PlaybackSpeed.Value = Math.Max(0.05f, PlaybackSpeed.Value / 1.10f);
    [CommandHandler]
    public bool SeekToTime(CommandContext context)
    {
        context.GetString(s =>
        {
            if (Util.ParseTime(s) is double t) Seek(t);
        }, "Seek to time", "Time", Util.FormatTime(CurrentTime));
        return true;
    }


    record TrackState(double time);
    Stack<TrackState> StateStack = new();
    [CommandHandler]
    public void CursorUndo()
    {
        if (IsRunning ? StateStack.TryPeek(out var state) : StateStack.TryPop(out state)) LoadState(state);
    }
    public void PushState()
    {
        var state = new TrackState(CurrentTime);
        if (StateStack.Count == 0 || StateStack.Peek() != state) StateStack.Push(state);
    }
    void LoadState(TrackState state)
    {
        Seek(state.time, fromHistory: true);
    }
    double? MarkedPosition;
    [CommandHandler]
    public void MarkCurrentPosition()
    {
        var old = MarkedPosition;
        MarkedPosition = CurrentTime;
        if (old is double t) Seek(t);
        else Util.Palette.ShowMessage("Position saved. Activate again to return.");
    }
}

