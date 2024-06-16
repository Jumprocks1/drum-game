using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Utils;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;

namespace DrumGame.Game.Components.Basic.Autocomplete;

public class EnumOption<T> : IFilterable where T : struct, Enum
{
    public readonly T Value;
    public EnumOption(T value)
    {
        Value = value;
        Name = value.DisplayName();
    }
    public string Name { get; }
    public string MarkupTooltip => Util.MarkupDescription(Value);
}

public class EnumAutocomplete<T> : Autocomplete<EnumOption<T>>, IHasTooltip where T : struct, Enum
{
    Bindable<T> Binding;
    public EnumAutocomplete(Bindable<T> binding)
    : this(binding.Value)
    {
        Binding = binding.GetBoundCopy();
        Input.FocusChanged += f =>
        {
            if (!f) CommittedTarget = Options.First(e => e.Value.Equals(Binding.Value));
        };
        OnSelect += option =>
        {
            if (option != null) Binding.Value = option.Value;
        };
        Binding.BindValueChanged(ev =>
        {
            CommittedTarget = Options.First(e => e.Value.Equals(ev.NewValue));
        });
    }
    public EnumAutocomplete(T current = default, IEnumerable<T> values = null) : this(current, false, values) { }
    public EnumAutocomplete(T? current = default, bool nullable = true, IEnumerable<T> values = null)
    {
        var options = (values ?? Enum.GetValues(typeof(T)).Cast<T>()).Select(e => new EnumOption<T>(e));
        if (nullable) options = options.Append(null);
        Options = options.ToList();
        CommittedTarget = current == null ? null : Options.First(e => e != null && e.Value.Equals(current));
        ClearOnFocus = true;
    }
    public LocalisableString TooltipText => "Click to edit";
}