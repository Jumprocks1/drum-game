using System;
using DrumGame.Game.Components.Abstract;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Components.Overlays;

public enum MessagePosition
{
    Top,
    Center,
    Bottom,
}
public class TextOverlay : FadeContainer
{
    public TextOverlay(string text, MessagePosition position)
    {
        AutoSizeAxes = Axes.Both;
        if (position == MessagePosition.Top)
        {
            Anchor = Anchor.TopCentre;
            Origin = Anchor.TopCentre;
            Y = 100;
        }
        else if (position == MessagePosition.Center)
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }
        else if (position == MessagePosition.Bottom)
        {
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;
            Y = -100;
        }
        AddInternal(new SpriteText
        {
            Text = text,
            Padding = new MarginPadding(15),
            Font = FrameworkFont.Regular.With(size: 30)
        });
    }

    public Action OnDisappear;

    protected override void Update()
    {
        base.Update();
        if (Alpha == 0) OnDisappear?.Invoke();
    }
}