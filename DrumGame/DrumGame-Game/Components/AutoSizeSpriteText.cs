using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.IO.Stores;

public class AutoSizeSpriteText : SpriteText
{
    [Resolved] FontStore FontStore { get; set; }
    public float MaxSize = 120;
    public AutoSizeSpriteText()
    {
        AllowMultiline = false;
    }

    float sizeRatio;

    float TargetWidth => Parent.RelativeToAbsoluteFactor.X - Padding.TotalHorizontal;

    protected override void LoadComplete()
    {
        var width = EstimateCurrentTextWidth();
        if (width <= 0) return;
        sizeRatio = Font.Size / width;
        Font = Font.With(size: Math.Max(1, Math.Min(MaxSize, TargetWidth * sizeRatio)));
        base.LoadComplete();
    }

    protected override void Update()
    {
        // Might be a better way to do this, but this isn't too bad
        // Ideally we would use a layout listener for parent DrawWidth, but Idk how to do that
        var targetSize = Math.Max(1, Math.Min(MaxSize, TargetWidth * sizeRatio));
        if (Font.Size != targetSize)
            Font = Font.With(size: targetSize);
        base.Update();
    }

    public float EstimateCurrentTextWidth()
    {
        var textBuilder = CreateTextBuilder(FontStore);
        textBuilder.AddText(Text.ToString());
        // left padding is added to text builder offset automatically, so we subtract it to get the raw text width
        return textBuilder.Bounds.X - Padding.Left;
    }
}