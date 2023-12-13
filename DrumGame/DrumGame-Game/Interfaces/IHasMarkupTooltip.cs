using System;
using System.Linq;
using DrumGame.Game.Commands;
using DrumGame.Game.Containers;
using DrumGame.Game.Utils;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;

namespace DrumGame.Game.Interfaces;

public interface IHasMarkupTooltip : IHasCustomTooltip
{
    ITooltip IHasCustomTooltip.GetCustomTooltip() => null;
    object IHasCustomTooltip.TooltipContent
    {
        get
        {
            var tooltip = MarkupTooltip;
            if (tooltip == null) return null;
            return new MarkupTooltipData(MarkupTooltip);
        }
    }
    public string MarkupTooltip { get; }
}