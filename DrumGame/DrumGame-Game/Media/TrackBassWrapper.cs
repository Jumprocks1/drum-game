using System;
using DrumGame.Game.Utils;
using ManagedBass;
using osu.Framework.Audio.Callbacks;
using osu.Framework.Audio.Track;

namespace DrumGame.Game.Media;

public class TrackBassWrapper : TrackWrapper
{
    public override double AbsoluteTime => GetAbsoluteTime();
    int _handle;
    int Handle
    {
        get
        {
            if (_handle != 0) return _handle;
            return _handle = Util.Get<int>(Track, "activeStream");
        }
    }

    int MixerHandle = BassUtil.MixerHandle();
    double MixerOffset;

    bool _isRunning;
    public override bool IsRunning { get => _isRunning; }

    public TrackBass Track { get; }
    public TrackBassWrapper(TrackBass track) : base(track)
    {
        Track = track;
    }

    object _syncLock = new();

    long queuedMixerSync; // in bytes
    int mixerSyncHandle;
    void UpdateSync()
    {
        lock (_syncLock)
        {
            if (!IsRunning)
            {
                if (mixerSyncHandle != 0)
                {
                    Bass.ChannelRemoveSync(MixerHandle, mixerSyncHandle);
                    mixerSyncHandle = 0;
                }
                return;
            }
            if (CurrentTime >= 0)
            {
                if (!Track.IsRunning)
                    Track.Start();
                return;
            }
            try
            {
                Bass.ChannelLock(MixerHandle, true);
                var mixerBytePos = Bass.ChannelGetPosition(MixerHandle);
                var mixerTime = Bass.ChannelBytes2Seconds(MixerHandle, mixerBytePos) * 1000;
                MixerOffset = mixerTime - CurrentTime;
                var syncTime = MixerOffset / Track.Rate;
                var syncByte = Bass.ChannelSeconds2Bytes(MixerHandle, syncTime / 1000);

                if (queuedMixerSync == syncByte) return;

                if (mixerSyncHandle != 0)
                {
                    Bass.ChannelRemoveSync(MixerHandle, mixerSyncHandle);
                    mixerSyncHandle = 0;
                }
                _ = Track.SeekAsync(0);
                queuedMixerSync = syncByte;
                var localMixerSyncHandle = 0;
                var callback = new SyncCallback((_, _, _, _) =>
                {
                    lock (_syncLock)
                    {
                        if (mixerSyncHandle != localMixerSyncHandle) return;
                        if (Util.Call<bool>(Track, "startInternal"))
                        {
                            Util.Set(Track, "isPlayed", true);
                            Util.Set(Track, "isRunning", true);
                        }
                    }
                });
                var flags = SyncFlags.Position | SyncFlags.Onetime | SyncFlags.Mixtime;
                mixerSyncHandle = localMixerSyncHandle = Bass.ChannelSetSync(MixerHandle, flags,
                    syncByte, callback.Callback, callback.Handle);
            }
            finally
            {
                Bass.ChannelLock(MixerHandle, false);
            }
        }
    }

    public override void Seek(double time)
    {
        lock (_syncLock)
        {
            if (time >= 0)
            {
                Track.Seek(time);
                if (IsRunning && !Track.IsRunning)
                    Track.Start();
                CurrentTime = AbsoluteTime;
            }
            else
            {
                CurrentTime = time;
                UpdateSync();
            }
        }
    }

    public override void Start()
    {
        lock (_syncLock)
        {
            _isRunning = true;
            if (CurrentTime >= 0)
            {
                if (Track.CurrentTime != CurrentTime)
                    Track.Seek(CurrentTime);
                Track.Start();
                CurrentTime = Track.CurrentTime;
            }
            else UpdateSync();
        }
    }

    public override void Stop()
    {
        lock (_syncLock)
        {
            _isRunning = false;
            if (Track.IsRunning)
            {
                Track.Stop();
                CurrentTime = Track.CurrentTime;
            }
        }
    }


    public override void Update()
    {
        lock (_syncLock)
            CurrentTime = AbsoluteTime;
    }

    double GetAbsoluteTime()
    {
        if (!IsRunning) return CurrentTime;
        if (CurrentTime >= 0 && Track.IsRunning)
            return BassUtil.BassMixTime(Handle);
        return (BassUtil.ChannelTime(MixerHandle) - MixerOffset) * Track.Rate;
    }
}