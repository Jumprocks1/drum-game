using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Commons.Music.Midi;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Formats;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Channels;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Midi;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using Newtonsoft.Json.Linq;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Logging;

namespace DrumGame.Game.Beatmaps;

public class BeatmapDifficultyDefinition
{
    // since this is the only thing we actually save, this will only work with mounted files for now
    public string Name;
    public bool Default;
}

// Shareable beatmap data
// Should only be modified by "edit" style functions (not playback/display)
// When working with beatmap as Json, only the properties of BJson are serialized
public partial class Beatmap : BJson, IHasHitObjects
{
    // constructor should only really be used by JSON load
    public static Beatmap Create() => new() { Id = Guid.NewGuid().ToString(), CreationTimeUtc = DateTime.UtcNow };
    protected Beatmap() { } // avoid calling this
    // if a beat is within this much of an integer, it will count as that integer beat (only used when rounding a beat)
    public const double TimeToBeatFraction = 1 << 10;
    public const double BeatEpsilon = 1 / TimeToBeatFraction;
    // Must be sorted by HitObject.Time
    // make sure not to directly modify any of the objects in here
    // Appending/Removing is fine, so long as you push to history the previous state
    public List<HitObject> HitObjects { get; set; }
    // Must be sorted by TempoChange.Time
    public List<TempoChange> TempoChanges;
    public List<MeasureChange> MeasureChanges;


    // we could include this in the BJson, but for now we'll focus on getting this playable
    // the default difficulty can point to the same list as Beatmap.HitObjects
    // I expect anything related to this dictionary to change
    // The current plan is to only use these with song.ini imports, since those have multiple difficulties in a single file
    public List<BeatmapDifficultyDefinition> DifficultyDefinitions;

    public override double StartOffset
    {
        get => base.StartOffset; set
        {
            // this does get triggered by Newtonsoft, but this is fine since OffsetUpdate is null at the start
            base.StartOffset = value;
            FireOffsetUpdated();
        }
    }

    public double ComputedLeadIn()
    {
        var minimumLeadIn = Util.ConfigManager.Get<double>(Stores.DrumGameSetting.MinimumLeadIn) * 1000;
        if (HitObjects.Count == 0) return LeadIn;
        return Math.Max(LeadIn, -(MillisecondsFromTick(HitObjects[0].Time) - minimumLeadIn));
    }

    public override double LeadIn
    {
        get => base.LeadIn; set
        {
            base.LeadIn = value;
            FireOffsetUpdated();
        }
    }
    public bool UseYouTubeOffset;
    public double CurrentTrackStartOffset => UseYouTubeOffset ? StartOffset + YouTubeOffset : StartOffset;
    public double TotalOffset => CurrentTrackStartOffset;
    public string MapStoragePath => Source.MapStoragePath;

    public new string Id { get => base.Id ?? Source.FilenameNoExt; set => base.Id = value; }
    public double CurrentRelativeVolume => RelativeVolume ?? BJson.DefaultRelativeVolume;
    public event Action OffsetUpdated;
    public void FireOffsetUpdated()
    {
        OffsetUpdated?.Invoke();
        // the "length" in milliseconds is typically affected by offset
        // because of this, we make sure to update this length event
        // an example of this is the "endTime" which is used to track when to show the end screen
        LengthChanged?.Invoke();
    }
    public event Action LengthChanged;
    public event Action AnnotationsUpdated;
    public void FireAnnotationsUpdated() => AnnotationsUpdated?.Invoke();
    public event Action BookmarkUpdated;
    public void FireBookmarkUpdated() => BookmarkUpdated?.Invoke();
    public event Action TempoUpdated;
    public void FireTempoUpdated() => TempoUpdated?.Invoke();
    public event Action MeasuresUpdated;
    public void FireMeasuresUpdated() => MeasuresUpdated?.Invoke();
    public BJsonSource Source;
    public string FullAudioPath() => FullAssetPath(Audio);
    public string YouTubeAudioPath => Util.Resources.YouTubeAudioPath(YouTubeID);
    double _length;
    public double QuarterNotes
    {
        get => _length; set
        {
            if (_length == value) return;
            _length = value;
            LengthChanged?.Invoke();
        }
    }
    public void LoadMissingDefaults()
    {
        // this does not go inside Init since Init isn't even called when loading from non-bjson sources
        Bookmarks ??= new();
        Annotations ??= new();
        HitObjects ??= new();
        TempoChanges ??= new();
        MeasureChanges ??= new();
    }
    // only used after loading from BJson deserialization
    public void Init()
    {
        if (Notes == null) throw new NotSupportedException();
        if (TickRate <= 0)
            TickRate = DefaultTickRate;
        HitObjects = new List<HitObject>();
        if (NotePresets != null)
        {
            foreach (var (key, value) in NotePresets)
                value.Key = key;
        }
        var presets = NotePresets ??= new();
        double t = 0; // in beats
        foreach (var note in Notes)
        {
            var modifiers = note.GetModifiers();
            t = note.Time;
            var roll = note.Duration.HasValue && note.Duration.Value > 0;
            if (roll)
                modifiers |= NoteModifiers.Roll;
            var preset = (note.Preset != null && presets.TryGetValue(note.Preset, out var tempPreset)) ? tempPreset : null;
            var data = new HitObjectData(note.GetDrumChannel(), modifiers, preset);
            if (roll)
            {
                HitObjects.Add(new RollHitObject(TickFromBeat(t), data, TickFromBeat(note.Duration.Value)));
            }
            else
            {
                HitObjects.Add(new HitObject(TickFromBeat(t), data));
            }
        }
        BJsonLoadHelpers.LoadTempo(this, true);
        BJsonLoadHelpers.LoadMeasures(this, true);
        QuarterNotes = Math.Max((int)(t + 1), 4);
        // We use OrderBy instead of List.Sort since OrderBy is stable sort
        HitObjects = HitObjects.OrderBy(e => e.Time).ToList();
        TempoChanges = TempoChanges.OrderBy(e => e.Time).ToList();
        Notes = null;
    }

    public bool TrySetName(string name)
    {
        var nameWithExt = name + ".bjson";
        var index = nameWithExt.IndexOfAny(Path.GetInvalidFileNameChars());
        if (index >= 0)
        {
            Logger.Log($"Failed to rename beatmap, invalid character in file name at position {index}", level: LogLevel.Error);
            return false;
        }
        var path = Path.Combine(Path.GetDirectoryName(Source.Filename), nameWithExt);
        if (File.Exists(path) && path != Source.OriginalAbsolutePath)
        {
            Logger.Log($"Failed to rename beatmap, file already exists at {path}", level: LogLevel.Error);
            return false;
        }
        // since we're changing the name and setting the format, we need to remove old format tags
        if (Source.Format != null && Source.Format != BJsonFormat.Instance)
        {
            RemoveTags(Source.Format.MountTag);
            AddTags(Source.Format.ConvertTag);
        }
        Source = new BJsonSource(path, BJsonFormat.Instance)
        {
            MapStoragePath = Source.MapStoragePath
        };
        if (Source.MapStoragePath != null)
        {
            // We have to be careful here. When using libraries, the game uses $library/ as the prefix,
            //     but on Windows, path methods will convert this to $library\
            // MapStoragePaths are generated in MapLibraries.GetMaps
            // I'm open to less messy ways of fixing this
            Source.MapStoragePath = Path.Combine(Path.GetDirectoryName(Source.MapStoragePath), nameWithExt);
            if (Source.MapStoragePath.StartsWith("$"))
            {
                var correctedPath = Source.MapStoragePath.ToCharArray();
                // replaces first slash with `/`
                for (var i = 0; i < correctedPath.Length; i++)
                {
                    if (correctedPath[i] == '/') break;
                    else if (correctedPath[i] == '\\')
                    {
                        correctedPath[i] = '/';
                        break;
                    }
                }
                Source.MapStoragePath = new string(correctedPath);
            }
        }
        return true;
    }
    public BJson Export()
    {
        // This just updates ourself to make sure our BJson data matches our Beatmap data
        // Should basically be the inverse of Init()
        Notes = new();
        if (NotePresets != null)
        {
            foreach (var (key, preset) in NotePresets)
                preset.Key = key;
        }
        foreach (var hitObject in HitObjects)
        {
            Notes.Add(new BJsonNote
            {
                Time = (double)hitObject.Time / TickRate,
                Channel = BJsonNote.GetChannelString(hitObject.Data.Channel),
                Modifier = BJsonNote.ModifierString(hitObject.Data.Modifiers),
                Sticking = BJsonNote.StickingString(hitObject.Data.Modifiers),
                Preset = hitObject.Data.Preset?.Key,
                Duration = hitObject is RollHitObject roll ? (double)roll.Duration / TickRate : null
            });
        }
        BPM = Util.ListToToken(TempoChanges, e => JObject.FromObject(
            new
            {
                time = (double)e.Time / TickRate,
                bpm = e.Tempo.HumanBPM
            }), e => e.Tempo.HumanBPM);

        MeasureConfig = Util.ListToToken(MeasureChanges, e => JObject.FromObject(
            e.Time == 0 ? new { beats = e.Beats } : new
            {
                time = (double)e.Time / TickRate,
                beats = e.Beats
            }));
        ComputeStats();
        return this;
    }

    public static double RoundBeat(double beat, double e = TimeToBeatFraction) => Math.Round(beat * e) / e;
    public static double FloorBeat(double beat, double e = TimeToBeatFraction) => Math.Floor((beat + BeatEpsilon) * e) / e;
}
public struct Tempo
{
    public int MicrosecondsPerQuarterNote;
    public readonly double MillisecondsPerQuarterNote => MicrosecondsPerQuarterNote / 1000.0;
    public double BPM
    {
        readonly get => (double)60_000_000 / MicrosecondsPerQuarterNote;
        set => MicrosecondsPerQuarterNote = MicrosecondsFromBPM(value);
    }
    public readonly double HumanBPM
    {
        get
        {
            var bpm = BPM;
            // 3 decimal places is near the limit for MicrosecondsPerQuarterNote
            // ex: 179.999bpm = 333335, 180bpm = 333333, 180.001bpm = 333331
            var rounded = Math.Round(bpm, 3);
            // if we set the BPM to this rounded value, we must get the correct MicrosecondPerQuarterNote
            if (MicrosecondsFromBPM(rounded) == MicrosecondsPerQuarterNote) return rounded;
            return bpm;
        }
    }
    public static int MicrosecondsFromBPM(double bpm) => (int)(60_000_000.0 / bpm + 0.5);
    public override readonly string ToString() => HumanBPM.ToString();
}
public readonly struct HitObjectData
{
    public readonly DrumChannel Channel;
    public readonly NoteModifiers Modifiers;
    public readonly NotePreset Preset; // this might be a bit sketchy having ref in a struct, not sure
    public readonly NoteModifiers VelocityModifiers => Modifiers & (NoteModifiers.AccentedGhost | NoteModifiers.Roll);
    public static byte ComputeVelocity(NoteModifiers modifiers)
    {
        if (modifiers.HasFlagFast(NoteModifiers.Roll))
        {
            if (modifiers.HasFlagFast(NoteModifiers.Accented)) return 75;
            if (modifiers.HasFlagFast(NoteModifiers.Ghost)) return 40;
            return 62;
        }
        if (modifiers.HasFlagFast(NoteModifiers.Accented)) return 120;
        if (modifiers.HasFlagFast(NoteModifiers.Ghost)) return 68;
        return 92;
    }
    public byte Velocity => ComputeVelocity(Modifiers);
    public HitObjectData(DrumChannel channel, NoteModifiers modifiers = NoteModifiers.None, NotePreset preset = null)
    {
        Channel = channel;
        Modifiers = modifiers;
        Preset = preset;
    }

    public override bool Equals(object obj) => obj is HitObjectData other && this.Equals(other);
    public bool Equals(HitObjectData other) => Channel == other.Channel && Modifiers == other.Modifiers && other.Preset == Preset;
    public static bool operator ==(HitObjectData lhs, HitObjectData rhs) => lhs.Equals(rhs);
    public static bool operator !=(HitObjectData lhs, HitObjectData rhs) => !(lhs == rhs);
    // I don't think this is ever used
    public override int GetHashCode() => (Channel, Modifiers, Preset, Velocity).GetHashCode();

    public override string ToString() => $"{Channel}";
}
[Flags]
public enum NoteModifiers // these flags are used in shaders as ints, avoid changing the values
{
    None = 0,
    Accented = 1,
    Ghost = 2,
    AccentedGhost = Accented | Ghost, // should be used for clearing flags
    Roll = 4, // only used for setting velocity to quiet during rapid events in a roll
    Left = 8,
    Right = 16,
    LeftRight = Left | Right, // should be used for clearing flags
}
public class HitObjectRealTime : IComparable<double>
{
    public double Time;
    // note, the beatmap scorer re-sorts these objects sometimes, so this index may not match the current list
    // it should however match back to the HitObjects list on the Beatmap, as long as no changes were made
    public int OriginalObjectIndex;
    public HitObjectData Data;
    public double Duration;
    public bool IsRoll => Duration > 0;
    public HitObjectRealTime(double time, HitObject ho)
    {
        Time = time;
        Data = ho.Data;
    }
    public int CompareTo(double other) => Time.CompareTo(other);
    public override string ToString() => $"{Data.Channel}:{Time}";
    public DrumChannel Channel => Data.Channel;
}
// these are never directly serialized to Json, see BJsonNote
public class HitObject : ITickTime, IComparable<HitObject>, IMidiEvent
{
    public int Time { get; } // ticks
    public bool Roll => this is RollHitObject;
    public readonly HitObjectData Data;
    public NotePreset Preset => Data.Preset;
    public DrumChannel Channel => Data.Channel;
    public NoteModifiers Modifiers => Data.Modifiers;
    public HitObject() { }
    public HitObject(int time, HitObjectData data)
    {
        Time = time;
        Data = data;
    }
    public HitObject(int time, DrumChannel channel)
    {
        Time = time;
        Data = new HitObjectData(channel);
    }
    public NoteModifiers Sticking => Data.Modifiers & NoteModifiers.LeftRight;
    public int CompareTo(HitObject other) => this.Time - other.Time;
    public int CompareTo(int other) => this.Time - other;
    public HitObject With(HitObjectData data) => new(Time, data);
    public HitObject With(DrumChannel channel) => new(Time, new HitObjectData(channel, Data.Modifiers, Data.Preset));
    public HitObject With(NoteModifiers modifiers) => new(Time, new HitObjectData(Data.Channel, modifiers, Data.Preset));
    public HitObject Left() => new(Time, new HitObjectData(Data.Channel, modifiers: Modifiers & ~NoteModifiers.Right | NoteModifiers.Left, Data.Preset));
    public HitObject Right() => new(Time, new HitObjectData(Data.Channel, modifiers: Modifiers & ~NoteModifiers.Left | NoteModifiers.Right, Data.Preset));
    public virtual HitObject WithTime(int time) => new(time, Data);
    public byte MidiChannel => Data.Channel.MidiNote();
    public MidiEvent MidiEvent()
    {
        return new MidiEvent((byte)(Commons.Music.Midi.MidiEvent.NoteOn | MidiExport.MidiDrumChannel),
            MidiChannel, (byte)Data.Velocity, null, 0, 0);
    }
    public bool IsFoot => Data.Channel.IsFoot();
    public bool Voice => Data.Channel.IsFoot();
}
public class RollHitObject : HitObject
{
    public readonly int Duration;
    public RollHitObject(int time, HitObjectData data, int duration) : base(time, data)
    {
        Duration = duration;
    }
    public override HitObject WithTime(int time) => new RollHitObject(time, Data, Duration);
    public RollHitObject WithDuration(int duration) => new(Time, Data, duration);
}

public interface IBeatTime : IComparable<IBeatTime>
{
    public double Time { get; }
    int IComparable<IBeatTime>.CompareTo(IBeatTime other) => Time.CompareTo(other.Time);
}

