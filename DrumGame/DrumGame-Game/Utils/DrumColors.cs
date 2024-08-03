using System;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Skinning;
using osu.Framework.Graphics;

namespace DrumGame.Game.Utils;

public static class DrumColors
{
    public static readonly Colour4 ModalBackground = new(0, 0, 0, 110); // faded background behind modal
    public static readonly Colour4 DarkBackground = new(31, 31, 31, 255); // copied from VSCode context menu
    public static readonly Colour4 DarkBackgroundAlt = new(42, 42, 57, 255); // meant for overlapping with DarkBackground
    public static readonly Colour4 DarkBorder = new(69, 69, 69, 255); // copied from VSCode context menu
    public static readonly Colour4 LightBorder = new(207, 209, 212, 255); // copied from Windows
    public static readonly Colour4 DarkActiveBackground = new(39, 50, 88, 255);

    public static Colour4 ActiveField => Blue.Darken(0.6f);
    public static Colour4 ActiveTextBox => ActiveField;
    public static Colour4 FieldBackground => DarkBackground + new Colour4(10, 10, 10, 0);
    public static Colour4 ActiveButton => ActiveField; // for a button that will be activated with enter
    public static Colour4 DarkActiveButton => ActiveField.Darken(0.5f); // for a button that will be activated with enter
    public static Colour4 Button => FieldBackground; // for a regular button that is not currently active
    public static Colour4 ActiveCheckbox => BrightBlue;
    public static Colour4 CheckboxBackground => DarkActiveBackground;

    public static readonly Colour4 Placeholder = Colour4.White.MultiplyAlpha(0.4f);
    public static readonly Colour4 Selection = Colour4.White.MultiplyAlpha(0.4f);
    public static readonly Colour4 ObjectSelection = Colour4.SeaGreen;
    public static readonly Colour4 RowHighlight = Colour4.White.MultiplyAlpha(0.05f);
    public static readonly Colour4 RowHighlightSecondary = Colour4.White.MultiplyAlpha(0.02f);

    public static readonly Colour4 ExpertPlus = new(65, 74, 76, 255);
    public static Colour4 Expert => BrightMagenta;
    public static Colour4 Insane => BrightRed;
    public static Colour4 Hard => BrightOrange;
    public static Colour4 Normal => BrightCyan;
    public static Colour4 Easy => BrightGreen;

    public static Colour4 Command => BrightGreen;
    public static Colour4 Midi => BrightCyan;
    public static readonly Colour4 Code = rgb(144, 194, 255); // code text


    public static Colour4 BrightDangerText => BrightRed;

    // Mostly from VS Code
    // https://github.com/microsoft/vscode/blob/main/src/vs/workbench/contrib/terminal/common/terminalColorRegistry.ts

    // You basically want to maximize chroma at all times, keep hue constants between dark/normal/bright, then adjust lightness to your liking
    // lightness varies for me based on the hue

    // All dark colors were made by using https://oklch.com/

    // I made these oranges
    public static readonly Colour4 DarkOrange = rgb(212, 108, 0);
    public static readonly Colour4 Orange = rgb(212, 108, 0);
    public static readonly Colour4 BrightOrange = rgb(235, 129, 43);

    public static readonly Colour4 FadedText = new(180, 180, 180, 255);

    // careful with referencing other colors - the initialization order matters here

    public static readonly Colour4 DarkBlue = rgb(0, 92, 177);
    public static readonly Colour4 Blue = rgb(36, 114, 200);
    public static readonly Colour4 BrightBlue = rgb(59, 142, 234);

    public static readonly Colour4 Cyan = new(17, 168, 205, 255);
    public static readonly Colour4 BrightCyan = new(41, 184, 219, 255);

    public static readonly Colour4 DarkGreen = rgb(0, 140, 89);
    public static readonly Colour4 Green = rgb(13, 188, 121);
    public static readonly Colour4 BrightGreen = rgb(35, 209, 139);

    public static readonly Colour4 Magenta = new(188, 63, 188, 255);
    public static readonly Colour4 BrightMagenta = new(214, 112, 214, 255);

    public static readonly Colour4 DarkRed = rgb(168, 0, 20);
    public static readonly Colour4 Red = rgb(205, 49, 49);
    public static readonly Colour4 BrightRed = rgb(241, 76, 76);

    public static readonly Colour4 Yellow = new(229, 229, 16, 255);
    public static readonly Colour4 BrightYellow = new(245, 245, 67, 255);


    public static readonly Colour4 Black00 = new(0, 0, 0, 255);
    public static readonly Colour4 Black10 = new(25, 25, 25, 255);
    public static readonly Colour4 Black20 = new(51, 51, 51, 255);
    public static readonly Colour4 Black25 = new(63, 63, 63, 255);
    public static readonly Colour4 Black30 = new(76, 76, 76, 255);
    public static readonly Colour4 Black40 = new(102, 102, 102, 255);

    public static readonly Colour4 AnsiWhite = new(229, 229, 229, 255);
    public static Colour4 WarningText => BrightOrange;


    // https://www.reddit.com/r/CasualCSS/comments/43aizf/reddit_color_codes/
    public static Colour4 Upvote => new(255, 139, 90, 255);
    public static Colour4 Downvote => new(147, 145, 255, 255);


    // useful for inline decorator in VSCode
    static Colour4 rgb(byte r, byte g, byte b) => new(r, g, b, 255);
    public static Colour4 Mix(this Colour4 color, Colour4 other, float alpha) => color * (1 - alpha) + other * alpha;

    public static Colour4 BlendedHitColor(double error, Skin.Skin_HitColors colors, HitWindows windows)
    {
        // not might be a better way to do this, not sure
        var g = new (double error, Colour4 color)[]
        {
            (-windows.BadWindow, colors.EarlyMiss),
            (-windows.GoodWindow, colors.EarlyGood),
            (-windows.PerfectWindow, colors.EarlyPerfect),
            (windows.PerfectWindow, colors.LatePerfect),
            (windows.GoodWindow, colors.LateGood),
            (windows.BadWindow, colors.LateMiss),
        };
        if (error <= g[0].error) return g[0].color;
        for (var i = 0; i < g.Length - 1; i++)
        {
            if (error < g[i + 1].error)
                return Mix(g[i].color, g[i + 1].color, (float)((error - g[i].error) / (g[i + 1].error - g[i].error)));
        }
        return g[^1].color;
    }

    // See
    // https://ux.stackexchange.com/questions/107318/formula-for-color-contrast-between-text-and-background
    // could make this take a list of colors, then do max by contrast ratio
    public static Colour4 ContrastText(Colour4 background)
    {
        // cutoff is based on contrast ratio formula
        if (Luminance(background) > 0.1791) return Colour4.Black;
        return Colour4.White;
    }
    public static double ContrastRatio(Colour4 foreground, Colour4 background)
        => ContrastRatio(Luminance(foreground), Luminance(background));
    public static double ContrastRatio(double lumA, double lumB) => (Math.Max(lumA, lumB) + 0.05) / (Math.Min(lumA, lumB) + 0.05);
    public static double Luminance(Colour4 color)
    {
        // https://www.w3.org/TR/2008/REC-WCAG20-20081211/#relativeluminancedef
        var linear = color.ToLinear();
        return 0.2126 * linear.R + 0.7152 * linear.B + 0.0722 * linear.G;
    }
}
