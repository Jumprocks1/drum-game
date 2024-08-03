using DrumGame.Game.Components;
using osu.Framework.Bindables;
using osu.Framework.Graphics;

namespace DrumGame.Game.Views.Settings.SettingInfos;

public class StringSettingInfo : SettingInfo
{
    public Bindable<string> Binding;
    public StringSettingInfo(string label, Bindable<string> binding) : base(label)
    {
        Binding = binding;
    }
    public override void Render(SettingControl control) => control.Add(new DrumTextBox
    {
        Width = 300,
        Height = Height - 4,
        Y = 2,
        Anchor = Anchor.TopRight,
        Origin = Anchor.TopRight,
        X = -SettingControl.SideMargin,
        Text = Binding.Value?.ToString() ?? "",
        CommitOnFocusLost = true,
        Current = Binding
    });
}
