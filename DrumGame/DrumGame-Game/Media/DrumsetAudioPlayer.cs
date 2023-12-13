using System;
using System.Collections.Generic;
using System.IO;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Midi;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using ManagedBass;
using ManagedBass.Mix;
using osu.Framework.Audio;
using osu.Framework.Audio.Callbacks;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace DrumGame.Game.Media;

// For playing and loading hit sounds
public class DrumsetAudioPlayer : IDisposable
{
    int seekId = 0; // this goes up by 1 every time we seek - the goal is to clear the queue right before we seek
    public void ClearQueue() => seekId += 1;
    public readonly static Dictionary<DrumChannel, (string, int)> ChannelMapping = new()
    {
        {DrumChannel.Metronome, ("assets/metronome/*.wav", 1)},
    };


    public bool TryMidi = true;
    public bool MidiConnectionAttempted = false;

    private readonly Dictionary<string, Sample> sampleCache = new();
    readonly VolumeController volumeController;
    public readonly ISampleStore Samples;
    public SoundFont SoundFont;
    public SoundFont LoadedSoundFont
    {
        get
        {
            if (SoundFont == null && BassUtil.HasMidi)
            {
                var path = Util.Resources.GetAbsolutePath("soundfonts/main.sf2");
                if (!File.Exists(path)) Util.Palette.ShowMessage($"Failed to locate {path}");
                SoundFont = new SoundFont(path);
            }
            return SoundFont;
        }
    }

    class BasicHandler : ISampleHandler
    {
        public double Latency => 0;
        public bool BassNative => true;
        Sample Sample;

        public BasicHandler(Sample sample)
        {
            Sample = sample;
        }

        VolumeController VolumeController => Util.DrumGame.VolumeController;

        public void Play(DrumChannelEvent ev)
        {
            var sampleHandle = Util.Get<int>(Sample, "SampleId");
            if (sampleHandle != 0)
            {
                // this should be timed perfect down to a singular sample
                var channelHandle = Bass.SampleGetChannel(sampleHandle, BassFlags.SampleChannelStream | BassFlags.Decode);
                // Bass.ChannelSetAttribute(channelHandle, ChannelAttribute.Pan, 1);
                // can't use AggregateVolume here because sample isn't add to the mixer until you call Play()
                var volume = VolumeController.SampleVolume.ComputedValue
                    * VolumeController.MasterVolume.ComputedValue;
                if (ev.Channel == DrumChannel.Metronome)
                    volume *= VolumeController.MetronomeVolume.ComputedValue;
                else
                    volume *= VolumeController.HitVolume.ComputedValue;
                Bass.ChannelSetAttribute(channelHandle, ChannelAttribute.Volume, volume);
                BassMix.MixerAddChannel(Util.TrackMixerHandle, channelHandle, BassFlags.MixerChanBuffer | BassFlags.MixerChanNoRampin);
            }
            else Sample.Play(); // should be very rare
        }
    }

    class RawMidiHandler : ISampleHandler
    {
        public bool BassNative => false;
        public double Latency => Util.ConfigManager.MidiOutputOffset.Value;
        public void Play(DrumChannelEvent e) => DrumMidiHandler.SendBytes(e.MidiControl);
    }

    public ISampleHandler PreparePlay(DrumChannelEvent ev)
    {
        var channel = ev.Channel;
        if (channel == DrumChannel.None && ev.MIDI && ev.MidiControl != null) // raw MIDI bytes
        {
            if (TryMidi && (DrumMidiHandler.Output != null || !MidiConnectionAttempted))
            {
                if (!MidiConnectionAttempted)
                {
                    MidiConnectionAttempted = true;
                    DrumMidiHandler.UpdateOutputConnection();
                }
                else return new RawMidiHandler();
            }
        }
        var velocity = ev.Velocity;
        if (velocity == 0) return null;
        ISampleHandler handler = null;
        if (channel != DrumChannel.Metronome)
        {
            handler = LoadedSoundFont;
            if (handler == null && TryMidi && (DrumMidiHandler.Output != null || !MidiConnectionAttempted))
            {
                if (!MidiConnectionAttempted)
                {
                    MidiConnectionAttempted = true;
                    DrumMidiHandler.UpdateOutputConnection();
                }
                if (DrumMidiHandler.OutputFound)
                    handler = MidiSampleHandler.Instance;
            }
        }
        if (handler != null) return handler;
        if (!ChannelMapping.TryGetValue(channel, out var mapping)) return null;
        // we subtract 1 so that velocity = 127 maps to 126 * x / 127 = x - 1
        // it also helps giving the first sample a more "fair" balanced number of velocities
        int sampleId;
        if (channel == DrumChannel.Metronome)
            sampleId = ev.Velocity;
        else
            sampleId = (velocity - 1) * mapping.Item2 / 127 + 1;
        var filename = mapping.Item1.Replace("*", sampleId.ToString());
        var sample = sampleCache.GetValueOrDefault(filename);
        if (sample == null)
        {
            sample = Samples.Get(filename);
            if (sample == null)
            {
                Logger.Log($"Failed to locate sample: {filename}", level: LogLevel.Error);
                return null;
            }
            if (channel == DrumChannel.Metronome)
            {
                // this could also use aggregate instead of level.
                // instead we use level to verify these events are triggered when muted
                sample.AddAdjustment(AdjustableProperty.Volume, volumeController.MetronomeVolume.Level);
                // sample.AddAdjustment(AdjustableProperty.Balance, new BindableDouble(1));
            }
            else
            {
                sample.AddAdjustment(AdjustableProperty.Volume, volumeController.HitVolume.Aggregate);
            }
            sampleCache[filename] = sample;
        }
        return new BasicHandler(sample);
    }

    public bool Play(DrumChannelEvent ev)
    {
        var handler = PreparePlay(ev);
        if (handler == null) return false;
        handler.Play(ev);
        return true;
    }

    public int PlayAt(DrumChannelEvent ev, TrackClock clock, double targetTime, ConcurrentHashSet<int> queue = null)
    {
        var track = clock.Track;
        var trackHandle = track is TrackBass tb ? Util.Get<int>(tb, "activeStream") : 0;
        var handler = PreparePlay(ev);
        if (handler == null) return 0;
        if (track.IsRunning && track.CurrentTime > targetTime)
        {
            Logger.Log("Skipping event due to missed prefire.", level: LogLevel.Important);
            return 0;
        }
        if (trackHandle == 0) // can't queue if track is dead
        {
            Util.DrumGame.AudioThread.Scheduler.AddDelayed(() => handler?.Play(ev),
                (targetTime - clock.CurrentTime) / clock.Rate - handler.Latency);
            return 0;
        }
        var sync = 0;
        var callback = new SyncCallback((_, _, _, _) =>
        {
            queue?.Remove(sync);
            handler.Play(ev);
        });
        if (!handler.BassNative) // if we aren't native, we should target the regular track (instead of the mix time track)
        {
            targetTime -= handler.Latency * track.Rate;
            var nextPlayByte = Bass.ChannelSeconds2Bytes(trackHandle, targetTime / 1000);
            sync = BassMix.ChannelSetSync(trackHandle, SyncFlags.Position | SyncFlags.Onetime,
                nextPlayByte, callback.Callback, callback.Handle);
        }
        else
        {
            var nextPlayByte = Bass.ChannelSeconds2Bytes(trackHandle, targetTime / 1000);
            // this callback runs on the mixing thread, giving us perfect timing if we run natively
            sync = BassMix.ChannelSetSync(trackHandle, SyncFlags.Position | SyncFlags.Onetime | SyncFlags.Mixtime, nextPlayByte, callback.Callback, callback.Handle);
        }
        queue?.Add(sync);
        return sync;
    }

    public readonly GameHost Host;
    public DrumsetAudioPlayer(ISampleStore lazySamples, GameHost host, CommandController controller, VolumeController volumeController)
    {
        Samples = lazySamples;
        Host = host;
        this.volumeController = volumeController;
        controller.RegisterHandlers(this); // don't need to unregister since the lifetimes here are bound
    }

    public void Dispose()
    {
        SoundFont?.Dispose();
        SoundFont = null;
    }

    [CommandHandler]
    public void ReloadSoundFont()
    {
        SoundFont?.Dispose();
        SoundFont = null;
    }
}
