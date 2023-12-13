
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Midi;
using DrumGame.Game.Stores;
using DrumGame.Game.Stores.DB;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using osu.Framework.Logging;

namespace DrumGame.Game.Beatmaps.Replay;

// meant to be serialized as JSON
public class BeatmapReplay : IBeatEventHandler
{
    [JsonConverter(typeof(ReplayEventConverter))]
    public List<IReplayEvent> Events;
    public string Video;
    public double VideoOffset;
    [JsonIgnore]
    public double Length => Events.Count == 0 ? 0 : Events[Events.Count - 1].Time; // ms
    public int Version { get; set; } = 2; // only used loosely, not intended to be perfect

    public BeatmapReplay(IEnumerable<IReplayEvent> events)
    {
        Events = events.OrderBy(e => e.Time).ToList();
    }

    public BeatmapReplay() { } // for JSON deserializer

    public void Save(FileSystemResources resources, string path)
    {
        var target = resources.GetAbsolutePath(path);
        Logger.Log($"Replay saved to {target}", level: LogLevel.Important);
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        using (var stream = File.Open(target, FileMode.Create, FileAccess.Write))
        using (var writer = new StreamWriter(stream))
        {
            var serializer = new JsonSerializer()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            };
            serializer.Serialize(writer, this);
        }
    }

    public static BeatmapReplay From(FileSystemResources resources, string path)
    {
        var target = resources.GetAbsolutePath(path);
        var serializer = new JsonSerializer
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };
        using var sr = File.OpenText(target);
        return (BeatmapReplay)serializer.Deserialize(sr, typeof(BeatmapReplay));
    }
    public static BeatmapReplay From(FileSystemResources resources, ReplayInfo replayInfo)
        => From(resources, replayInfo.Path);

    [JsonIgnore] public BeatmapPlayer Player;
    const double Prefire = 300;
    int playedThrough = -1;
    double nextBeatTime = -1;
    public void SkipTo(int _, double time)
    {
        playedThrough = Events.BinarySearchThrough(time) - 1;
        nextBeatTime = Events.Count > 0 && playedThrough + 1 < Events.Count ? Events[playedThrough + 1].Time : -1;
    }

    // we use a queue to make sure we send aux events grouped when possible
    // this grouping is technically not perfect since control events with different times can end up in the same group
    List<ReplayAuxEvent> auxQueue = new();
    public void TriggerThrough(int _, BeatClock clock, bool prefire)
    {
        var inputHandler = Player?.BeatmapPlayerInputHandler;
        if (inputHandler != null)
        {
            var time = clock.CurrentTime;
            var playbackSpeed = clock.PlaybackSpeed.Value;
            var triggerThroughTime = prefire ? time + Prefire * playbackSpeed : time;
            while (triggerThroughTime > nextBeatTime && Events.Count > playedThrough + 1)
            {
                playedThrough += 1;
                if (Events[playedThrough] is DrumChannelEvent e)
                {
                    if (auxQueue.Count > 0)
                    {
                        e.MidiControl = auxQueue.SelectMany(e => e.MidiBytes()).ToArray();
                        auxQueue.Clear();
                    }
                    if (prefire) inputHandler.TriggerEventDelayed(e);
                    else inputHandler.TriggerEvent(e, false);
                    nextBeatTime = Events.Count > playedThrough + 1 ? Events[playedThrough + 1].Time : -1;
                }
                else if (Events[playedThrough] is ReplayAuxEvent aux)
                {
                    if (prefire) auxQueue.Add(aux);
                    nextBeatTime = Events.Count > playedThrough + 1 ? Events[playedThrough + 1].Time : -1;
                }
            }
            if (auxQueue.Count > 0)
            {
                var bytes = auxQueue.SelectMany(e => e.MidiBytes()).ToArray();
                inputHandler.SendMidiBytesDelayed(bytes, auxQueue[0].Time);
                auxQueue.Clear();
            }
        }
    }

    public class ReplayAuxEvent : IReplayEvent
    {
        public enum AuxEventType
        {
            Pressure, // choke
            Control // position
        }
        public byte[] MidiBytes()
        {
            if (Type == BeatmapReplay.ReplayAuxEvent.AuxEventType.Control)
                return new MidiControlEvent(D1, D2).AsBytes();
            else
                return new MidiPressureEvent(D1, D2).AsBytes();
        }
        public double Time { get; set; }
        public AuxEventType Type;
        public byte D1;
        public byte D2;
        public ReplayAuxEvent(double time, MidiAuxEvent ev)
        {
            Time = time;
            if (ev is MidiControlEvent ce)
            {
                Type = AuxEventType.Control;
                D1 = ce.Control;
                D2 = ce.Value;
            }
            else if (ev is MidiPressureEvent pe)
            {
                Type = AuxEventType.Pressure;
                D1 = pe.Note;
                D2 = pe.Value;
            }
        }
    }
}

class ReplayEvents
{
    public IEnumerable<DrumChannelEvent> Channel;
    public IEnumerable<BeatmapReplay.ReplayAuxEvent> Aux;
    public ReplayEvents(List<IReplayEvent> events)
    {
        Channel = events.OfType<DrumChannelEvent>();
        Aux = events.OfType<BeatmapReplay.ReplayAuxEvent>();
    }
    public ReplayEvents() { }
    public IEnumerable<IReplayEvent> GetEvents()
    {
        IEnumerable<IReplayEvent> o = Channel;
        if (Aux != null) o = Aux.Concat(o); // aux should fire before regular events
        return o.OrderBy(e => e.Time);
    }
}

internal class ReplayEventConverter : JsonConverter<List<IReplayEvent>>
{
    public override void WriteJson(JsonWriter writer, List<IReplayEvent> value, JsonSerializer serializer)
        => serializer.Serialize(writer, new ReplayEvents(value));
    public override List<IReplayEvent> ReadJson(JsonReader reader, Type objectType, List<IReplayEvent> existingValue, bool hasExistingValue, JsonSerializer serializer)
        => JObject.ReadFrom(reader).ToObject<ReplayEvents>().GetEvents().ToList();
}