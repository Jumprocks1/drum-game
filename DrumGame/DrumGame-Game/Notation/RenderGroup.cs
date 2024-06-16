using System;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Utils;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Notation;

public partial class MusicFont
{
    public void RenderGroup(Container container, NoteGroup group, double groupOffset, Container outside)
    {
        var down = group.Down;
        var dir = down ? 1 : -1;
        var flags = group.Flags;

        // this is the height where we will draw the top bar of the group
        var targetHeight = group.HighestNote / 2f + (down ? StemHeight : -StemHeight);
        // left-most flag position, used for drawing top beam
        var beamLeft = float.MaxValue;
        // right-most flag position, used for drawing top beam
        var beamRight = float.MinValue;

        for (var i = 0; i < flags.Count; i++)
        {
            // Compute final flag values before rendering
            var flag = flags[i];
            if (i == flags.Count - 1)
            {
                flag.EffectiveDuration = group.GroupEnd - flags[i].Time;
            }
            else
            {
                flag.EffectiveDuration = flags[i + 1].Time - flags[i].Time;
            }

            // anchor offset for the bottom notehead of this flag

            // we use the X value for the standard notehead for consistency
            // this is to prevent the flag from shifting when different noteheads appears as the bottom note
            // this is really bad for the ride-bell notehead
            // good example: bbbbb (sixteenth notes)
            //               s s s
            // where b is ride bell, s is snare - the spacing on the ride notes will be awful
            var bottomAnchor = (GetNoteheadAnchor(MusicGlyph.noteheadBlack, down).x,
                GetNoteheadAnchor(flag.BottomNote.Note.Notehead, down).y);

            // this is the X position of the bottom note
            // all other notes in this flag group will be positioned based on this
            var bottomX = (float)((flag.Time - groupOffset) * Spacing);

            var bottomNote = flag.BottomNote;
            flag.FlagLeft = bottomX + bottomAnchor.x;
            if (!down) flag.FlagLeft -= EngravingDefaults.stemThickness;

            foreach (var note in flag.Notes)
            {
                var skinNote = note.Note;

                // we don't care about the y-anchor since that is only used for the bottom note drawing the stem
                var anchor = GetNoteheadAnchor(skinNote.Notehead, down).x;

                // left-most part of the notehead
                var noteX = bottomX + bottomAnchor.x - anchor;
                var l = note.Modifiers.HasFlag(NoteModifiers.Left);
                var r = note.Modifiers.HasFlag(NoteModifiers.Right);
                var ghost = note.Modifiers.HasFlag(NoteModifiers.Ghost);

                var noteheadColor = skinNote.Color;
                if (skinNote.AccentColor != default && note.Modifiers.HasFlag(NoteModifiers.Accented))
                    noteheadColor = skinNote.AccentColor;
                else if (skinNote.GhostColor != default && ghost)
                    noteheadColor = skinNote.GhostColor;

                if (skinNote.StickingColorNotehead)
                {
                    if (l)
                        noteheadColor = skinNote.LeftColor;
                    else if (r)
                        noteheadColor = skinNote.RightColor;
                }

                container.Add(Notehead(skinNote, noteX, noteheadColor));
                if ((l || r) && !skinNote.StickingColorNotehead)
                {
                    var hollow = skinNote.IsHollow();
                    container.Add(new SpriteText
                    {
                        Text = l ? "L" : "R",
                        Origin = Anchor.Centre,
                        Colour = l ? skinNote.LeftColor : skinNote.RightColor,
                        Depth = -skinNote.Position - 5,
                        Scale = new osuTK.Vector2(hollow ? 2f / 20 : 1f / 20),
                        Y = skinNote.Position / 2f,
                        X = noteX + 0.5f
                    });
                }
                if (note.Duration > 0) // roll
                {
                    var centerOffset = 0.5f;
                    outside.Add(new Circle
                    {
                        Y = skinNote.Position / 2f - RollHeight / 2f,
                        // this is placed outside, so we can ignore groupOffset
                        X = (float)(flag.Time * Spacing) + bottomAnchor.x - anchor + centerOffset,
                        Height = RollHeight,
                        Width = (float)(note.Duration * Spacing) - centerOffset,
                        Colour = RollColour,
                        Depth = 10
                    });
                }
                if (ghost)
                {
                    container.Add(new NoteSprite
                    {
                        Character = MusicGlyph.noteheadParenthesis.Codepoint(),
                        Y = -8 + skinNote.Position / 2f,
                        X = noteX,
                        Colour = Colour,
                        Font = FontUsage
                    });
                }
                // need to dot note
                // if ((BitConverter.DoubleToInt64Bits(flag.EffectiveDuration) & 0xfffffffffffffL) == 0x8000000000000L)
                if (flag.EffectiveDuration == 0.75 || flag.EffectiveDuration == 0.375)
                {
                    // right-most part of the notehead
                    var rightSide = down ? GetAnchorValue(skinNote.Notehead, "stemUpSE")[0] : anchor;
                    var dotY = skinNote.Position;
                    if ((dotY & 1) == 0) dotY += dir; // prevent dot being placed on a line
                    container.Add(new NoteSprite
                    {
                        Character = MusicGlyph.augmentationDot.Codepoint(),
                        Y = -8 + dotY / 2f,
                        X = noteX + rightSide + AugmentationDotGap,
                        Colour = Colour,
                        Font = FontUsage
                    });
                }
            }
            var flagX = flag.FlagLeft;
            beamLeft = Math.Min(beamLeft, flagX);
            beamRight = Math.Max(beamRight, flagX + EngravingDefaults.stemThickness);
            container.Add(new Box // stem
            {
                X = flagX,
                Y = bottomNote.Note.Position / 2f - bottomAnchor.y,
                Origin = down ? Anchor.TopLeft : Anchor.BottomLeft,
                Colour = Colour,
                Width = EngravingDefaults.stemThickness,
                Height = Math.Abs(bottomNote.Note.Position / 2f - targetHeight) + dir * bottomAnchor.y
            });
            Drawable flagGlyph = null;
            if (flags.Count == 1) // fancy flag
            {
                flagGlyph = RenderFlag(flags[0], targetHeight, down);
                if (flagGlyph != null) container.Add(flagGlyph);
            }
            if (flag.Accented)
            {
                var accentHeight = targetHeight + dir * (AccentGap +
                    (flags.Count > 1 || flagGlyph != null ? EngravingDefaults.beamThickness : 0));
                container.Add(new NoteSprite
                {
                    Character = (down ? MusicGlyph.articAccentBelow : MusicGlyph.articAccentAbove).Codepoint(),
                    Y = accentHeight - 8,
                    X = bottomX,
                    Colour = Colour,
                    Font = FontUsage
                });
            }
        }

        if (flags.Count > 1)
        {
            container.Add(Beam(beamLeft, beamRight, down ? targetHeight : targetHeight - EngravingDefaults.beamThickness));
            // depth 1 = 16th, depth 2 = 32nd
            var beamY = targetHeight + (down ? -EngravingDefaults.beamSpacing - EngravingDefaults.beamThickness : EngravingDefaults.beamSpacing);
            var duration = 0.25f;
            foreach (var depth in new[] { 1, 2 })
            {
                var startCount = container.Count;
                float beamStart = 0;
                var beamCount = 0;
                for (var i = 0; i < flags.Count; i++)
                {
                    var flag = flags[i];
                    if (flag.EffectiveDuration <= duration || flag.EffectiveDuration == duration * 1.5f)
                    {
                        if (beamCount == 0) beamStart = flag.FlagLeft;
                        beamCount += 1;
                    }
                    else
                    {
                        // there's a note that is not part of the current beam, so we have to end it
                        if (beamCount > 0)
                        {
                            var end = beamCount == 1 ? (beamStart + flag.FlagLeft + EngravingDefaults.stemThickness) / 2f : flags[i - 1].FlagLeft;
                            container.Add(Beam(beamStart, end, beamY));
                            beamCount = 0;
                        }
                    }
                }
                // we finished going through the notes and there is an active beam
                if (beamCount > 0)
                {
                    if (beamCount == 1)
                    {
                        container.Add(Beam(beamStart - Spacing * duration * 0.5f, beamStart, beamY));
                    }
                    else
                    {
                        var end = flags[flags.Count - 1].FlagLeft;
                        container.Add(Beam(beamStart, end, beamY));
                    }
                }
                if (container.Count == startCount) break; // if we didn't add any beams, there won't be any more
                duration *= 0.5f;
                beamY += dir * -(EngravingDefaults.beamSpacing + EngravingDefaults.beamThickness);
            }
        }
    }
}

