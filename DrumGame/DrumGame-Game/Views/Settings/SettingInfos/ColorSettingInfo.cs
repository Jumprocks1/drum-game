using DrumGame.Game.Components.Basic;
using DrumGame.Game.Components.ColourPicker;
using DrumGame.Game.Containers;
using DrumGame.Game.Utils;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;

namespace DrumGame.Game.Views.Settings.SettingInfos;

public class ColorSettingInfo : SettingInfo
{
    public Bindable<Colour4> Binding;
    Box ColourBox;
    public override string Description => Binding.Description;
    public ColorSettingInfo(string label, Bindable<Colour4> binding) : base(label)
    {
        Binding = binding;
    }
    public override void Render(SettingControl control)
    {
        control.Add(ColourBox = new Box
        {
            Width = 300,
            Height = Height - 10,
            Y = 5,
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = -SettingControl.SideMargin,
            Colour = Binding.Value,
        });
    }

    public override bool Open => Popover != null;

    DrumPopoverContainer.PopoverInstance Popover;

    public override void OnClick(SettingControl control)
    {
        var picker = new DrumColourPicker();
        var pickerContainer = new MouseBlockingContainer
        {
            AutoSizeAxes = Axes.Both,
            Anchor = Anchor.BottomRight,
            Origin = Anchor.TopRight,
            Child = picker
        };
        Binding.ValueChanged += ValueChanged;
        picker.Current = Binding;
        Popover = Util.GetParent<DrumPopoverContainer>(control).Popover(pickerContainer, ColourBox, false, true);
        Placeholder placeholder = null;
        Popover.OnClose = () =>
        {
            if (placeholder != null) control.ScrollContainer.Remove(placeholder, true);
            placeholder = null;
            Popover = null;
            Binding.ValueChanged -= ValueChanged;
        };
        var scroll = control.ScrollContainer;
        if (scroll != null)
        {
            var pickerBottom = control.Y + control.Height + DrumColourPicker.TotalHeight;
            var target = pickerBottom - scroll.DisplayableContent;
            // we can't scroll past the control's Y value, otherwise it will be hidden
            // this behavior is very reasonable/intuitive in my testing
            // it does require that the top of the scroll container be far enough from the bottom of the screen for it to fit the color picker
            //    this should be the case for all normal scenarios
            if (target > control.Y)
                target = control.Y;
            if (scroll.Target < target)
            {
                if (scroll.AvailableContent < pickerBottom)
                    scroll.Add(placeholder = new Placeholder { Y = pickerBottom });
                scroll.ScrollTo(target);
            }
        }
    }
    class Placeholder : Drawable { }
    public void ValueChanged(ValueChangedEvent<Colour4> e)
    {
        ColourBox.Colour = e.NewValue;
    }
}