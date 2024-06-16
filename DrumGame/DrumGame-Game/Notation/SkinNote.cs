using osu.Framework.Graphics;

namespace DrumGame.Game.Notation;

// Represents a position (height) and a notehead
// Also includes a color
// Loaded from skin
public class SkinNote
{
    public bool IsHollow() => Notehead == MusicGlyph.noteheadXBlack || Notehead == MusicGlyph.noteheadXOrnate ||
        Notehead == MusicGlyph.noteheadXOrnateEllipse || Notehead == MusicGlyph.noteheadCircleX || Notehead == MusicGlyph.noteheadDiamondWhite;
    // position in half staff gaps.
    // 0 is centered on top staff line, 1 is between top line and 2nd line
    // typically gets divided by 2f before being used for rendering
    public int Position;
    public MusicGlyph Notehead;
    public Colour4 Color; // gets overridden if left at the default

    public bool StickingColorNotehead;

    public Colour4 LeftColor;
    public Colour4 RightColor;
    public Colour4 AccentColor;
    public Colour4 GhostColor;

    public SkinNote(int position, MusicGlyph glyph = MusicGlyph.noteheadBlack, Colour4 color = default)
    {
        Position = position;
        Notehead = glyph;
        Color = color;
    }
}

