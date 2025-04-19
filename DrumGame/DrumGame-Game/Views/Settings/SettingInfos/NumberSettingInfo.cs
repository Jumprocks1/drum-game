using System;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Fields;
using osu.Framework.Bindables;
using osu.Framework.Graphics;

namespace DrumGame.Game.Views.Settings.SettingInfos;

public class NullableParsableSettingInfo<T> : SettingInfo where T : struct, IParsable<T>
{
    public Bindable<T?> Binding;
    public override string Description => Binding.Description;
    public NullableParsableSettingInfo(string label, Bindable<T?> binding) : base(label)
    {
        Binding = binding;
    }
    public override void Render(SettingControl control)
    {
        var textBox = new DrumTextBox
        {
            Width = 300,
            Height = Height - 6,
            Y = 3,
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = -SettingControl.SideMargin,
            Text = Binding.Value.ToString(),
            CommitOnFocusLost = true
        };
        control.Add(textBox);
        textBox.OnCommit += (_, __) =>
        {
            Binding.Value = T.TryParse(textBox.Current.Value, null, out var o) ? o : null;
            textBox.Current.Value = Binding.Value.ToString();
        };
    }
}
public class ParsableSettingInfo<T> : SettingInfo where T : IParsable<T>
{
    public Bindable<T> Binding;
    public override string Description => Binding.Description;
    public ParsableSettingInfo(string label, Bindable<T> binding) : base(label)
    {
        Binding = binding;
    }
    public override void Render(SettingControl control)
    {
        control.Add(new ParsableTextBox<T>(Binding)
        {
            Width = 300,
            Height = Height - 6,
            Y = 3,
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = -SettingControl.SideMargin,
            CommitOnFocusLost = true
        });
    }
}

public class DoubleSettingInfo : ParsableSettingInfo<double>
{
    public DoubleSettingInfo(string label, Bindable<double> binding) : base(label, binding) { }
}
public class FloatSettingInfo : ParsableSettingInfo<float>
{
    public FloatSettingInfo(string label, Bindable<float> binding) : base(label, binding) { }
}
public class IntSettingInfo : ParsableSettingInfo<int>
{
    public IntSettingInfo(string label, Bindable<int> binding) : base(label, binding) { }
}
