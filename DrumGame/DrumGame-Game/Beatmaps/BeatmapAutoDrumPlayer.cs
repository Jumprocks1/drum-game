using System;
using DrumGame.Game.Media;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Timing;
using osu.Framework.Allocation;
using System.Linq;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Beatmaps.Display;

// Eventually this could be change to run through the input handler (so we get more visuals)
public class BeatmapAutoDrumPlayer
{
    private DrumsetAudioPlayer Drumset;
    BeatClock Track;
    Beatmap Beatmap;
    BeatEventList<HitObject> hitEvents;
    BookmarkVolumeEvent[] VolumeEvents;
    bool _enabled;
    SyncQueue Queue = new();
    public bool Enabled
    {
        get => _enabled; set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (_enabled)
            {
                if (VolumeEvents == null)
                {
                    BookmarkVolumeEvent[] ev() => BookmarkEvent.CreateList(Beatmap).OfType<BookmarkVolumeEvent>().AsArray();
                    VolumeEvents ??= ev();
                    // only add handler once, we never remove it
                    Beatmap.BookmarkUpdated += () => VolumeEvents = ev();
                }
                hitEvents ??= new BeatEventHitObjects(Beatmap, (o, playbackSpeed) =>
                {
                    var volumeMultiplier = Beatmap.GetVolumeMultiplier(VolumeEvents, o);

                    if (o is RollHitObject roll)
                    {
                        var ticksPerHit = Track.Beatmap.TickRate / 8;
                        var count = roll.Duration / ticksPerHit;

                        var v = HitObjectData.ComputeVelocity(o.Data.Modifiers | NoteModifiers.Roll);
                        if (volumeMultiplier != 1)
                            v = (byte)Math.Clamp(Math.Floor(v * volumeMultiplier), 0, 127);

                        var ev = new DrumChannelEvent(0, o.Data.Channel, v);
                        for (var i = 0; i < count; i++)
                        {
                            var tickTime = o.Time + i * ticksPerHit;
                            Drumset.PlayAt(ev, Track, Beatmap.MillisecondsFromTick(o.Time + i * ticksPerHit), Queue);
                        }
                    }
                    else
                    {
                        var v = HitObjectData.ComputeVelocity(o.Data.Modifiers);
                        if (volumeMultiplier != 1)
                            v = (byte)Math.Clamp(Math.Floor(v * volumeMultiplier), 0, 127);

                        var startTime = Beatmap.MillisecondsFromTick(o.Time);
                        Drumset.PlayAt(new DrumChannelEvent(0, o.Data.Channel, v), Track, startTime, Queue);
                    }
                })
                {
                    OnReset = () => Queue.UnbindAndClear(Track.Track)
                };
                Track.RegisterEvents(hitEvents);
            }
            else
            {
                Track.UnregisterEvents(hitEvents);
                Queue.UnbindAndClear(Track.Track);
            }
        }
    }
    public BeatmapAutoDrumPlayer(Beatmap beatmap, BeatClock track)
    {
        Track = track;
        Beatmap = beatmap;
        Drumset = Util.DrumGame.Drumset.Value;
    }
}
