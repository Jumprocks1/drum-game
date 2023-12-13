using DrumGame.Game.Interfaces;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;

namespace DrumGame.Game.Components.Basic;
public class TooltipSpriteText : SpriteText, IHasTooltip
{
    public TooltipSpriteText(string tooltip) : base() { Tooltip = tooltip; }
    public TooltipSpriteText() : base() { }
    public string Tooltip;
    public LocalisableString TooltipText => Tooltip;
}
public class MarkupTooltipSpriteText : SpriteText, IHasMarkupTooltip
{
    public MarkupTooltipSpriteText(string tooltip) : base() { MarkupTooltip = tooltip; }
    public MarkupTooltipSpriteText() : base() { }
    public string MarkupTooltip { get; set; }
}