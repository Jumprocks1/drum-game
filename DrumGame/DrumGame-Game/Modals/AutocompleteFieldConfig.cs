using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Components.Basic.Autocomplete;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;

namespace DrumGame.Game.Modals;

public class EnumFieldConfig<T> : FieldConfigBase<T> where T : struct, Enum
{
    public override IDrawableField<T> Render(RequestModal modal) => new Field(this);
    public List<T> Values;
    class Field : EnumAutocomplete<T>, IDrawableField<T>, IHasCustomTooltip
    {
        public FieldConfigBase Config { get; }
        public object TooltipContent { get; set; }
        public ITooltip GetCustomTooltip() => null;
        public T Value
        {
            get => target.Value; set
            {
                var eq = EqualityComparer<T>.Default;
                Target = Options.First(e => eq.Equals(e.Value, value));
            }
        }
        public Field(EnumFieldConfig<T> config) : base(config.DefaultValue, config.Values)
        {
            Config = config;
            TooltipContent = config.Tooltip;
            RelativeSizeAxes = Axes.X;
            OnSelect += value =>
            {
                var modal = Util.GetParent<RequestModal>(this);
                if (modal == null) return;
                if (modal.Config.Fields.Length == 1)
                    modal.Commit();
            };
        }
    }
}

public class NullableEnumFieldConfig<T> : FieldConfigBase<T?> where T : struct, Enum
{
    public override IDrawableField<T?> Render(RequestModal modal) => new Field(this);
    class Field : EnumAutocomplete<T>, IDrawableField<T?>, IHasCustomTooltip
    {
        public FieldConfigBase Config { get; }
        public object TooltipContent { get; set; }
        public ITooltip GetCustomTooltip() => null;
        public T? Value
        {
            get => target?.Value; set
            {
                var eq = EqualityComparer<T?>.Default;
                Target = Options.First(e => eq.Equals(e?.Value, value));
            }
        }
        public Field(NullableEnumFieldConfig<T> config) : base(config.DefaultValue, nullable: true)
        {
            Config = config;
            TooltipContent = config.Tooltip;
            RelativeSizeAxes = Axes.X;
            OnFocusChange += focused =>
            {
                if (!focused)
                {
                    var modal = Util.GetParent<RequestModal>(this);
                    if (modal == null) return;
                    if (modal.Config.Fields.Length == 1)
                        modal.Close();
                }
            };
            OnSelect += value =>
            {
                var modal = Util.GetParent<RequestModal>(this);
                if (modal == null) return;
                if (modal.Config.Fields.Length == 1)
                    modal.Commit();
            };
        }
    }
}

public class AutocompleteFieldConfig : AutocompleteFieldConfig<BasicAutocompleteOption>
{
    public new string[] Options { set => base.Options = value.Select(e => new BasicAutocompleteOption(e)).ToArray(); }
    public new string DefaultValue { set => base.DefaultValue = base.Options.FirstOrDefault(e => e.Name == value); }
    public new Action<string> OnCommit { set => base.OnCommit = e => value(e?.Name); }

    public static AutocompleteFieldConfig FromOptions(IEnumerable<string> options, string current = null)
    {
        string[] basicOptions;
        if (current == null || options.Contains(current))
        {
            basicOptions = options.AsArray();
        }
        else
        {
            basicOptions = options.Append(current).AsArray();
        }
        return new() { Options = basicOptions, DefaultValue = current };
    }
}
public class AutocompleteFieldConfig<T> : FieldConfigBase<T> where T : class, IFilterable
{
    public T[] Options;
    public IEnumerable<Drawable> Buttons;
    public override IDrawableField<T> Render(RequestModal modal) => new Field(modal, this);
    class Field : Autocomplete<T>, IDrawableField<T>
    {
        public FieldConfigBase Config { get; }
        public Field(RequestModal modal, AutocompleteFieldConfig<T> config)
        {
            Config = config;
            Input.TabbableContentContainer = modal;
            if (config.Buttons != null)
            {
                var x = -5f;
                foreach (var button in config.Buttons)
                {
                    button.X = x;
                    button.Anchor = Anchor.CentreRight;
                    button.Origin = Anchor.CentreRight;
                    Input.Add(button);
                    x -= button.Width + 5;
                }
            }
            ClearOnFocus = true;
            Options = config.Options;
            RelativeSizeAxes = Axes.X;
            if (config.DefaultValue != null)
                CommittedTarget = Options.FirstOrDefault(e => e == config.DefaultValue);
            OnSelect += value =>
            {
                var modal = Util.GetParent<RequestModal>(this);
                if (modal == null) return;
                if (modal.Config.Fields.Length == 1)
                    modal.Commit();
            };
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == osuTK.Input.Key.Escape) // kill modal instead of just losing focus
            {
                var modal = Util.GetParent<RequestModal>(this);
                if (modal != null)
                {
                    modal.Close();
                    return true;
                }
            }
            return base.OnKeyDown(e);
        }
        public T Value { get => Target; set { Target = Options.First(e => e == value); } }
    }
}
public class FreeSoloFieldConfig : FieldConfigBase<string>
{
    public string[] Options;
    public IEnumerable<Drawable> Buttons;
    public override IDrawableField<string> Render(RequestModal modal) => new Field(modal, this);
    class Field : AutocompleteFreeSolo, IDrawableField<string>
    {
        public FieldConfigBase Config { get; }
        public Field(RequestModal modal, FreeSoloFieldConfig config)
        {
            Config = config;
            Input.TabbableContentContainer = modal;
            if (config.Buttons != null)
            {
                var x = -5f;
                foreach (var button in config.Buttons)
                {
                    button.X = x;
                    button.Anchor = Anchor.CentreRight;
                    button.Origin = Anchor.CentreRight;
                    Input.Add(button);
                    x -= button.Width + 5;
                }
            }
            Options = config.Options;
            RelativeSizeAxes = Axes.X;
            OnCommit += value =>
            {
                var modal = Util.GetParent<RequestModal>(this);
                if (modal == null) return;
                if (modal.Config.Fields.Length == 1)
                    modal.Commit();
            };
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == osuTK.Input.Key.Escape) // kill modal instead of just losing focus
            {
                var modal = Util.GetParent<RequestModal>(this);
                if (modal != null)
                {
                    modal.Close();
                    return true;
                }
            }
            return base.OnKeyDown(e);
        }
    }
}