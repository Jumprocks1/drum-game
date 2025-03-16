using System;
using DrumGame.Game.Components;
using osu.Framework.Graphics;

namespace DrumGame.Game.Views.Settings.SettingInfos;

public class ButtonSettingInfo : SettingInfo
{
    public override string Description { get; }
    string ButtonText;
    public ButtonSettingInfo(string label, string description, string buttonText) : base(label)
    {
        ButtonText = buttonText;
        Description = description;
    }
    public Action<SettingControl> Action;
    public override void OnClick(SettingControl control)
    {
        Action?.Invoke(control);
    }
    public override void Render(SettingControl control)
    {
        control.Add(new DrumButton
        {
            Width = 300,
            Height = Height - 6,
            Y = 3,
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = -SettingControl.SideMargin,
            Text = ButtonText,
            Action = () => Action?.Invoke(control)
        });
    }
}