using System;
using DrumGame.Game.Commands;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Components;

public class CommandText : SpriteText, IHasCommand
{
    public Command Command { get; }
    public Func<string> ExtraTooltip;
    string IHasMarkupTooltip.MarkupTooltip
    {
        get
        {
            var main = IHasCommand.GetMarkupTooltip(Command);
            return ExtraTooltip == null ? main : $"{ExtraTooltip()}\n{main}";
        }
    }
    Colour4? baseColor;
    public CommandText(Command command)
    {
        Command = command;
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