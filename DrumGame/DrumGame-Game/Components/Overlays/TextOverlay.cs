using System;
using DrumGame.Game.Components.Abstract;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Components.Overlays;

public class TextOverlay : FadeContainer
{
    SpriteText Text;
    public TextOverlay(string text)
    {
        AutoSizeAxes = Axes.Both;
        Anchor = Anchor.TopCentre;
        Origin = Anchor.TopCentre;
        Y = 100;
        AddInternal(Text = new SpriteText
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