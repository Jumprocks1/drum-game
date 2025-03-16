using System;
using System.Linq;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using DrumGame.Game.Media;
using DrumGame.Game.Modals;
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

    bool HumanizeTiming;
    float TimingStdDev = 5;
    bool HumanizeVelocity;
    float VelocityStdDev = 3;
    float EighthsVelocity = 0.9f;
    float OffBeatVelocity = 0.75f;

    public override void Configure() => Util.Palette.Request(new RequestConfig
    {
        Title = $"Configuring {FullName} Modifier",
        Fields = [
            new BoolFieldConfig {
                Label = "Humanize timing",
                MarkupTooltip = "Whether the timing of Autoplay should be humanized.",
                DefaultValue = HumanizeTiming,
                OnCommit = e => { HumanizeTiming = e; },
            },
            new FloatFieldConfig {
                Label = "Timing standard deviation",
                MarkupTooltip = "How large the standard deviation (in ms) should be for the timing randomizer.",
                RefN = () => ref TimingStdDev
            },
            new BoolFieldConfig {
                Label = "Humanize velocity",
                MarkupTooltip = "Whether the velocity of Autoplay should be humanized.",
                DefaultValue = HumanizeVelocity,
                OnCommit = e => { HumanizeVelocity = e; },
            },
            new FloatFieldConfig {
                Label = "Velocity standard deviation",
                MarkupTooltip = "How large the standard deviation should be for the velocity randomizer.",
                RefN = () => ref VelocityStdDev
            },
            new FloatFieldConfig {
                Label = "Eighth note velocity multiplier",
                MarkupTooltip = "How much the velocity for eighth notes should be multiplied by.",
                RefN = () => ref EighthsVelocity
            },
            new FloatFieldConfig {
                Label = "Off beat note velocity multiplier",
                MarkupTooltip = "How much the velocity for off beat notes should be multiplied by.\nThis includes sixteenth notes and triplets",
                RefN = () => ref OffBeatVelocity
            }
        ],

        // triggering this will update the mod display
        // main thing that changes is the color of the configure button
        OnCommit = _ => TriggerChanged()
    });
    protected override string SerializeData()
    {
        return $"{HumanizeTiming},{TimingStdDev},{HumanizeVelocity},{VelocityStdDev},{EighthsVelocity},{OffBeatVelocity}";
    }
    public override void ApplyData(string data)
    {
        var spl = data.Split(',', 6);
        HumanizeTiming = bool.Parse(spl[0]);
        TimingStdDev = float.Parse(spl[1]);
        HumanizeVelocity = bool.Parse(spl[2]);
        VelocityStdDev = float.Parse(spl[3]);
        EighthsVelocity = float.Parse(spl[4]);
        OffBeatVelocity = float.Parse(spl[5]);
    }


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

        double humanizeTiming(double msTime)
        {
            msTime += Util.RNGNormal(0, TimingStdDev);
            return msTime;
        }
        byte humanizeVelocity(byte v, int tickTime)
        {
            // values are arbitrary, whatever sounded good enough
            if (tickTime % track.Beatmap.TickRate == 0)
            {
                // nothing, fourths
            }
            else if (tickTime % (track.Beatmap.TickRate / 2) == 0)
            {
                // eights
                v = (byte)(v * EighthsVelocity);
            }
            else
            {
                // off time
                // e.g. sixteenths, triplets, flams
                v = (byte)(v * OffBeatVelocity);
            }

            v = (byte)(v + Util.RNGNormal(0, VelocityStdDev));

            return v;
        }
        void play(int tickTime, DrumChannel channel, byte baseVelocity)
        {
            var velocity = baseVelocity;
            if (HumanizeVelocity)
                velocity = humanizeVelocity(velocity, tickTime);
            velocity = Math.Clamp(velocity, (byte)0, (byte)127);

            var msTime = beatmap.MillisecondsFromTick(tickTime);
            if (HumanizeTiming)
                msTime = humanizeTiming(msTime);

            var ev = new DrumChannelEvent(msTime, channel, velocity);
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
                    v = (byte)Math.Floor(v * volumeMultiplier);
                for (var i = 0; i < count; i++)
                    play(o.Time + i * ticksPerHit, o.Data.Channel, v);
            }
            else
            {
                var v = HitObjectData.ComputeVelocity(o.Data.Modifiers);
                if (volumeMultiplier != 1)
                    v = (byte)Math.Floor(v * volumeMultiplier);
                play(o.Time, o.Data.Channel, v);
            }
        })
        {
            OnReset = Queue.UnbindAndClear
        };
        player.Track.RegisterEvents(hitEvents);
        player.Mode = BeatmapPlayerMode.Playing;
    }
    protected override void ModifyInternal(BeatmapPlayer player)
    {
        player.OnLoadComplete += _ => AfterLoad(player);
    }
}