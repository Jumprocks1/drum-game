using DrumGame.Game.Components;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;

namespace DrumGame.Game.Views.Settings.SettingInfos;

public class BooleanSettingInfo : SettingInfo
{
    public Bindable<bool> Binding;
    public override string Description => Binding.Description;
    public BooleanSettingInfo(string label, Bindable<bool> binding) : base(label)
    {
        Binding = binding;
    }
    public override void Render(SettingControl control)
    {
        control.Add(new DrumCheckbox(24)
        {
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreLeft,
            X = -SettingControl.SideMargin - 300,
            Current = Binding,
        });
    }
    public override void OnClick(SettingControl control)
    {
        Binding.Value = !Binding.Value;
    }
}