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
    public double Rate { get => PlaybackSpeed.Value; set => PlaybackSpeed.Value = value; }
    public event Action RunningChanged;
    public bool IsRunning { get; private set; }
    public double? PendingSeek { get; private set; }
    public double PercentAt(double t) => EndTime == 0 ? 0 : (t + LeadIn) / (EndTime + LeadIn);
    public double Percent => PercentAt(CurrentTime);
    public double EndTime => Track?.Length ?? 0; // This is buffered in TrackBass, so not expensive
    protected int AsyncSeeking = 0; // this is to prevent updating CurrentTime while we are waiting on async seek
    public bool IsAsyncSeeking => AsyncSeeking > 0;
    public bool AtEnd { get; private set; }
    public bool IsLoaded = false;
    // TODO we could even pull this directly with a Bass call so it's not limited by the AudioThread
    // Should be a more precise version of CurrentTime - used for scoring
    public double AbsoluteTime => IsRunning && Track.IsRunning && !IsAsyncSeeking ? Track.CurrentTime + LatencyFactor : CurrentTime;
    public AdjustableProperty PreferredTempoAdjustment;
    // frequency adjustment doesn't seem to need this
    double LatencyCorrection => PreferredTempoAdjustment == AdjustableProperty.Tempo ? 60 : 0;
    // incremented on update thread when seeking. Use to handle events that originate from different threads
    public int TimeVersion = 1;
    double LatencyFactor => IsRunning ? -LatencyCorrection * (Rate - 1) : 0;
    public Track Track { get; private set; }
    public Track PrimaryTrack { get; private set; } // only non-null when Track is holding drum only audio
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
    void PitchPreferenceChanged(ValueChangedEvent<bool> e)
    {
        Track?.RemoveAdjustment(PreferredTempoAdjustment, PlaybackSpeed);
        PrimaryTrack?.RemoveAdjustment(PreferredTempoAdjustment, PlaybackSpeed);
        PreferredTempoAdjustment = e.NewValue ? AdjustableProperty.Tempo : AdjustableProperty.Frequency;
        Track?.AddAdjustment(PreferredTempoAdjustment, PlaybackSpeed);
        PrimaryTrack?.AddAdjustment(PreferredTempoAdjustment, PlaybackSpeed);
    }
    public TrackClock(Track track, double leadIn = 0)
    {
        Util.ConfigManager.PreservePitch.BindValueChanged(PitchPreferenceChanged, true);
        _leadIn = leadIn;
        CurrentTime = -leadIn;
        SetupTrack(track);
        Track = track;
        UpdateLoaded();
        Util.CommandController.RegisterHandlers(this);
    }
    public Track AcquireTrack()
    {
        Track res;
        if (PrimaryTrack == null)
        {
            res = Track;
            Track = null;
        }
        else
        {
            res = PrimaryTrack;
            PrimaryTrack = null;
            Track = null;
        }
        DisownTrack(res);
        Dispose();
        return res;
    }
    void InnerRunningChanged()
    {
        if (Track.IsRunning) Start();
        else Stop();
    }
    void DisownTrack(Track track)
    {
        track.RemoveAdjustment(PreferredTempoAdjustment, PlaybackSpeed);
        track.Completed -= OnComplete;
        if (track is YouTubeTrack yt) yt.RunningChanged -= InnerRunningChanged;
    }
    void SetupTrack(Track track)
    {
        track.AddAdjustment(PreferredTempoAdjustment, PlaybackSpeed);
        // track.Balance.Value = -1;
        track.Completed += OnComplete;
        if (track is YouTubeTrack yt) yt.RunningChanged += InnerRunningChanged;
    }
    public void ResumePrimary() // don't return old track here, since the caller should already have a reference
    {
        if (PrimaryTrack == null) return;
        Track.Stop();
        DisownTrack(Track);
        SwapBase(PrimaryTrack);
        PrimaryTrack = null;
    }
    void SwapBase(Track track)
    {
        IsLoaded = track.IsLoaded;
        PendingSeek = null;
        AsyncSeeking = 0;
        Track = track;
        var targetTime = CurrentTime; // track.Length may not be loaded at this point unfortunately
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
    // since this is a temporary swap, we DO NOT own the track here
    // we will try to dispose it anyways, but do not rely on TrackClock disposing
    public void TemporarySwap(Track track)
    {
        if (track == null) return;
        PrimaryTrack = Track;
        PrimaryTrack.Stop();
        Track = track;
        SetupTrack(track);
        SwapBase(track);
    }
    public void SwapTrack(Track track) // this murders the primary and discards the current track
    {
        if (track == null) return;
        if (PrimaryTrack != null)
        {
            DisownTrack(Track);
            Track.Stop();
            DisownTrack(PrimaryTrack);
            PrimaryTrack.Dispose();
            PrimaryTrack = null;
        }
        else
        {
            DisownTrack(Track);
            Track.Dispose();
        }
        SetupTrack(track);
        SwapBase(track);
    }
    private void OnComplete()
    {
        Util.UpdateThread.Scheduler.Add(() => Stop(true));
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
                // we have to check against the previous time, since this can trigger sometimes when the track reaches the end
                // Our local IsRunning variable is updated in a callback from the Completed event, but it waits until the next update cycle
                // to reproduce this bug, add a 10ms delay to the OnComplete handler
                if (CurrentTime < 0 && t >= 0)
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
                Stop(true);
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
        // this has to increment AFTER updating current tiem
        // otherwise a different thread could read the version while we are at 1000
        // then seek finishes and the new time say 0, but the version is still correct from when we pulled 1000

        // by setting it after, we could accidentally pull the time at 0 with the old version.
        // This will just cause the event to be dropped later
        TimeVersion += 1; // don't need Interlocked since this is always on the update thread
        if (IsRunning && !async && !fromHistory) PushState();
        saveState = !async && !IsRunning;
        AfterSeek?.Invoke(CurrentTime);
    }
    public void CommitAsyncSeek() => (Track as YouTubeTrack)?.CommitAsyncSeek();
    protected virtual void SeekCommit(double time)
    {
        OnSeekCommit?.Invoke(time);
    }
    public event Action<double> OnSeekCommit;
    public event Action<double> BeforeSeek;
    public event Action<double> AfterSeek;
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
    public void Stop() => Stop(false);
    public void Stop(bool end)
    {
        if (IsRunning)
        {
            IsRunning = false;
            if (end) AtEnd = true; // probably don't need this line since it's set below, keeping in case of thread issues
            // note, we only update CurrentTime if the inner track is running
            // this is because we could call Stop during the lead in phase (when Track is still paused)
            // if that happens, the inner track's CurrentTime isn't valid
            if (Track.IsRunning)
            {
                Track.Stop();
                // Math.Max needed since LatencyFactor can cause annoying issues
                CurrentTime = Math.Max(Track.CurrentTime, CurrentTime);
            }
            AtEnd = end || CurrentTime >= EndTime;
            RunningChanged?.Invoke();
        }
    }

    public virtual void Dispose()
    {
        Util.CommandController.RemoveHandlers(this);
        Util.ConfigManager.PreservePitch.ValueChanged -= PitchPreferenceChanged;
        OnLoad = null;
        OnSeekCommit = null;
        BeforeSeek = null;
        if (PrimaryTrack != null)
        {
            PrimaryTrack.Completed -= OnComplete;
            PrimaryTrack.Dispose();
        }
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
    public Bindable<double> PlaybackSpeed = new BindableNumber<double>(1)
    {
        // http://bass.radio42.com/help/html/90d034c4-b426-7f7c-4f32-28210a5e6bfb.htm
        // Doc says -95% to 5000%
        MinValue = 0.05f, // f is intentional. o!f compares with a float, setting to 0.05d will cause a crash
        MaxValue = 50
    };
    [CommandHandler] public bool SetPlaybackSpeed(CommandContext context) => context.GetNumber(PlaybackSpeed, "Set Playback Speed", "Speed");
    [CommandHandler] public void IncreasePlaybackSpeed() => PlaybackSpeed.Value *= 1.10f;
    [CommandHandler] public void DecreasePlaybackSpeed() => PlaybackSpeed.Value /= 1.10f;
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

