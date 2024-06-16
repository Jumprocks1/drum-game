using osu.Framework.Localisation;

namespace DrumGame.Game.Containers;

public record MultilineTooltipData(string Data) { }
// just my shitty version of markdown
public record MarkupTooltipData(string Data)
{
    public static explicit operator LocalisableString(MarkupTooltipData b) => b.Data;
}