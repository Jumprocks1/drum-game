using System;
using DrumGame.Game.Containers;
using osu.Framework.Graphics;

namespace DrumGame.Game.Modals;

public abstract class FieldConfigBase : IFieldConfig
{
    public object Tooltip { get; set; }
    public abstract Type OutputType { get; }
    public string MarkupTooltip { get => (Tooltip as MarkupTooltipData)?.Data; set => Tooltip = new MarkupTooltipData(value); }
    public string Label { get; set; }
    public Drawable[] LabelButtons { get; set; }
    public string Key { get; set; } // optional
    public abstract bool HasCommit { get; }
    public abstract void SetDefault(object value);
    public abstract IDrawableField Render(RequestModal modal);
    public static FieldConfigBase GetConfigFor(Type t)
    {
        var under = System.Nullable.GetUnderlyingType(t);
        if (under != null)
        {
            if (under.IsEnum)
                return (FieldConfigBase)Activator.CreateInstance(typeof(NullableEnumFieldConfig<>).MakeGenericType(under));
            else if (under == typeof(double))
                return new StringFieldConfig();
            else
                return null;
        }
        else if (t.IsEnum)
        {
            var fieldType = typeof(EnumFieldConfig<>).MakeGenericType(t);
            return (FieldConfigBase)Activator.CreateInstance(fieldType);
        }
        else
        {
            return new StringFieldConfig();
        }
    }
}

public abstract class FieldConfigBase<T> : FieldConfigBase, IFieldConfig<T>
{
    public T DefaultValue { get; set; }
    public Action<T> OnCommit { get; set; }
    public override bool HasCommit => OnCommit != null;
    public override Type OutputType => typeof(T);
    public override void SetDefault(object value) => DefaultValue = (T)value;



    public override abstract IDrawableField<T> Render(RequestModal modal);
    public virtual T Convert(object v) => (T)v;
}