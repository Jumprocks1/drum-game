using System;
using DrumGame.Game.Commands;
using DrumGame.Game.Interfaces;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Components;

public class IconButton : SpriteIcon, IHasMarkupTooltip, IHasCursor
{
    public SDL2.SDL.SDL_SystemCursor? Cursor => Action == null ? null : SDL2.SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_HAND;
    public Action Action;

    public string MarkupTooltip { get; set; }

    Colour4 _buttonColor = Colour4.White;
    public Colour4 ButtonColor
    {
        get => _buttonColor; set
        {
            _buttonColor = value;
            Colour = value;
        }
    }

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

    public bool BlockHover = true;

    protected override bool OnMouseDown(MouseDownEvent e) => true;
    protected override bool OnHover(HoverEvent e)
    {
        this.FadeColour(ButtonColor.Darken(0.3f), 200);
        return BlockHover;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        this.FadeColour(ButtonColor, 200);
        base.OnHoverLost(e);
    }
}