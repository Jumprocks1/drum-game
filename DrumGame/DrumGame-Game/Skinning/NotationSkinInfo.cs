using System.Collections.Generic;
using System.ComponentModel;
using DrumGame.Game.Channels;
using DrumGame.Game.Notation;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using osu.Framework.Graphics;

namespace DrumGame.Game.Skinning;

public class NotationSkinInfo
{
    [Description("Color for stems, beams, accents, and ghost notes.")]
    public Colour4 NotationColor = Colour4.Black;
    [Description("Default color for noteheads. If not supplied, defaults to match notation color.")]
    public Colour4 NoteColor;
    [Description("Enable to show measures lines in the notation view while playing")]
    public bool MeasureLines;
    public Colour4 MeasureLineColor;
    public Colour4 StaffLineColor;
    public Colour4 PlayfieldBackground = Colour4.White;
    public Colour4 InputDisplayBackground = Colour4.Gainsboro;
    public Colour4 LeftNoteColor = DrumColors.BrightCyan;
    public Colour4 RightNoteColor = DrumColors.BrightRed;
    public AdjustableSkinData SongInfoPanel;
    public AdjustableSkinData EventContainer;
    public AdjustableSkinData VolumeControlGroup;
    public AdjustableSkinData PracticeInfoPanel;
    public AdjustableSkinData HitErrorDisplay;
    public void LoadDefaults()
    {
        SongInfoPanel?.LoadDefaults();
        EventContainer?.LoadDefaults();
        VolumeControlGroup?.LoadDefaults();
        PracticeInfoPanel?.LoadDefaults();
        HitErrorDisplay?.LoadDefaults();
        if (NoteColor == default) NoteColor = NotationColor;
        if (StaffLineColor == default) StaffLineColor = NotationColor;
        if (MeasureLineColor == default) MeasureLineColor = DrumColors.Blue.MultiplyAlpha(0.4f);
        foreach (var channel in Channels.Values)
        {
            if (channel.Color == default)
                channel.Color = NoteColor;

            // if we are hiding sticking letters, always change the L/R color
            if (channel.StickingColorNotehead)
            {
                if (channel.LeftColor == default)
                    channel.LeftColor = LeftNoteColor;
                if (channel.RightColor == default)
                    channel.RightColor = RightNoteColor;
            }
            else
            {
                if (channel.LeftColor == default)
                    channel.LeftColor = channel.IsHollow() ? LeftNoteColor : PlayfieldBackground;
                if (channel.RightColor == default)
                    channel.RightColor = channel.IsHollow() ? RightNoteColor : PlayfieldBackground;
            }
        }
    }
    // we could add an array version of this since dictionary is much slower
    [JsonConverter(typeof(SkinManager.SkinChannelConverter))]
    public Dictionary<DrumChannel, SkinNote> Channels = new()
        {
            {DrumChannel.HiHatPedal, new SkinNote(9, MusicGlyph.noteheadXBlack)},
            {DrumChannel.Crash, new SkinNote(-2, MusicGlyph.noteheadXOrnate){ StickingColorNotehead = true }},
            {DrumChannel.Splash, new SkinNote(-2, MusicGlyph.noteheadDiamondWhite){ StickingColorNotehead = true }},
            {DrumChannel.China, new SkinNote(-2, MusicGlyph.noteheadXOrnateEllipse){ StickingColorNotehead = true }},
            {DrumChannel.OpenHiHat, new SkinNote(-1, MusicGlyph.noteheadCircleX)},
            {DrumChannel.HalfOpenHiHat, new SkinNote(-1, MusicGlyph.noteheadCircleX)},
            {DrumChannel.ClosedHiHat, new SkinNote(-1, MusicGlyph.noteheadXBlack)},
            {DrumChannel.Ride, new SkinNote(0, MusicGlyph.noteheadXBlack)},
            {DrumChannel.RideBell, new SkinNote(0, MusicGlyph.noteheadDiamondBlack)},
            {DrumChannel.Snare, new SkinNote(3)},
            {DrumChannel.Rim, new SkinNote(1, MusicGlyph.noteheadXBlack)},
            {DrumChannel.SideStick, new SkinNote(3, MusicGlyph.noteheadCircledBlackLarge)},
            {DrumChannel.SmallTom, new SkinNote(1, color: new(0f, 0.5f, 0.58f, 1f))},
            {DrumChannel.MediumTom, new SkinNote(2, color: new(0.33f, 0f, 0.58f, 1f))},
            {DrumChannel.LargeTom, new SkinNote(5, color: new(0.58f, 0.17f, 0f, 1f))},
            {DrumChannel.BassDrum, new SkinNote(7)},
        };
}
