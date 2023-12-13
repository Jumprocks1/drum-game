using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;

namespace DrumGame.Game.Components;

public class DrumButtonTooltip : DrumButton, IHasTooltip
{
    public LocalisableString TooltipText { get; set; }
}