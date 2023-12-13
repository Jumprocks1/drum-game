using DrumGame.Game.Components.Basic;
using DrumGame.Game.Utils;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Views.Settings.SettingInfos;

public class ColorSettingInfo : SettingInfo
{
    public Bindable<Colour4> Binding;
    BasicColourPicker Picker;
    Box ColourBox;
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

    public override bool Open => Picker != null;

    class SettingsColourPicker : BasicColourPicker
    {
        readonly DrumScrollContainer scrollContainer;
        public SettingsColourPicker(DrumScrollContainer scrollContainer) { this.scrollContainer = scrollContainer; }
        protected override void LoadComplete()
        {
            base.LoadComplete();
            // can't get this to work without the 1ms delay
            Scheduler.AddDelayed(() => scrollContainer.ScrollIntoView(this), 1);
        }
        protected override bool Handle(UIEvent e)
        {
            if (base.Handle(e)) return true;
            switch (e)
            {
                case ScrollEvent:
                case MouseEvent:
                    return true;
            }
            return false;
        }
    }

    public override void OnClick(SettingControl control)
    {
        Picker = new SettingsColourPicker(control.ScrollContainer)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Y = control.Y + control.Height
        };
        Binding.ValueChanged += ValueChanged;
        Picker.Current = Binding;
        control.ScrollContainer.Add(Picker);
    }

    public void ValueChanged(ValueChangedEvent<Colour4> e)
    {
        ColourBox.Colour = e.NewValue;
    }

    public override void Close(SettingControl control)
    {
        if (Picker != null)
        {
            control.ScrollContainer.Destroy(ref Picker);
            Binding.ValueChanged -= ValueChanged;
        }
    }
    public override void Dispose()
    {
        Binding.ValueChanged -= ValueChanged;
    }
}