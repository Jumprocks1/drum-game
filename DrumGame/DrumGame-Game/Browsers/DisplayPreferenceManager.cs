using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Utils;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Browsers;


public class DisplayPreferenceManager : CompositeDrawable
{
    CommandButton Button;
    public DisplayPreferenceManager()
    {
        AddInternal(new SpriteText
        {
            Text = "Display Mode",
            Y = 2,
            Font = FrameworkFont.Regular.With(size: 16),
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre
        });
        Button = new CommandButton(Command.SelectDisplayMode)
        {
            Y = 20,
            Height = 30,
            RelativeSizeAxes = Axes.X,
            FontSize = 26
        };
        Util.ConfigManager.DisplayMode.BindValueChanged(BindingChanged, true);
        AddInternal(Button);
        Util.CommandController.RegisterHandlers(this);
    }
    void BindingChanged(ValueChangedEvent<DisplayPreference> e)
    {
        Button.Text = e.NewValue.ToString();
    }

    [CommandHandler] public bool SelectDisplayMode(CommandContext context) => context.GetItem(Util.ConfigManager.DisplayMode, "Selecting Display Mode");

    protected override void Dispose(bool isDisposing)
    {
        Util.CommandController.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }
}