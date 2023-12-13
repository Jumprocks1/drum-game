using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DrumGame.Game.Notation;

// data mostly from https://www.w3.org/2021/03/smufl14/tables/noteheads.html
[JsonConverter(typeof(StringEnumConverter))]
public enum MusicGlyph : ushort
{
    noteheadBlack = 0xE0A4, // round, default notehead
    noteheadXBlack = 0xE0A9, // Cymbol
    noteheadCircleX = 0xE0B3, // Open Cymbol
    noteheadCircledBlackLarge = 0xE0E8, // side stick
    noteheadXOrnate = 0xE0AA, // Crash
    noteheadXOrnateEllipse = 0xE0AB, // China
    noteheadDiamondWhite = 0xE0DD, // Splash
    noteheadDiamondBlack = 0xE0DB, // Ride bell
    flag8thUp = 0xE240,
    flag8thDown = 0xE241,
    flag16thUp = 0xE242,
    flag16thDown = 0xE243,
    augmentationDot = 0xE1E7,
    articAccentAbove = 0xE4A0,
    articAccentBelow = 0xE4A1,
    noteheadParenthesis = 0xE0CE,
}

public static class MusicGlyphExtensions
{
    public static char Codepoint(this MusicGlyph glyph) => Unsafe.As<MusicGlyph, char>(ref glyph);
}
