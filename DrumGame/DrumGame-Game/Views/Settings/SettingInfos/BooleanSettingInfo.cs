using DrumGame.Game.Utils;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;

namespace DrumGame.Game.Views.Settings.SettingInfos;

public class BooleanSettingInfo : SettingInfo
{
    public Bindable<bool> Binding;
    public override string Description => Binding.Description;
    Box Box;
    public BooleanSettingInfo(string label, Bindable<bool> binding) : base(label)
    {
        Binding = binding;
    }
    public override void Render(SettingControl control)
    {
        control.Add(new Box
        {
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreLeft,
            X = -SettingControl.SideMargin - 300,
            Height = 24,
            Width = 24,
            Colour = DrumColors.CheckboxBackground
        });
        control.Add(Box = new Box
        {
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreLeft,
            X = -SettingControl.SideMargin - 300 + 2,
            Height = 20,
            Width = 20
        });
        UpdateBoxColor();
    }
    public void UpdateBoxColor() =>
        Box.Colour = Binding.Value ? DrumColors.ActiveCheckbox : DrumColors.CheckboxBackground;
    public override void OnClick(SettingControl control)
    {
        Binding.Value = !Binding.Value;
        UpdateBoxColor();
    }
}