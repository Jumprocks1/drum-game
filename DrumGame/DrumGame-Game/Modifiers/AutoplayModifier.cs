using System;
using System.Linq;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Media;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Modifiers;

public class AutoplayModifier : BeatmapModifier
{
    public new const string Key = "AP";
    public override string Abbreviation => Key;

    public override string FullName => "Autoplay";
    public override bool AllowSaving => true;

    public override string MarkupDescription => "Automatically generates all hit-events to play the current map.";
    // mostly copy pasted from AutoDrumPlayer and a little from BeatmapReplay
    void AfterLoad(BeatmapPlayer player)
    {
        var drumset = Util.DrumGame.Drumset.Value;
        var beatmap = player.Beatmap;
        var track = player.Track;

        BookmarkVolumeEvent[] ev() => BookmarkEvent.CreateList(beatmap).OfType<BookmarkVolumeEvent>().AsArray();
        var volumeEvents = ev();
        // only add handler once, we never remove it
        beatmap.BookmarkUpdated += () => volumeEvents = ev();

        SyncQueue Queue = new();

        void play(DrumChannelEvent ev)
        {
            if (player.BeatmapPlayerInputHandler != null)
                player.BeatmapPlayerInputHandler?.TriggerEventDelayed(ev);
        }

        var hitEvents = new BeatEventHitObjects(player.Beatmap, (o, playbackSpeed) =>
        {
            var volumeMultiplier = beatmap.GetVolumeMultiplier(volumeEvents, o);

            if (o is RollHitObject roll)
            {
                var ticksPerHit = track.Beatmap.TickRate / 8;
                var count = roll.Duration / ticksPerHit;

                var v = HitObjectData.ComputeVelocity(o.Data.Modifiers | NoteModifiers.Roll);
                if (volumeMultiplier != 1)
                    v = (byte)Math.Clamp(Math.Floor(v * volumeMultiplier), 0, 127);

                for (var i = 0; i < count; i++)
                {
                    var tickTime = o.Time + i * ticksPerHit;
                    play(new DrumChannelEvent(beatmap.MillisecondsFromTick(o.Time + i * ticksPerHit), o.Data.Channel, v));
                }
            }
            else
            {
                var v = HitObjectData.ComputeVelocity(o.Data.Modifiers);
                if (volumeMultiplier != 1)
                    v = (byte)Math.Clamp(Math.Floor(v * volumeMultiplier), 0, 127);

                play(new DrumChannelEvent(beatmap.MillisecondsFromTick(o.Time), o.Data.Channel, v));
            }
        })
        {
            OnReset = () => Queue.UnbindAndClear(track.Track)
        };
        player.Track.RegisterEvents(hitEvents);
        player.Mode = BeatmapPlayerMode.Playing;
    }
    protected override void ModifyInternal(BeatmapPlayer player)
    {
        player.OnLoadComplete += _ => AfterLoad(player);
    }
}