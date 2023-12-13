using DrumGame.Game.Commands;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;

namespace DrumGame.Game.Components;

public class CommandTextIconButton : CompositeDrawable, IHasCommand
{
    public Command Command { get; }
    public bool AllowClick => true;
    Colour4? baseColor;

    SpriteText SpriteText;
    SpriteIcon SpriteIcon;
    public LocalisableString Text { get => SpriteText.Text; set => SpriteText.Text = value; }
    public CommandTextIconButton(Command command, IconUsage icon, float height)
    {
        Command = command;
        Height = height;
        AddInternal(SpriteText = new SpriteText
        {
            Font = FrameworkFont.Regular.With(size: height)
        });
        AddInternal(SpriteIcon = new SpriteIcon
        {
            Width = height,
            Height = height,
            Icon = icon,
            Origin = Anchor.CentreRight,
            Anchor = Anchor.CentreRight
        });
    }

    protected override void LoadComplete()
    {
        Width = SpriteText.Width + 10 + SpriteIcon.Width;
    }

    protected override bool OnMouseDown(MouseDownEvent e) => true;
    protected override bool OnHover(HoverEvent e)
    {
        baseColor ??= Colour; // save original color
        this.FadeColour(baseColor.Value.DarkenOrLighten(0.3f), 200);
        return base.OnHover(e);
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        this.FadeColour(baseColor.Value, 200);
        base.OnHoverLost(e);
    }
}