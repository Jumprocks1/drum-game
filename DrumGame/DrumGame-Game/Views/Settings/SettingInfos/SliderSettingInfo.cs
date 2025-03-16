using System;
using System.Numerics;
using DrumGame.Game.Components;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
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
        protected override Drawable CreateThumb()
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
        control.Add(slider);
    }

    public class VolumeSettingsSlider : VolumeSliderDb, IHasMarkupTooltip
    {
        public override Colour4 BackgroundColour => Colour4.Transparent;
        public string MarkupTooltip => $"{FormatAsDb(Value.Value)}\n{Value.Value * 100:0.#}%";
        public VolumeSettingsSlider(BindableNumber<double> target) : base(target)
        {
        }
        protected override Drawable CreateThumb()
        {
            var thumb = base.CreateThumb();
            thumb.Colour = DrumColors.ActiveField;
            return thumb;
        }
    }
}