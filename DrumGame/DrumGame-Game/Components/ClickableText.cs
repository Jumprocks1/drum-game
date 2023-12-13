using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;

namespace DrumGame.Game.Components;

public class ClickableText : SpriteText, IHasTooltip
{
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
    public Action Action;

    public LocalisableString TooltipText { get; set; }

    protected override bool OnClick(ClickEvent e)
    {
        Action?.Invoke();
        return true;
    }
}