using System;
using DrumGame.Game.Commands;
using DrumGame.Game.Interfaces;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;

namespace DrumGame.Game.Components;

public class VolumeControl : CompositeDrawable
{
    public const float Thickness = 20;
    public const float SliderHeight = 100;
    public string HelperText;
    private readonly VolumeButton button;
    public readonly IVolumeBinding Target;
    public readonly string Label;
    protected override bool OnScroll(ScrollEvent e)
    {
        Target.Aggregate.Value = Math.Clamp(Math.Round(Target.Aggregate.Value + Math.Sign(e.ScrollDelta.Y) * 0.02, 2), 0, 1);
        return true;
    }
    public VolumeControl(IVolumeBinding target, string label, IconUsage icon, VolumeButton customButton = null,
        string helperText = null)
    {
        HelperText = helperText;
        Label = label;
        Target = target;
        Width = Thickness;
        var topIconHeight = Thickness * 0.75f;
        var bottomIconHeight = Thickness * 0.75f;
        Height = topIconHeight + SliderHeight + bottomIconHeight;
        AddInternal(new Box
        {
            Colour = new Colour4(100, 4, 4, 220),
            RelativeSizeAxes = Axes.Both
        });
        AddInternal(new VolumeSlider(this)
        {
            Y = topIconHeight
        });

        // add label sprite after slider so it's tooltip gets priority
        AddInternal(new LabelSpriteContainer(new SpriteIcon
        {
            Icon = icon,
            Size = new Vector2(Thickness * 0.8f),
            Y = Thickness * 0.5f,
            Anchor = Anchor.TopCentre,
            Origin = Anchor.Centre,
        }, label, this)
        {
            Size = new Vector2(Thickness),
        });

        button = customButton ?? new VolumeButton();
        button.Control = this;
        button.UpdateIcon(Target.ComputedValue);
        Target.ComputedValueChanged += button.UpdateIcon;
        button.Y = SliderHeight + topIconHeight;
        button.Height = bottomIconHeight;
        button.RelativeSizeAxes = Axes.X;
        AddInternal(button);
    }
    protected override void Dispose(bool isDisposing)
    {
        if (button != null) Target.ComputedValueChanged -= button.UpdateIcon;
        base.Dispose(isDisposing);
    }
}
public class LabelSpriteContainer : CompositeDrawable, IHasMarkupTooltip
{
    public string MarkupTooltip { get; }
    public LabelSpriteContainer(Drawable sprite, string label, VolumeControl control)
    {
        if (!label.EndsWith(" Volume"))
            label += " Volume";
        MarkupTooltip = $"<brightGreen>{label}</c>";
        if (control.HelperText != null)
            MarkupTooltip += '\n' + control.HelperText;
        AddInternal(sprite);
    }
}
public class VolumeSlider : BasicSlider<double>, IHasMarkupTooltip
{
    protected override void UpdateThumb(double _) => base.UpdateThumb(Control.Target.ComputedValue);
    public override Colour4 BackgroundColour => Colour4.Transparent;

    public string MarkupTooltip => $"<brightGreen>{Control.Label}</c>: {Value.Value * 100:0.#}%";
    public readonly VolumeControl Control;
    public VolumeSlider(VolumeControl control) : base(control.Target.Aggregate)
    {
        Control = control;
        Direction = Direction.Vertical;
        Thickness = VolumeControl.Thickness;
        Length = VolumeControl.SliderHeight;
        Control.Target.ComputedValueChanged += UpdateThumb;
    }
    protected override void Dispose(bool isDisposing)
    {
        Control.Target.ComputedValueChanged -= UpdateThumb;
        base.Dispose(isDisposing);
    }
    protected override Drawable CreateThumb() => new Circle
    {
        Colour = Colour4.CornflowerBlue,
        Origin = Anchor.Centre,
        Width = 14,
        Height = 14,
        RelativePositionAxes = SlideAxis
    };
}
public class VolumeButton : CompositeDrawable, IHasCommand
{
    public Command Command { get; set; }
    public bool AllowClick => true;
    public IconUsage? Icon;
    public void UpdateIcon(double volume)
    {
        icon.Icon = Icon ?? (volume == 0 ? FontAwesome.Solid.VolumeMute :
               //    volume < 0.25 ? FontAwesome.Solid.VolumeOff :
               //    volume < 0.5 ? FontAwesome.Solid.VolumeDown :
               FontAwesome.Solid.VolumeUp);
    }
    public VolumeControl Control;
    readonly SpriteIcon icon;

    string IHasMarkupTooltip.MarkupTooltip
    {
        get
        {
            if (Command == Command.SetNormalizedRelativeVolume)
                return IHasCommand.GetMarkupTooltip(Command);
            var text = Control.Target.ComputedValue == 0 ? $"Unmute {Control.Label}" : $"Mute {Control.Label}";
            return Command != Command.None ? IHasCommandInfo.GetMarkupTooltip(text, IHasCommand.GetMarkupHotkeyString(Command)) : text;
        }
    }

    public VolumeButton()
    {
        AddInternal(icon = new SpriteIcon
        {
            // unfortunately we can't really move the hitbox for the tooltip/click up with the icon due to the slider
            Y = -VolumeControl.Thickness * 0.2f,
            Size = new Vector2(VolumeControl.Thickness * 0.8f),
            Origin = Anchor.Centre,
            Anchor = Anchor.Centre,
        });
    }
    protected override bool OnMouseDown(MouseDownEvent e) => true;
    protected override bool OnClick(ClickEvent e)
    {
        // The input manager will automatically skip this call if we route through the command controller
        // since we have AllowClick, this will be skipped whenever Command is set
        Control.Target.ToggleMute();
        return base.OnClick(e);
    }
}

