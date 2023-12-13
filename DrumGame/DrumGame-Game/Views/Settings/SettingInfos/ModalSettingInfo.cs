using DrumGame.Game.Components;
using DrumGame.Game.Interfaces;
using osu.Framework.Allocation;
using osu.Framework.Graphics;

namespace DrumGame.Game.Views.Settings.SettingInfos;

public class ModalSettingInfo<T> : SettingInfo where T : IModal, new()
{
    public ModalSettingInfo(string label) : base(label)
    {
    }
    public override void Render(SettingControl control)
    {
        control.Add(new DrumButton
        {
            Text = "Edit",
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            RelativeSizeAxes = Axes.Y,
            Width = 300,
            X = -SettingControl.SideMargin,
            Action = () => control.Dependencies.Get<SettingsView>().AddModal<T>(),
        });
    }
    public override void OnClick(SettingControl control)
    {
        control.Dependencies.Get<SettingsView>().AddModal<T>();
    }
}