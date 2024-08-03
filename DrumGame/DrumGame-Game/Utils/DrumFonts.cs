using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Utils;

public static class DrumFont
{
    public static FontUsage Bold => new FontUsage("Roboto", weight: "Bold");
    public static FontUsage Regular => FrameworkFont.Regular;
}