using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Channels;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Modifiers;

public class DoubleBassModifier : BeatmapModifier
{
    public new const string Key = "DB";
    public override string Abbreviation => Key;

    public override string FullName => "Double Bass";
    public override bool AllowSaving => false;

    public override string MarkupDescription => "Converts all bass notes into double bass.\nThis is done by converting quarter/eighth notes into sixteenth notes.\nAll hi-hat pedal notes are removed.";

    protected override void ModifyInternal(BeatmapPlayer player)
    {
        // ~1-2ms currently (3x faster after JIT)
        // using var _ = Util.WriteTime();

        var beatmap = player.Beatmap;
        var tickRate = beatmap.TickRate;
        var newHitObjects = beatmap.HitObjects.ToList();

        newHitObjects.RemoveAll(e => e.Data.Channel == DrumChannel.HiHatPedal);

        var currentBassNotes = new List<int>(); // ticks for all bass hits

        for (var i = 0; i < newHitObjects.Count; i++)
        {
            var hitObject = newHitObjects[i];
            if (hitObject.Channel == DrumChannel.ClosedHiHat)
                newHitObjects[i] = hitObject.With(DrumChannel.OpenHiHat);
            else if (hitObject.Channel == DrumChannel.BassDrum)
            {
                // remove sticking and other modifiers from all bass notes
                if (hitObject.Modifiers != NoteModifiers.None)
                    newHitObjects[i] = hitObject.With(NoteModifiers.None);
                currentBassNotes.Add(hitObject.Time);
            }
        }

        var bassLengths = beatmap.TicksToNoteLengths(currentBassNotes);

        var newNotes = new List<int>();

        bool NotesAt(int tick) // we could replace this with a HashSet check
        {
            return newHitObjects.BinarySearch(new HitObject(tick, DrumChannel.None)) >= 0;
        }

        for (var i = 0; i < currentBassNotes.Count; i++)
        {
            var tick = currentBassNotes[i];
            if (bassLengths[i] == tickRate) // quarter note, safely add 3 notes
            {
                // only insert if there's a note at the start of next beat
                if (NotesAt(tick + tickRate)
                    || (
                        // if there's notes in each spot we want to add, we can add them
                        NotesAt(tick + tickRate / 4) &&
                        NotesAt(tick + tickRate / 2) &&
                        NotesAt(tick + tickRate / 4 * 3)
                    )
                )
                {
                    newNotes.Add(tick + tickRate / 4);
                    newNotes.Add(tick + tickRate / 2);
                    newNotes.Add(tick + tickRate / 4 * 3);
                }
                // we can still insert a sixteenth note if there's room
                else if (NotesAt(tick + tickRate / 2) || NotesAt(tick + tickRate / 4))
                {
                    newNotes.Add(tick + tickRate / 4);
                }
            }
            else if (bassLengths[i] == tickRate / 2) // eighth note
            {
                if (NotesAt(tick + tickRate / 2) || NotesAt(tick + tickRate / 4))
                {
                    newNotes.Add(tick + tickRate / 4);
                }
            }
        }

        var j = 0;
        for (var i = 0; i < newHitObjects.Count; i++)
        {
            var tick = newHitObjects[i].Time;
            if (tick > newNotes[j])
            {
                newHitObjects.Insert(i, new HitObject(newNotes[j], DrumChannel.BassDrum));
                j += 1;
                if (j >= newNotes.Count) break;
            }
        }

        // add final bass note if needed
        if (j < newNotes.Count)
            newHitObjects.Add(new HitObject(newNotes[j], DrumChannel.BassDrum));

        beatmap.HitObjects = newHitObjects;
    }
}