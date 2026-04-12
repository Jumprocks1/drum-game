using System;
using System.Numerics;
using DrumGame.Game.Components.Fields;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;

namespace DrumGame.Game.Views.Settings.SettingInfos;

public class SliderSettingInfo<T> : SettingInfo where T : struct, INumber<T>, IMinMaxValue<T>, IConvertible
{
    public BindableNumber<T> Binding;
    public override string Description => Binding.Description;
    public SliderSettingInfo(string label, BindableNumber<T> binding) : base(label)
    {
        Binding = binding;
    }
    public override void Render(SettingControl control)
    {
        var slider = new SettingsSlider(Binding)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = -SettingControl.SideMargin
        };
        control.Add(slider);
    }


    public class SettingsSlider : BasicSlider<T>, IHasTooltip
    {
        public override Colour4 BackgroundColour => Colour4.Transparent;
        public LocalisableString TooltipText => $"{Value.Value * T.CreateChecked(100):0.#}%";
        public SettingsSlider(BindableNumber<T> target) : base(target)
        {
        }
        protected override Container CreateThumb()
        {
            var thumb = base.CreateThumb();
            thumb.Colour = DrumColors.ActiveField;
            return thumb;
        }
    }
}
public class VolumeSliderSettingInfo : SettingInfo
{
    public BindableNumber<double> Binding;
    public override string Description => Binding.Description;
    DrumGameSetting? Setting;
    public VolumeSliderSettingInfo(string label, DrumGameSetting setting, BindableNumber<double> binding) : this(label, binding)
    {
        Setting = setting;
        Tooltip = DrumGameConfigManager.GetDescription(setting);
    }
    public VolumeSliderSettingInfo(string label, BindableNumber<double> binding) : base(label)
    {
        Binding = binding;
    }
    public override void Render(SettingControl control)
    {
        var slider = new VolumeSettingsSlider(Binding)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = -SettingControl.SideMargin
        };
        if (Setting == DrumGameSetting.MasterVolume)
        {
            // prevent overlay from appearing when changing the volume through this type of slider
            slider.BeforeSet = () => Util.DrumGame?.VolumeOverlay?.Disable();
            slider.AfterSet = () => Util.DrumGame?.VolumeOverlay?.Enable();
        }
        control.Add(slider);
    }

    public class VolumeSettingsSlider : VolumeSlider, IHasMarkupTooltip
    {
        public override Colour4 BackgroundColour => Colour4.Transparent;
        public string MarkupTooltip => $"{FormatAsDb(Value.Value)}\n{Value.Value * 100:0.#}%";
        public VolumeSettingsSlider(BindableNumber<double> target) : base(target)
        {
        }
    }
}