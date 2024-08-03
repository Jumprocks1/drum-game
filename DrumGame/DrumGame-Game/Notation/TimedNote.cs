using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Notation;

// represents a Note (notehead + vertical position) and a time (horizontal position)
public class TimedNote
{
    public double Time; // in quarter notes
    public double Duration; // used for rolls
    public NotePreset Preset;
    public SkinNote Note;
    public NoteModifiers Modifiers;
    public TimedNote(int tickRate, HitObject hitObject)
    {
        Time = (double)hitObject.Time / tickRate;
        if (hitObject is RollHitObject roll)
            Duration = (double)roll.Duration / tickRate;
        Note = Util.Skin.Notation.Channels[hitObject.Data.Channel];
        Modifiers = hitObject.Data.Modifiers;
        Preset = hitObject.Data.Preset;
    }
}
