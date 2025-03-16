using System;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics;
using DrumGame.Game.Utils;
using osu.Framework.Graphics.Effects;
using osuTK;
using osu.Framework.Graphics.Containers;

namespace DrumGame.Game.Beatmaps.Display.Mania;

public class ManiaJudgementErrorNumbers : CompositeDrawable
{
    public SpriteText Number;
    public SpriteText FastSlow;
    static Skinning.ManiaJudgementInfo.ErrorNumbersInfo Config => Util.Skin.Mania.Judgements.ErrorNumbers;

    public ManiaJudgementErrorNumbers(ManiaBeatmapDisplay.Lane lane)
    {
        X = lane.X + lane.Width / 2;
        Y = 1 - (lane.Config.JudgementTextPosition + Config.Y);
        RelativePositionAxes = Axes.Both;
        Origin = Anchor.BottomCentre;

        var outline = new OutlineEffect
        {
            Strength = 2,
            BlurSigma = new Vector2(2)
        };
        Number = new SpriteText
        {
            Text = "",
            Anchor = Anchor.BottomCentre,
            Origin = Anchor.BottomCentre,
            Font = DrumFont.Bold.With(size: 18)
        };
        AddInternal(Number.WithEffect(outline));
        if (Config.ShowFastSlow)
        {
            FastSlow = new SpriteText
            {
                Text = "",
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Font = DrumFont.Bold.With(size: 18),
                Y = -18
            };
            AddInternal(FastSlow.WithEffect(outline));
        }
        Height = 36;
        Width = 100;
    }

    public void DisplayError(double error)
    {
        ClearTransforms();
        if (Math.Abs(error) < Config.HideWindow)
        {
            Alpha = 0;
            return;
        }
        Number.Text = $"{-error:+000;-000}";
        var color = error > 0 ? Config.SlowColor : Config.FastColor;
        Number.Colour = color;
        if (FastSlow != null)
        {
            FastSlow.Text = error > 0 ? "SLOW" : "FAST";
            FastSlow.Colour = color;
        }
        Alpha = 1;
        this.FadeOut(Config.Duration, Easing.InCubic);
    }
}