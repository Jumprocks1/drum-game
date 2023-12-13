using DrumGame.Game.Components;
using DrumGame.Game.Views;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace DrumGame.Game.Browsers;

public class KeyBindingBrowser : CompositeDrawable
{
    public KeyBindingBrowser()
    {
        RelativeSizeAxes = Axes.Both;
        AddInternal(new SpriteText { Text = "Press modifier keys (Ctrl, Shift, Alt) to view different bindings", Y = 3, X = 4 });
        var checkbox = new DrumCheckbox { LabelText = "Only Show Available Commands", X = 500, Y = 3 };
        AddInternal(checkbox);
        var keyboardView = new PhysicalKeyboardView();
        checkbox.Current.BindValueChanged(e =>
        {
            keyboardView.OnlyShowAvailable = e.NewValue;
        });
        AddInternal(new DrawSizePreservingFillContainer
        {
            TargetDrawSize = new Vector2(PhysicalKeyboardView.Width, PhysicalKeyboardView.Height),
            Strategy = DrawSizePreservationStrategy.Minimum,
            Child = keyboardView,
            Padding = new MarginPadding { Top = 40 }
        });
    }
}