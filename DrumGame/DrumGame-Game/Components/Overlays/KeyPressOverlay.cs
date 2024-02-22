using DrumGame.Game.Commands;
using DrumGame.Game.Components.Abstract;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Components.Overlays;

public class KeyPressOverlay : AdjustableSkinElement
{
    public override ref AdjustableSkinData SkinPath => ref Util.Skin.KeyPressOverlay;
    public override AdjustableSkinData DefaultData() => new()
    {
        Anchor = Anchor.BottomLeft,
        Y = -250
    };


    SpriteText Text;

    FadeContainer Container;


    public KeyPressOverlay()
    {
        Util.KeyPressOverlay = this;
        AutoSizeAxes = Axes.Both; // not ideal since FadeContainer goes to alpha = 0
        AddInternal(Container = new()
        {
            AutoSizeAxes = Axes.Both
        });
        Container.Add(Text = new SpriteText
        {
            Padding = new MarginPadding(15),
            Font = FrameworkFont.Regular.With(size: 20)
        });
        Util.CommandController.AfterCommandActivated += AfterCommandActivated;
    }

    string baseText;
    int repeatCount = 0;
    void UpdateDisplay(string text, string keys)
    {
        string res;
        if (text == baseText)
        {
            res = $"{baseText} x{++repeatCount}";
        }
        else
        {
            repeatCount = 1;
            res = baseText = text;
        }
        if (keys != null) res += $"   [ {keys} ]";
        Text.Text = res;
    }

    public void Handle(string name, KeyCombo keyCombo)
    {
        UpdateDisplay(name, keyCombo.DisplayString);
        Container.Touch();
    }

    void AfterCommandActivated(CommandInfo command, CommandContext context) =>
        Handle(command.Name, context.KeyEvent != null ? new KeyCombo(context.KeyEvent) : KeyCombo.None);

    protected override void Dispose(bool isDisposing)
    {
        Util.CommandController.AfterCommandActivated -= AfterCommandActivated;
        base.Dispose(isDisposing);
    }
}