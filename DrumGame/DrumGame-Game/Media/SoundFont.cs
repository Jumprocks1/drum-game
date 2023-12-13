using System;
using System.Diagnostics;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using DrumGame.Game.Components;
using DrumGame.Game.Midi;
using DrumGame.Game.Utils;
using ManagedBass;
using ManagedBass.Midi;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Logging;

namespace DrumGame.Game.Media;

public class SoundFont : ISampleHandler, IDisposable
{
    public bool BassNative => true;
    bool loaded = false;
    int midiStream;
    int fontId;
    bool disposed = false;

    VolumeController VolumeController => Util.DrumGame.VolumeController;

    void VolumeChanged(double _)
    {
        Bass.ChannelSetAttribute(midiStream, ChannelAttribute.Volume,
            VolumeController.SampleVolume.ComputedValue * VolumeController.HitVolume.ComputedValue
            * VolumeController.MasterVolume.ComputedValue);
    }

    public double Latency { get; set; } = 40;

    public SoundFont(string path)
    {
        Util.DrumGame.AudioThread.Scheduler.Add(() =>
        {
            // see https://github.com/ManagedBass/Demo.WPF/blob/master/src/MidiSynth/MainViewModel.cs
            if (!disposed && !loaded)
            {
                midiStream = BassMidi.CreateStream(1, BassFlags.Default | BassFlags.Decode, 1);
                // Bass.ChannelSetAttribute(midiStream, ChannelAttribute.Pan, 1);

                fontId = BassMidi.FontInit(Util.Resources.GetAbsolutePath(path), 0);
                var font = new MidiFont
                {
                    Handle = fontId,
                    Bank = 0,
                    Preset = -1
                };
                BassMidi.StreamSetFonts(midiStream, new[] { font }, 1);
                BassMidi.StreamEvent(midiStream, 0, MidiEventType.Drums, 1);

                // var presets = BassMidi.FontGetPresets(fontId);
                // for (var a = 0; a < 270; ++a)
                // {
                //     var name = BassMidi.FontGetPreset(fontId, a, 128); // not really sure why drums are in bank 128
                //     if (name != null)
                //         Console.WriteLine($"{a:D3}: {name}");
                // }

                VolumeController.SampleVolume.ComputedValueChanged += VolumeChanged;
                VolumeController.HitVolume.ComputedValueChanged += VolumeChanged;
                VolumeController.MasterVolume.ComputedValueChanged += VolumeChanged;
                VolumeChanged(0);

                ManagedBass.Mix.BassMix.MixerAddChannel(Util.TrackMixerHandle, midiStream, 0);

                // Bass.PluginLoad("bassmidi.dll");
                loaded = true;
            }
        }, false);
    }

    public int Handle => midiStream;

    public void Play(DrumChannelEvent e)
    {
        // Debug.Assert(ThreadSafety.IsAudioThread);
        if (!disposed)
        {
            if (!e.MIDI && e.Channel == DrumChannel.Crash && e.Velocity == Beatmaps.HitObjectData.ComputeVelocity(Beatmaps.NoteModifiers.Accented))
            {
                // for accented crashes, we can play both china and crash instead
                Play(new DrumChannelEvent(e.Time, DrumChannel.Crash));
                Play(new DrumChannelEvent(e.Time, DrumChannel.China));
            }
            else
            {
                var midiNote = e.Channel.MidiNote();
                if (!loaded) // if we aren't loaded, this definitely won't work, so we can try to delay it
                    Util.DrumGame.AudioThread.Scheduler.Add(() =>
                        BassMidi.StreamEvent(midiStream, 0, MidiEventType.Note, BitHelper.MakeWord(midiNote, e.Velocity)));
                else
                    BassMidi.StreamEvent(midiStream, 0, MidiEventType.Note, BitHelper.MakeWord(midiNote, e.Velocity));
            }
        }
    }

    // make sure to run all the methods in here on the AudioThread
    public void Dispose()
    {
        disposed = true;
        VolumeController.SampleVolume.ComputedValueChanged -= VolumeChanged;
        VolumeController.HitVolume.ComputedValueChanged -= VolumeChanged;
        VolumeController.MasterVolume.ComputedValueChanged -= VolumeChanged;
        Util.DrumGame.AudioThread.Scheduler.Add(() =>
        {
            if (loaded)
            {
                Bass.StreamFree(midiStream);
                BassMidi.FontFree(fontId);
            }
        }, false);
    }
}