using DrumGame.Game.Components.Abstract;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Components.Overlays;

public class VolumeOverlay : FadeContainer
{
    public override double VisibleTime => 500;
    Circle Bar;
    SpriteIcon Icon;
    SpriteText Text;
    public void VolumeUpdated(ValueChangedEvent<double> e)
    {
        GenerateInternals();
        var value = e.NewValue;
        Icon.Icon = value == 0 ? FontAwesome.Solid.VolumeMute :
            value > e.OldValue ? FontAwesome.Solid.VolumeUp :
            FontAwesome.Solid.VolumeDown;
        var iconSize = e.NewValue == 0 ? 0.899f :
            e.NewValue > e.OldValue ? 1f :
            0.67f;
        Icon.Height = baseVolumeSize * iconSize;
        Icon.Width = baseVolumeSize * iconSize;
        Text.Text = value.ToString("0%");
        Bar.Width = (float)((Width - Padding * 2) * value);
        Touch();
    }
    public new const float Width = 324;
    public new const float Height = baseVolumeSize + barHeight + Padding * 3;
    new const float Padding = 15;
    const float baseVolumeSize = 100f;
    const float barHeight = 10;
    public VolumeOverlay()
    {
        base.Width = Width;
        base.Height = Height;
        Anchor = Anchor.TopCentre;
        Origin = Anchor.TopCentre;
        Y = 100;
    }
    void GenerateInternals()
    {
        if (Bar != null) return;

        AddInternal(Icon = new SpriteIcon
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.CentreLeft,
            X = Padding,
            Y = baseVolumeSize / 2 + Padding,
        });

        AddInternal(Bar = new Circle
        {
            Height = barHeight,
            X = Padding,
            Y = Height - barHeight - Padding
        });

        AddInternal(Text = new SpriteText
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.CentreRight,
            X = -Padding,
            Y = baseVolumeSize / 2 + Padding,
            Font = new FontUsage(size: baseVolumeSize * 0.6f)
        });
    }
}