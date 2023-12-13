using System;
using DrumGame.Game.Commands;
using DrumGame.Game.Interfaces;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Components;

public class IconButton : SpriteIcon, IHasMarkupTooltip
{
    public Action Action;

    public string MarkupTooltip { get; set; }

    public IconButton(Action action, IconUsage icon, float size)
    {
        Action = action;
        Icon = icon;
        Width = size;
        Height = size;
    }

    protected override bool OnClick(ClickEvent e)
    {
        Action?.Invoke();
        return true;
    }

    protected override bool OnMouseDown(MouseDownEvent e) => true;
    protected override bool OnHover(HoverEvent e)
    {
        this.FadeColour(Colour4.White.Darken(0.3f), 200);
        return base.OnHover(e);
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        this.FadeColour(Colour4.White, 200);
        base.OnHoverLost(e);
    }
}