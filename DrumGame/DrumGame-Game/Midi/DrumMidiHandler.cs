using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Commons.Music.Midi;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using DrumGame.Game.Media;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using ManagedBass;
using ManagedBass.Midi;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Logging;
using osu.Framework.Utils;

namespace DrumGame.Game.Midi;

public class MidiSampleHandler : ISampleHandler
{
    public static MidiSampleHandler Instance = new();
    public double Latency => Util.ConfigManager.MidiOutputOffset.Value;
    public bool BassNative => false;
    public void Play(DrumChannelEvent e) => DrumMidiHandler.SendEvent(new MidiNoteOnEvent(e));
}

public static class DrumMidiHandler
{
    public static int InputPreference(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return -5;
        name = name.Trim();
        var r = 0;
        if (name == "Midi Through Port-0")
            r -= 1;
        if (name == Util.ConfigManager.Get<string>(Stores.DrumGameSetting.PreferredMidiInput)?.Trim())
            r += 100;
        return r;
    }
    public static int OutputPreference(string name, string inputName)
    {
        if (string.IsNullOrWhiteSpace(name)) return -5;
        name = name.Trim();
        var r = 0;
        if (name == "Midi Through Port-0")
            r -= 1;
        if (name == Util.ConfigManager.Get<string>(Stores.DrumGameSetting.PreferredMidiInput)?.Trim())
            r += 2;
        if (name == inputName?.Trim())
            r += 5;
        if (name == Util.ConfigManager.Get<string>(Stores.DrumGameSetting.PreferredMidiOutput)?.Trim())
            r += 100;
        return r;
    }
    public const string Disabled = "Disable";

    public static List<IMidiPortDetails> Inputs { get; private set; }
    public static List<IMidiPortDetails> Outputs { get; private set; }
    public static IMidiInput Input;
    public static IMidiOutput Output;
    public static bool InputFound;
    public static bool OutputFound;

    public static string DeviceString
    {
        get
        {
            var o = "Inputs: ";

            var i = Inputs; // cache these so we know they won't change during iteration
            var os = Outputs;
            var currentInput = Input?.Details?.Id;
            var currentOutput = Output?.Details?.Id;

            if (i == null || i.Count == 0) o += "N/A";
            else o += string.Join(", ", i.Select(e => currentInput != null && e.Id == currentInput ? $"[{e.Name}]" : e.Name));
            o += " | Outputs: ";
            if (os == null || os.Count == 0) o += "N/A";
            else o += string.Join(", ", os.Select(e => currentOutput != null && e.Id == currentOutput ? $"[{e.Name}]" : e.Name));
            return o;
        }
    }

    public static void RefreshMidi() => Util.Host.UpdateThread.Scheduler.Add(() => _ = UpdateInputConnectionAsync(true));

    // could probably use some locks here, but it's probably fine :)
    // try to call only on update thread
    public static async Task UpdateInputConnectionAsync(bool force, bool quiet = false)
    {
        var i = Input;
        Inputs = MidiAccessManager.Default.Inputs.ToList();
        if (!quiet)
            Logger.Log($"Found {Inputs.Count} MIDI input devices", level: LogLevel.Important);
        if (Inputs.Count > 0)
        {
            var target = Inputs.MaxBy(e => InputPreference(e.Name));
            if (i?.Details.Id != target.Id || force) // if we force, we always disconnect the existing input
            {
                if (i != null)
                {
                    Input = null;
                    // if we are reconnecting to the same input, close syncronously
                    Close(i, i.Details.Id == target.Id);
                    i = null;
                }
                InputFound = true;
                try
                {
                    var newInput = await MidiAccessManager.Default.OpenInputAsync(target.Id);
                    Logger.Log($"Connected input to {newInput.Details.Name}", level: LogLevel.Important);
                    Input = newInput;
                    ResetInputState();
                    newInput.MessageReceived += onMessage;
                }
                catch (Exception e) { Logger.Error(e, "Failed to open MIDI input device"); }
            }
        }
    }

    public static async Task UpdateOutputConnectionAsync()
    {
        var inputName = Input?.Details?.Name;
        var o = Output;
        Outputs = MidiAccessManager.Default.Outputs.Where(e => e.Name != "Microsoft GS Wavetable Synth").ToList();
        Logger.Log($"Found {Outputs.Count} MIDI output devices", level: LogLevel.Important);

        var disabled = Util.ConfigManager.Get<string>(Stores.DrumGameSetting.PreferredMidiOutput)?.Trim() == Disabled;


        if (o != null)
        {
            if (!disabled && Outputs.Any(e => e.Id == o.Details.Id)) return; // current output is good to go
            Output = null;
            Close(o);
            o = null;
        }

        if (Outputs.Count == 0 || disabled) return;
        if (inputName != null) Logger.Log($"Searching for output named {inputName}", level: LogLevel.Important);
        var target = Outputs.MaxBy(e => OutputPreference(e.Name, inputName));
        OutputFound = true;
        try
        {
            var newOutput = await MidiAccessManager.Default.OpenOutputAsync(target.Id);
            Logger.Log($"Connected output to {newOutput.Details.Name}", level: LogLevel.Important);
            Output = newOutput;
            if (OutputBuffer != null)
            {
                while (OutputBuffer.TryDequeue(out var res)) SendBytes(res);
                OutputBuffer = null;
            };
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to open MIDI output device");
        }
    }

    static byte? previousEventType = null;
    static byte currentProgram;
    static byte hiRes;
    static byte hiHatPosition; // current position we think the hi hat is in
    static int logErrors;
    // this is the known state of the input MIDI device
    static void ResetInputState()
    {
        previousEventType = null;
        currentProgram = 0;
        hiRes = 0;
        logErrors = 5; // only log first 5 errors
    }

    static void Close(IMidiOutput device)
    {
        Task.Factory.StartNew(device.CloseAsync, TaskCreationOptions.LongRunning);
        Logger.Log($"Disconnected MIDI output device: {device.Details.Name}", level: LogLevel.Important);
    }
    static void Close(IMidiInput device, bool sync = false)
    {
        device.MessageReceived -= onMessage;
        if (sync) device.CloseAsync().Wait();
        else Task.Factory.StartNew(device.CloseAsync, TaskCreationOptions.LongRunning);
        Logger.Log($"Disconnected MIDI input device: {device.Details.Name}", level: LogLevel.Important);
    }


    static void onMessage(object sender, MidiReceivedEventArgs ev)
    {
        var s = (IMidiInput)sender;
        try
        {
            // this parsing logic is very similar to what we have in MidiTrack.cs
            // it would be cool if we could combine them
            // this parser is newer and therefore preferrred
            var d = ev.Data;
            for (var i = ev.Start; i < ev.Start + ev.Length;)
            {
                DrumMidiEvent e = null;

                var eventType = d[i++];
                if (eventType <= 0x7F)
                {
                    if (previousEventType is byte previous)
                    {
                        eventType = previous;
                        i--;
                    }
                    else throw new Exception("MIDI missing event type");
                }
                else previousEventType = eventType;

                var highNibble = (byte)(eventType >> 4);
                // we don't really care about the MIDI channel, so we can ignore the low nibble usually
                var lowNibble = (byte)(eventType & 0xF);

                // 0xB is for random control stuff

                // if (d.Length == 3)
                // {
                //     Console.WriteLine($"{eventType:X2} {d[i]:X2} {d[i + 1]:X2} {ev.Timestamp}");
                // }
                // else
                // {
                //     Console.WriteLine($"{Convert.ToHexString(d)} {ev.Timestamp}");
                // }


                if (highNibble == 0x8) i += 2; // note off, ignore
                else if (highNibble == 0x9)
                {
                    var ne = new MidiNoteOnEvent(d[i++], d[i++], hiRes);
                    hiRes = 0;
                    if (ne.DrumChannel == DrumChannel.OpenHiHat || ne.DrumChannel == DrumChannel.ClosedHiHat)
                        ne.HiHatControl = hiHatPosition;
                    e = ne;
                }
                else if (highNibble == 0xA) // polyphonic key pressure, ie. choking
                {
                    var p1 = d[i++];
                    var p2 = d[i++];
                    e = new MidiPressureEvent(p1, p2);
                }
                else if (highNibble == 0xB)
                {
                    var p1 = d[i++];
                    var p2 = d[i++];
                    if (p1 == 0x58) hiRes = p2;
                    else e = new MidiControlEvent(p1, p2);//Console.WriteLine($"control message {p1:x2} {p2:x2}");
                }
                else if (highNibble == 0xC)
                {
                    e = new ProgramChangeEvent(currentProgram = d[i++]);
                    Logger.Log($"MIDI program changed to {currentProgram + 1}");
                }
                else if (eventType == 0xF8) { } // timing clock
                else if (eventType == 0xFE) { } // active sensing
                else
                {
                    if (ExtraMidiHandler == null)
                        throw new Exception("Unknown MIDI event");
                    else
                    {
                        ExtraMidiHandler(ev.Data.AsSpan(ev.Start, ev.Length).ToArray());
                        break; // we don't want to keep processing this anymore
                    }
                }

                if (e != null)
                {
                    e.Timestamp = ev.Timestamp;
                    OnEvent?.Invoke(e);
                    if (e is MidiNoteOnEvent ne) handleNoteOn(ne);
                    else if (e is MidiAuxEvent ae) handleAux(ae);
                }
            }
        }
        catch (Exception e)
        {
            if (logErrors > 0)
            {
                var bytes = ev.Data.AsSpan(ev.Start..(ev.Start + ev.Length));
                var o = new StringBuilder();
                foreach (var b in bytes) o.Append($"{b:x2} ");
                Logger.Error(e, $"MIDI exception: [{s.Details.Id}: {ev.Timestamp}]: {o}");
                logErrors -= 1;
            }
        }
    }

    // Most modules will display `1` for program ID 0, so make sure to add 1 when sending this to the user
    public record ProgramChangeEvent(byte Program) : DrumMidiEvent
    {
        public override byte[] AsBytes() => new byte[] { 0xC9, Program };
    }
    public abstract record DrumMidiEvent
    {
        public long Timestamp;
        public abstract byte[] AsBytes();
    }
    public static event Action<byte[]> ExtraMidiHandler;
    public static event Action<DrumMidiEvent> OnEvent;
    public delegate bool NoteOnHandler(MidiNoteOnEvent noteOn);
    public delegate bool MidiAuxHandler(MidiAuxEvent ev);

    class MidiHandler
    {
        public NoteOnHandler Handler;
        // raw handlers trigger immediately and can cancel further processing
        // update handlers run on update thread
        public bool Raw;
        public bool BlockSoundfontPlayback;
    }
    static List<MidiHandler> handlers = [];
    public static void AddNoteHandler(NoteOnHandler handler, bool raw = false, bool blockSoundfont = false) => handlers.Add(new MidiHandler
    {
        Handler = handler,
        Raw = raw,
        BlockSoundfontPlayback = blockSoundfont
    });
    public static void RemoveNoteHandler(NoteOnHandler handler) => handlers.RemoveAll(e => e.Handler == handler);
    static List<MidiAuxHandler> auxHandlers = new();
    public static void AddAuxHandler(MidiAuxHandler handler) => auxHandlers.Add(handler);
    public static void RemoveAuxHandler(MidiAuxHandler handler) => auxHandlers.Remove(handler);
    static void handleNoteOn(MidiNoteOnEvent e)
    {
        // ignore velocity 0
        if (e.Velocity <= Util.ConfigManager.MidiThreshold.Value) return;
        Util.InputManager?.HideMouse();
        if (Util.ConfigManager.PlaySamplesFromMidi.Value && !handlers.Any(e => e.BlockSoundfontPlayback) &&
            Util.ConfigManager.Get<bool>(DrumGameSetting.PlaySoundfontOutsideMaps))
        {
            Util.DrumGame.Drumset.Value.LoadedSoundFont?.Play(new DrumChannelEvent(0, DrumChannel.None, e.Velocity)
            {
                Note = e.Note,
                MIDI = true
            });
        }
        var updateHandlers = false;
        // should technically lock the handlers here, oh well
        for (var i = handlers.Count - 1; i >= 0; i--)
        {
            var handler = handlers[i];
            if (handler.Raw)
            {
                if (handler.Handler(e))
                    return; // raw has priority
            }
            else updateHandlers = true;
        }
        if (updateHandlers)
        {
            Util.Host.UpdateThread.Scheduler.Add(() =>
            {
                for (var i = handlers.Count - 1; i >= 0; i--)
                {
                    var handler = handlers[i];
                    if (!handler.Raw)
                        if (handler.Handler(e)) return;
                }
            });
        }
    }

    public static void TriggerRandomNote() => handleNoteOn(new MidiNoteOnEvent((byte)RNG.Next(0, 127), (byte)RNG.Next(1, 127)));

    static void handleAux(MidiAuxEvent e)
    {
        if (Util.ConfigManager.PlaySamplesFromMidi.Value)
        {
            var font = Util.DrumGame.Drumset.Value.LoadedSoundFont;
            // TODO stream control events
        }
        if (e is MidiControlEvent ce)
        {
            if (ce.Control == MidiCC.Foot)
                hiHatPosition = ce.Value;
        }
        for (var i = auxHandlers.Count - 1; i >= 0; i--) if (auxHandlers[i](e)) return;
    }

    public static void SendEvent(DrumMidiEvent e) => SendBytes(e.AsBytes());
    static ConcurrentQueue<byte[]> OutputBuffer;
    public static void SendBytes(byte[] bytes)
    {
        if (Output == null)
        {
            OutputBuffer ??= new();
            OutputBuffer.Enqueue(bytes);
            if (!OutputFound) _ = UpdateOutputConnectionAsync();
        }
        else Output.Send(bytes, 0, bytes.Length, 0);
    }
}

public abstract record MidiAuxEvent : DrumMidiHandler.DrumMidiEvent;
public record MidiControlEvent(byte Control, byte Value) : MidiAuxEvent
{
    public override byte[] AsBytes() => new byte[] { 0xB9, Control, Value };
}

public record MidiPressureEvent(byte Note, byte Value) : MidiAuxEvent
{
    public override byte[] AsBytes() => new byte[] { 0xA9, Note, Value };
}

// Note, hi res velocities above 64 should be treated as 64
public record MidiNoteOnEvent(byte Note, byte Velocity, byte HiResVelocity = 0) : DrumMidiHandler.DrumMidiEvent
{
    public byte[] Control;
    public byte HiHatControl;
    public MidiNoteOnEvent(DrumChannelEvent ev) : this(ev.MidiNote, ev.Velocity, ev.HiResVelocity) // TODO remove
    {
        Control = ev.MidiControl;
    }
    public InputKey InputKey => InputKey.MidiA0 - (int)MidiKey.A0 + Note;
    public MidiKey MidiKey => (MidiKey)Note;
    public float ComputedVelocity => Velocity < 127 ? (HiResVelocity < 64 ? Velocity : Velocity + 0.5f) :
        127 + HiResVelocity * 0.5f;

    // this should match what the TD-27 displays
    public string VelocityString => (Velocity < 127 || HiResVelocity <= 1) ? $"{Velocity}" : $"{Velocity}+{HiResVelocity / 2}";
    public MidiNoteOnEvent(MidiKey note, byte velocity, byte hiRes = 0) : this((byte)note, velocity, hiRes) { }
    public DrumChannel DrumChannel
    {
        get
        {
            var channel = ChannelMapping.MidiMapping(Note);
            if (channel == DrumChannel.OpenHiHat || channel == DrumChannel.ClosedHiHat)
            {
                var range = Util.ConfigManager.HiHatRange.Value;
                if (range.Item1 < 255)
                {
                    if (HiHatControl < range.Item1) return DrumChannel.OpenHiHat;
                    else if (HiHatControl > range.Item2) return DrumChannel.ClosedHiHat;
                    else return DrumChannel.HalfOpenHiHat;
                }
            }
            return channel;
        }
    }
    public override byte[] AsBytes()
    {
        var noteBytes = HiResVelocity == 0 ? new byte[] { 0x99, Note, Velocity } : new byte[] { 0xB9, 0x58, HiResVelocity, 0x99, Note, Velocity };
        if (Control != null)
        {
            var o = new byte[Control.Length + noteBytes.Length];
            Control.CopyTo(o, 0);
            noteBytes.CopyTo(o, Control.Length);
            return o;
        }
        return noteBytes;
    }
    public DrumChannelEvent ToDrumChannelEvent(double time) => new DrumChannelEvent(time, DrumChannel, Velocity)
    {
        HiResVelocity = HiResVelocity,
        MIDI = true,
        Note = Note
    };
}