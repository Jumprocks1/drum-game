using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace DrumGame.Game.Views;

public class SplashScreen : CompositeDrawable
{
    public SplashScreen(string message)
    {
        RelativeSizeAxes = Axes.Both;
        AddInternal(new AutoSizeSpriteText
        {
            Text = message,
            Font = FrameworkFont.Regular,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Padding = new MarginPadding
            {
                Left = 100,
                Right = 100
            }
        });
    }
}