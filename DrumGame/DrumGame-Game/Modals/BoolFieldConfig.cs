using DrumGame.Game.Components;
using osu.Framework.Graphics.Cursor;

namespace DrumGame.Game.Modals;

public class BoolFieldConfig : FieldConfigBase<bool>
{
    public const float Height = 30;
    public BoolFieldConfig(string label = null, bool defaultValue = false)
    {
        Label = label;
        DefaultValue = defaultValue;
    }
    public override IDrawableField<bool> Render(RequestModal modal) => new Field(this);

    // TODO tooltip should show up when hovering label
    class Field : DrumCheckbox, IDrawableField<bool>, IHasCustomTooltip
    {
        public FieldConfigBase Config { get; }
        public bool Value { get => Current.Value; set => Current.Value = value; }
        public object TooltipContent { get; set; }

        public Field(BoolFieldConfig config)
        {
            Config = config;
            Height = BoolFieldConfig.Height;
            TooltipContent = config.Tooltip;
            Value = config.DefaultValue;
        }
        public ITooltip GetCustomTooltip() => null;
    }
}