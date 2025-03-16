using osu.Framework.Localisation;

namespace DrumGame.Game.Containers;

public record MultilineTooltipData(string Data) { }
// just my shitty version of markdown
public record MarkupTooltipData(string Data)
{
    // This is meant to fix the below exception
    // Unable to cast object of type 'DrumGame.Game.Containers.MarkupTooltipData' to type 'osu.Framework.Localisation.LocalisableString'
    // But it doesn't work for some reason
    // That exception occurs when hovering over a markup tooltip with the draw visualizer open
    public static explicit operator LocalisableString(MarkupTooltipData b) => b.Data;
}