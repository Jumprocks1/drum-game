extern alias OriginalManagedBass;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DrumGame.Game.API;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using DrumGame.Game.Utils;
using ManagedBass;
using ManagedBass.Midi;
using osu.Framework.Logging;

using BassFlags = OriginalManagedBass::ManagedBass.BassFlags;

namespace DrumGame.Game.Media;

public class SoundFontRenderer : IDisposable
{
    int fontId;
    bool disposed;

    public Task<List<SoundFontSample>> CompletionTask => CompletionSource.Task;
    TaskCompletionSource<List<SoundFontSample>> CompletionSource = new();
    public SoundFontRenderer(string path)
    {
        Util.DrumGame.AudioThread.Scheduler.Add(() =>
        {
            if (!BassUtil.HasMidi)
            {
                Logger.Log("BASS MIDI not found, soundfont loading failed.", level: LogLevel.Important);
                return;
            }
            fontId = BassMidi.FontInit(Util.Resources.GetAbsolutePath(path), 0);
        }, false);
    }

    double ComputePan(MidiEvent[] events) // returns pan from -100 to 100
    {
        var midiStream = BassMidi.CreateStream(events, 24, BassFlags.Decode);
        var font = new MidiFont
        {
            Handle = fontId,
            Bank = 0,
            Preset = -1
        };
        BassMidi.StreamSetFonts(midiStream, new[] { font }, 1);

        var len = (int)Bass.ChannelGetLength(midiStream);
        var sampleBuffer = new short[len / 2];
        var bytesRead = Bass.ChannelGetData(midiStream, sampleBuffer, len);

        Bass.StreamFree(midiStream);

        var max = 0;
        var maxI = 0;
        for (var i = 0; i < sampleBuffer.Length; i++)
        {
            var s = Math.Abs(sampleBuffer[i]);
            if (s > max)
            {
                maxI = i;
                max = s;
            }
        }
        var j = maxI / 2 * 2;
        var sl = Math.Abs(sampleBuffer[j]);
        var sr = Math.Abs(sampleBuffer[j + 1]);
        var ang = Math.Acos(sl / Math.Sqrt(sl * sl + sr * sr));
        var pan = ang / Math.PI * 400 - 100;
        return pan;
    }

    public class SoundFontSample
    {
        public string Path;
        public double Pan;
        public double Boost = 1; // will always be at least 1
        public bool Rendered;
    }

    List<SoundFontSample> RenderedSamples = new();

    public void Render(string outputPath, byte note, byte velocity, double? chokeDelay)
    {
        var sample = new SoundFontSample { Path = outputPath };
        RenderedSamples.Add(sample);
        if (disposed) return;
        if (File.Exists(outputPath))
        {
            Logger.Log($"Skipping {outputPath}, already exists");
            return;
        }
        if (!BassUtil.HasMidi) return;
        Util.DrumGame.AudioThread.Scheduler.Add(() =>
        {
            if (disposed) return;
            if (File.Exists(outputPath))
            {
                Logger.Log($"Skipping {outputPath}, already exists");
                return;
            }
            var tickRate = 24 * 10;
            var eventList = new List<MidiEvent>();
            eventList.Add(new MidiEvent
            {
                EventType = MidiEventType.Drums,
                Parameter = 1,
            });
            eventList.Add(new MidiEvent
            {
                EventType = MidiEventType.Program,
                Parameter = 2,
            });
            eventList.Add(new MidiEvent
            {
                EventType = MidiEventType.Note,
                Parameter = BitHelper.MakeWord(note, velocity),
            });
            if (chokeDelay is double delay)
            {
                eventList.Add(new MidiEvent
                {
                    EventType = MidiEventType.Note,
                    Parameter = BitHelper.MakeWord(note, 0),
                    // multiply by 2 since default bpm = 120 = 2 beats per second
                    Ticks = (int)(chokeDelay * tickRate * 2)
                });
            }
            // we leave 2 measures of decay, this gets cropped anyways
            eventList.Add(new MidiEvent { EventType = MidiEventType.End, Ticks = tickRate * 8 });

            var events = eventList.ToArray();

            // invert because sound font should really be flipped ?- this comment seems out of date
            sample.Pan = ComputePan(events); // this is expensive unfortunately

            var midiStream = BassMidi.CreateStream(events, tickRate, BassFlags.Decode | BassFlags.Mono);
            var font = new MidiFont
            {
                Handle = fontId,
                Bank = 0,
                Preset = -1
            };
            BassMidi.StreamSetFonts(midiStream, new[] { font }, 1);

            var byteLen = (int)Bass.ChannelGetLength(midiStream);
            var sampleBuffer = new short[byteLen / 2];
            var bytesRead = Bass.ChannelGetData(midiStream, sampleBuffer, byteLen);
            Bass.StreamFree(midiStream);

            var goodLen = sampleBuffer.Length;
            while (goodLen > 0 && sampleBuffer[goodLen - 1] == 0) goodLen--;

            var max = sampleBuffer.Max(e => Math.Abs((int)e));
            sample.Boost = Math.Max(1, (double)short.MaxValue / max);

            // We don't need the AudioThread anymore, so we could move to async code here
            // But we currently rely on the synchronous AudioThread scheduling to decide when we are done
            // This is not a concern since generally this is still pretty fast

            var process = new FFmpegProcess("converting sample");

            // https://stackoverflow.com/questions/11986279/can-ffmpeg-convert-audio-from-raw-pcm-to-wav
            process.AddArguments("-f", "s16le");
            process.AddArguments("-ar", "44.1k");
            process.AddArguments("-ac", "1");
            process.AddInput("-");
            if (sample.Boost > 1) process.AddArguments("-filter:a", $@"volume={sample.Boost}");
            process.AddOutput(outputPath);
            process.AfterStart = proc =>
            {
                proc.StandardInput.BaseStream.Write(MemoryMarshal.AsBytes(sampleBuffer.AsSpan(0, goodLen)));
                proc.StandardInput.Close();
            };
            process.Run(); // could make this async, but would have to adjust WaitForResult

            sample.Rendered = true;
        }, false);
    }

    public List<SoundFontSample> WaitForResult()
    {
        Dispose();
        return CompletionTask.Result;
    }

    public void Dispose()
    {
        if (disposed) return;
        Util.DrumGame.AudioThread.Scheduler.Add(() =>
        {
            if (disposed) return;
            BassMidi.FontFree(fontId);
            disposed = true;
            CompletionSource.SetResult(RenderedSamples);
        }, false);
    }
}