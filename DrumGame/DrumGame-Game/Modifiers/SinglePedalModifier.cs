
using System.Collections.Generic;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Channels;

namespace DrumGame.Game.Modifiers;

public class SinglePedalModifier : BeatmapModifier
{
    public new const string Key = "1P";
    public override string Abbreviation => Key;

    public override string FullName => "Single Pedal";
    public override bool AllowSaving => false;

    public override string MarkupDescription => "Removes all sixteenth (or faster) bass notes.\nKeeps all notes that line up on eighth note divisors.\nIn the future I will be adding more configuration for this.";

    // TODO we should add some configuration
    // Should be able to keep double taps since those can be done with a single pedal
    // Should add option to remove triplet or a different divisor
    // I think it's fine to add configuration as instance fields, just make sure they serialize and parse

    // could also just use our set bass sticking and remove all left notes, don't think that's as good though
    protected override void ModifyInternal(BeatmapPlayer player)
    {
        var beatmap = player.Beatmap;
        var originalHitObjects = beatmap.HitObjects;
        var tickRate = beatmap.TickRate;
        var eighthTick = tickRate / 2;

        var bassTicks = new List<int>(); // ticks for all bass hits
        foreach (var hit in originalHitObjects)
        {
            if (hit.Channel == DrumChannel.BassDrum)
                bassTicks.Add(hit.Time);
        }
        var lengths = new (int left, int right)[bassTicks.Count];
        for (var i = 0; i < bassTicks.Count; i++)
        {
            var left = i == 0 ? int.MaxValue : bassTicks[i] - bassTicks[i - 1];
            var right = i == bassTicks.Count - 1 ? int.MaxValue : bassTicks[i + 1] - bassTicks[i];
            lengths[i] = (left, right);
        }
        var removeTicks = new HashSet<int>();
        for (var i = 0; i < lengths.Length; i++)
        {
            var tick = bassTicks[i];
            if (tick % eighthTick != 0) // don't remove any eighth notes
            {
                var v = lengths[i];
                if (v.left < eighthTick)
                {
                    removeTicks.Add(tick);
                    if (i + 1 < lengths.Length)
                        lengths[i + 1].left += v.left;
                }
                else if (v.right < eighthTick && (bassTicks[i + 1] % eighthTick == 0))
                {
                    removeTicks.Add(tick);
                    if (i + 1 < lengths.Length)
                        lengths[i + 1].left += v.left;
                }
            }
        }

        var newHitObjects = new List<HitObject>();
        for (var i = 0; i < originalHitObjects.Count; i++)
        {
            var hitObject = originalHitObjects[i];
            var add = true;
            if (hitObject.Channel == DrumChannel.BassDrum)
            {
                // remove sticking
                if (hitObject.Modifiers != NoteModifiers.None)
                    hitObject = hitObject.With(hitObject.Modifiers & ~NoteModifiers.LeftRight);
                if (removeTicks.Contains(hitObject.Time))
                    add = false;
            }
            if (add)
                newHitObjects.Add(hitObject);
        }
        beatmap.HitObjects = newHitObjects;
    }
}