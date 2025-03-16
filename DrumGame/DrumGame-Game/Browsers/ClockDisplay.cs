using System;
using System.Globalization;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;

namespace DrumGame.Game.Browsers;

public class ClockDisplay : CompositeDrawable, IHasTooltip
{
    DateTimeFormatInfo Formats => CultureInfo.InstalledUICulture.DateTimeFormat;
    SpriteText TimeText;
    SpriteText DateText;
    public ClockDisplay(float size)
    {
        AutoSizeAxes = Axes.Both;
        var timeSize = size * 0.6f;
        AddInternal(TimeText = new SpriteText
        {
            Font = FrameworkFont.Regular.With(size: timeSize)
        });
        AddInternal(DateText = new SpriteText
        {
            Font = FrameworkFont.Regular.With(size: size - timeSize),
            Y = timeSize
        });
        UpdateText();
    }
    int loaded = -1;
    public LocalisableString TooltipText => DateTime.Now.ToString("G", Formats).Replace('\u202F', ' ');
    void UpdateText()
    {
        var time = DateTime.Now;
        var min = time.Minute;
        if (min != loaded)
        {
            TimeText.Text = time.ToString(Formats.ShortTimePattern).Replace('\u202F', ' ');
            DateText.Text = time.ToString(Formats.ShortDatePattern);
            loaded = min;
        }
    }

    protected override void Update()
    {
        UpdateText();
        base.Update();
    }
}