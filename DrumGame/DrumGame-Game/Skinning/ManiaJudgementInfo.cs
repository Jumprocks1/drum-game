using System;
using System.ComponentModel;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;

namespace DrumGame.Game.Skinning;

public class ManiaJudgementInfo
{
    [Description("Shows the judgement result from a texture file.\nFor DTXMania skins, this will show animated Perfect/Great/Ok/Miss text.")]
    public bool Textures;
    [Description("Inserts a fading chip at the location the judgement occured.\nThis is mimics how judgements are displayed in notation mode.")]
    [DefaultValue(true)]
    public bool Chips = true;
    [Description("Hides chips after they are hit.\nCurrently, this does not include any animation. In the future there may be some particles for this.")]
    [DefaultValue(true)]
    public bool HideHitChips = true;
    public ErrorNumbersInfo ErrorNumbers = new();

    public class ErrorNumbersInfo
    {
        [Description("Displays the judgement error in milliseconds above the judgement texture location.\nMore configuration options are available by editing the skin directly.")]
        public bool Show;
        [Description("Shows FAST/SLOW text above the error number")]
        public bool ShowFastSlow;
        public DrumChannel? SingleLane;
        public float Y = 0.05f;
        [Description("Duration in milliseconds that the error numbers will be displayed for.")]
        public float Duration = 1000;
        public Colour4 FastColor = DrumColors.BrightCyan;
        public Colour4 SlowColor = DrumColors.BrightRed;
    }

    public SkinTexture Perfect;
    public SkinTexture Good;
    public SkinTexture Bad;
    public SkinTexture Miss;

    public SkinTexture TextureForJudgement(HitScoreRating rating) => rating switch
    {
        HitScoreRating.Perfect => Perfect,
        HitScoreRating.Good => Good,
        HitScoreRating.Bad => Bad,
        HitScoreRating.Miss => Miss,
        _ => null
    };

    public void LoadDefaults()
    {
    }
}

[Flags]
public enum ManiaJudgementStyle
{
    Texture = 1,
    Chip = 2,
    TextureAndChip = Texture | Chip
}