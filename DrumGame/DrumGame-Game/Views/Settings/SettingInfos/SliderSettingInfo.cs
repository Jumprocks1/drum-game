using DrumGame.Game.Components;
using DrumGame.Game.Utils;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;

namespace DrumGame.Game.Views.Settings.SettingInfos;

public class SliderSettingInfo : SettingInfo
{
    public BindableNumber<double> Binding;
    public SliderSettingInfo(string label, BindableNumber<double> binding) : base(label)
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


    public class SettingsSlider : BasicSlider<double>, IHasTooltip
    {
        public override Colour4 BackgroundColour => Colour4.Transparent;
        public LocalisableString TooltipText => $"{Value.Value * 100:0.#}%";
        public SettingsSlider(BindableNumber<double> target) : base(target)
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