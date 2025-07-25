using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Channels;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using Newtonsoft.Json;

namespace DrumGame.Game.Beatmaps.Scoring;

// probably better to name DrumInputEvent
public class DrumChannelEvent : IReplayEvent
{
    [JsonConverter(typeof(DrumChannelConverter))]
    public DrumChannel Channel; // this channel might not match HitObject.Channel if there's equivalents involved
    // public NoteModifiers Modifiers; // TODO should add this for custom sticking event support
    public double Time { get; } // ms
    public byte Velocity;
    public byte[] MidiControl; // extra MIDI bytes to send before this event
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public byte HiResVelocity;
    public float ComputedVelocity => Velocity < 127 ? (HiResVelocity < 64 ? Velocity : Velocity + 0.5f) :
        127 + HiResVelocity * 0.5f;
    // If the event was triggered from MIDI, can be set to false for JSON loaded hits since those are part of replays.
    // Replay hits don't really come from MIDI, they come from a scheduler.
    // Only used for deciding if we should play samples.
    // It is expected that a MIDI note will have it's sample played by the MIDI device itself
    [JsonIgnore] public bool MIDI;
    [JsonIgnore] public int TimeVersion; // only needed when created outside of update thread

    // these 2 are used for looking up samples for playback
    [JsonIgnore] public HitObject HitObject; // we only care about HitObject.Data, but that's a struct
    [JsonIgnore] public Beatmap CurrentBeatmap; // need since HitObject.Preset.Sample paths are relative to Beatmap
    public bool NeedsSamplePlayback => !MIDI || Util.ConfigManager.PlaySamplesFromMidi.Value;

    // MIDI note number, 0 if not from MIDI. MIDI bool is separate from this, since `Note` gets serialized and is used for playback
    public byte Note;
    public byte MidiNote => Note == 0 ? Channel.MidiNote() : Note;
    public DrumChannelEvent(double time, DrumChannel channel, byte velocity = 92)
    {
        Time = time;
        Channel = channel;
        Velocity = velocity;
    }
}