using System;
using System.Collections.Generic;
using System.IO;
using DrumGame.Game.Beatmaps.Data;
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
        {DrumChannel.PracticeMetronome, ("assets/metronome/*.wav", 1)},
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

    class BasicHandler : ISampleHandler, ISampleHandlerMixDelay
    {
        public double Latency => 0;
        public bool BassNative => true;
        Sample Sample;
        public NotePreset Preset;
        public double VolumeModifier => Preset?.Volume ?? 1;
        public double PanModifier => Preset?.Pan ?? 0;

        public BasicHandler(Sample sample)
        {
            Sample = sample;
        }

        VolumeController VolumeController => Util.DrumGame.VolumeController;

        public void Play(DrumChannelEvent ev, double delay)
        {
            var timesChecked = 0;
            void playInternal()
            {
                var sampleHandle = Util.Get<int>(Sample, "SampleId");
                if (sampleHandle == 0)
                {
                    // if sample isn't loaded yet, we give it a chance by scheduling on the audio thread
                    // I think it takes a maximum of 2 cycles to load
                    if (timesChecked < 2)
                    {
                        Util.AudioThread.Scheduler.Add(playInternal, true);
                        timesChecked += 1;
                    }
                    else
                    {
                        // should be very rare
                        var channel = Sample.GetChannel();
                        channel.Volume.Value = VolumeModifier;
                        channel.Play();
                    }
                    return;
                }
                if (sampleHandle != 0)
                {
                    // this should be timed perfect down to a singular sample if queued with delay
                    var channelHandle = Bass.SampleGetChannel(sampleHandle, BassFlags.SampleChannelStream | BassFlags.Decode);
                    // Bass.ChannelSetAttribute(channelHandle, ChannelAttribute.Pan, 1);
                    // can't use AggregateVolume here because sample isn't add to the mixer until you call Play()
                    var volume = VolumeController.SampleVolume.ComputedValue
                        * VolumeController.MasterVolume.ComputedValue
                        * VolumeModifier;
                    if (ev is Beatmaps.Practice.PracticeMetronomeEvent pme)
                        volume *= pme.Volume;
                    else if (ev.Channel == DrumChannel.Metronome)
                        volume *= VolumeController.MetronomeVolume.ComputedValue;
                    else
                        volume *= VolumeController.HitVolume.ComputedValue;
                    Bass.ChannelSetAttribute(channelHandle, ChannelAttribute.Volume, volume);
                    var pan = PanModifier;
                    if (pan != 0)
                        Bass.ChannelSetAttribute(channelHandle, ChannelAttribute.Pan, pan);
                    var mixer = Util.TrackMixerHandle;
                    BassMix.MixerAddChannel(mixer, channelHandle, BassFlags.MixerChanBuffer | BassFlags.MixerChanNoRampin, Bass.ChannelSeconds2Bytes(mixer, delay), 0);
                }
            }
            playInternal();
        }
        public void Play(DrumChannelEvent ev) => Play(ev, 0);
    }

    class RawMidiHandler : ISampleHandler
    {
        public bool BassNative => false;
        public double Latency => Util.ConfigManager.MidiOutputOffset.Value;
        public void Play(DrumChannelEvent e) => DrumMidiHandler.SendBytes(e.MidiControl);
    }

    public ISampleHandler PreparePlay(DrumChannelEvent ev)
    {
        // priority is
        //   1. MIDI if the event is a control event
        //   2. Sample file if the beatmap supplied one
        //   3. MIDI out
        //   4. Soundfont
        var channel = ev.Channel;
        if (channel == DrumChannel.None && ev.MIDI && ev.MidiControl != null) // raw MIDI bytes
        {
            if (TryMidi && (DrumMidiHandler.Output != null || !MidiConnectionAttempted))
            {
                if (!MidiConnectionAttempted)
                {
                    MidiConnectionAttempted = true;
                    _ = DrumMidiHandler.UpdateOutputConnectionAsync();
                }
                else return new RawMidiHandler();
            }
        }
        var velocity = ev.Velocity;
        if (velocity == 0) return null;
        ISampleHandler handler = null;
        var preset = ev.HitObject?.Preset;
        var presetSampleFile = preset?.Sample;
        if (presetSampleFile != null && ev.CurrentBeatmap != null)
        {
            // TODO doesn't work for imported double crashes
            if (!sampleCache.TryGetValue(presetSampleFile, out var sample))
            {
                sampleCache[presetSampleFile] = sample = Samples.Get(ev.CurrentBeatmap.Source.FullAssetPath(presetSampleFile));
            }
            if (sample != null)
                return new BasicHandler(sample) { Preset = preset };
        }
        if (channel != DrumChannel.Metronome && channel != DrumChannel.PracticeMetronome)
        {
            if (handler == null && TryMidi && (DrumMidiHandler.Output != null || !MidiConnectionAttempted))
            {
                if (!MidiConnectionAttempted)
                {
                    MidiConnectionAttempted = true;
                    _ = DrumMidiHandler.UpdateOutputConnectionAsync();
                }
                if (DrumMidiHandler.OutputFound)
                    handler = MidiSampleHandler.Instance;
            }
            handler ??= LoadedSoundFont;
        }
        if (handler != null) return handler;

        // this part is only for metronome currently
        {
            if (!ChannelMapping.TryGetValue(channel, out var mapping)) return null;
            // we subtract 1 so that velocity = 127 maps to 126 * x / 127 = x - 1
            // it also helps giving the first sample a more "fair" balanced number of velocities
            int sampleId;
            if (channel == DrumChannel.Metronome || channel == DrumChannel.PracticeMetronome)
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
                // the adjustments here don't really do much since we override them before playing the sample in BasicHandler
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
    }

    public bool Play(DrumChannelEvent ev)
    {
        var handler = PreparePlay(ev);
        if (handler == null) return false;
        handler.Play(ev);
        return true;
    }

    public int PlayAt(DrumChannelEvent ev, TrackClock clock, double targetTime, SyncQueue queue = null)
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
        if (!handler.BassNative) // if we aren't native, we should target the regular track (instead of the mix time track)
        {
            var callback = new SyncCallback((_, _, _, _) =>
            {
                queue?.Remove((trackHandle, sync));
                handler.Play(ev);
            });
            targetTime -= handler.Latency * track.Rate;
            var nextPlayByte = Bass.ChannelSeconds2Bytes(trackHandle, targetTime / 1000);
            sync = BassMix.ChannelSetSync(trackHandle, SyncFlags.Position | SyncFlags.Onetime,
                nextPlayByte, callback.Callback, callback.Handle);
        }
        else
        {
            var nextPlayByte = Bass.ChannelSeconds2Bytes(trackHandle, targetTime / 1000);
            var callback = new SyncCallback((_, _, _, _) =>
            {
                queue?.Remove((trackHandle, sync));
                if (handler is ISampleHandlerMixDelay hMixDelay)
                {
                    var mixerHandle = BassMix.ChannelGetMixer(trackHandle);
                    var rawTime = Bass.ChannelGetPosition(trackHandle);
                    hMixDelay.Play(ev, Bass.ChannelBytes2Seconds(trackHandle, nextPlayByte - rawTime));
                }
                else handler.Play(ev);
            });
            // this callback runs on the mixing thread, giving us perfect timing if we run natively
            sync = BassMix.ChannelSetSync(trackHandle, SyncFlags.Position | SyncFlags.Onetime | SyncFlags.Mixtime, nextPlayByte, callback.Callback, callback.Handle);
        }
        queue?.Add((trackHandle, sync));
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
