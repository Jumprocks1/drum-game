using System;
using System.Collections.Generic;
using DrumGame.Game.Components.Basic.Autocomplete;
using osu.Framework.Bindables;
using osu.Framework.Graphics;

namespace DrumGame.Game.Views.Settings.SettingInfos;

public class DropdownSettingInfo<T> : SettingInfo where T : class, IFilterable
{
    public override string Description => Binding.Description;
    public Bindable<T> Binding;
    public List<T> Options;
    public DropdownSettingInfo(string label, Bindable<T> binding) : base(label)
    {
        Binding = binding;
    }
    public override void Render(SettingControl control)
    {
        Autocompete = new Autocomplete<T>
        {
            Width = 300,
            Height = Height,
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = -SettingControl.SideMargin,
            Options = Options,
            CommittedTarget = Binding.Value,
            ClearOnFocus = true
        };
        // Input.FocusChanged += f =>
        // {
        //     if (!f) CommittedTarget = Options.First(e => e.Value.Equals(Binding.Value));
        // };
        Autocompete.OnSelect += e => Binding.Value = e;
        Binding.ValueChanged += TargetChanged;
        control.Add(Autocompete);
    }
    void TargetChanged(ValueChangedEvent<T> e) => Autocompete.CommittedTarget = e.NewValue;
    Autocomplete<T> Autocompete;

    public override void Dispose()
    {
        Binding.ValueChanged -= TargetChanged;
        base.Dispose();
    }
}