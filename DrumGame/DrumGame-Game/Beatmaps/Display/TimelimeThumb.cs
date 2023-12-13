using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK.Input;

namespace DrumGame.Game.Beatmaps.Display;

public class TimelineThumb : Circle
{
    public static readonly Colour4 ThumbColour = Colour4.Gold;
    public static readonly Colour4 GlowColour = Colour4.Khaki;
    public void SetSize(float size)
    {
        Height = size;
        Width = size;
        EdgeEffect = new EdgeEffectParameters
        {
            Type = EdgeEffectType.Glow,
            Colour = GlowColour,
            Radius = size * 0.375f
        };
    }
    public TimelineThumb(float size)
    {
        Colour = ThumbColour;
        RelativePositionAxes = Axes.X;
        Anchor = Anchor.CentreLeft;
        Origin = Anchor.Centre;
        SetSize(size);
    }
}

