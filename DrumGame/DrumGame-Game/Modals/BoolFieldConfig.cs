using System;
using DrumGame.Game.Components;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;

namespace DrumGame.Game.Modals;

public class BoolFieldConfig : FieldConfigBase<bool>
{
    public BoolFieldConfig(string label = null, bool defaultValue = false)
    {
        Label = label;
        DefaultValue = defaultValue;
    }
    public override IDrawableField<bool> Render(RequestModal modal) => new Field(this);

    class Field : DrumCheckbox, IDrawableField<bool>, IHasCustomTooltip
    {
        public FieldConfigBase Config { get; }
        public bool Value { get => Current.Value; set => Current.Value = value; }
        public object TooltipContent { get; set; }

        public Field(BoolFieldConfig config)
        {
            Config = config;
            Height = 30;
            TooltipContent = config.Tooltip;
            Value = config.DefaultValue;
        }
        public ITooltip GetCustomTooltip() => null;
    }
}