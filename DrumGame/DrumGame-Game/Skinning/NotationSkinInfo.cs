using System.Collections.Generic;
using System.ComponentModel;
using DrumGame.Game.Channels;
using DrumGame.Game.Components;
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

    [Description("Increases or decreases spacing between notes. Higher values will make denser maps easier to read.\nRecommended between 1 and 2. Defaults to 1.")]
    [DefaultValue(1d)]
    public double NoteSpacingMultiplier = 1;

    [Description("Increases or decreases default zoom of the in-game staff. Defaults to 1. Recommended between 0.5 and 2.")]
    [DefaultValue(1d)]
    public double ZoomMultiplier = 1;

    [Description("Distance (in beats) between the cursor and the left edge of the screen. Defaults to 4. Smaller values are recommended when at higher zoom or note spacing.")]
    [DefaultValue(4d)]
    public double CursorInset = 4;

    [Description("Smooth scroll makes the notes move towards a static cursor/judgement line.")]
    [DefaultValue(true)]
    public bool SmoothScroll = true;

    public Colour4 MeasureLineColor;
    public Colour4 StaffLineColor;
    public Colour4 PlayfieldBackground = Colour4.White;
    public Colour4 InputDisplayBackground = Colour4.Gainsboro;
    public Colour4 LeftNoteColor = DrumColors.BrightCyan;
    public Colour4 RightNoteColor = DrumColors.BrightRed;
    public AdjustableSkinData SongInfoPanel;
    public AdjustableSkinData EventContainer;
    public VolumeControlGroup.CustomSkinData VolumeControlGroup;
    public AdjustableSkinData PracticeInfoPanel;
    public AdjustableSkinData HitErrorDisplay;
    public List<ExtraSkinElementData> ExtraElements;
    public NotationJudgementInfo Judgements = new();
    public void LoadDefaults()
    {
        Judgements?.LoadDefaults();
        SongInfoPanel?.LoadDefaults();
        EventContainer?.LoadDefaults();
        VolumeControlGroup?.LoadDefaults();
        PracticeInfoPanel?.LoadDefaults();
        HitErrorDisplay?.LoadDefaults();
        if (ExtraElements != null) foreach (var e in ExtraElements) e.Placement?.LoadDefaults();
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
            {DrumChannel.HalfOpenHiHat, new SkinNote(-1, MusicGlyph.noteheadCircleSlash)},
            {DrumChannel.ClosedHiHat, new SkinNote(-1, MusicGlyph.noteheadXBlack)},
            {DrumChannel.Ride, new SkinNote(0, MusicGlyph.noteheadXBlack)},
            {DrumChannel.RideBell, new SkinNote(0, MusicGlyph.noteheadDiamondBlack)},
            {DrumChannel.RideCrash, new SkinNote(0, MusicGlyph.noteheadXOrnate)},
            {DrumChannel.Snare, new SkinNote(3)},
            {DrumChannel.Rim, new SkinNote(1, MusicGlyph.noteheadXBlack)},
            {DrumChannel.SideStick, new SkinNote(3, MusicGlyph.noteheadCircledBlackLarge)},
            {DrumChannel.SmallTom, new SkinNote(1, color: new(0f, 0.5f, 0.58f, 1f))},
            {DrumChannel.MediumTom, new SkinNote(2, color: new(0.33f, 0f, 0.58f, 1f))},
            {DrumChannel.LargeTom, new SkinNote(5, color: new(0.58f, 0.17f, 0f, 1f))},
            {DrumChannel.BassDrum, new SkinNote(7)},
        };
}
