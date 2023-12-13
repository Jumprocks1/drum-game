using System;
using DrumGame.Game.Components;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Input;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Modals;

public class NumberFieldConfig : FieldConfigBase<double?>
{
    public override IDrawableField<double?> Render(RequestModal modal) => new StringFieldConfig.StringField(modal, this, DefaultValue.ToString());
}

public class StringFieldConfig : FieldConfigBase<string>
{
    public override IDrawableField<string> Render(RequestModal modal) => new StringField(modal, this);

    public StringFieldConfig(string label = null, string defaultValue = null)
    {
        Label = label;
        DefaultValue = defaultValue;
    }

    public class StringField : DrumTextBox, IDrawableField<string>, IDrawableField<double?>, IDrawableField<int?>, IHasCustomTooltip
    {
        public FieldConfigBase Config { get; }
        public StringField(RequestModal modal, StringFieldConfig config) : this(modal, config, config.DefaultValue) { }
        public StringField(RequestModal modal, FieldConfigBase config, string defaultValue)
        {
            TabbableContentContainer = modal;
            Height = 30;
            RelativeSizeAxes = Axes.X;
            ReleaseFocusOnCommit = false;
            Config = config;
            PlaceholderText = config.Label;
            OnCommit += (_, __) => modal?.Commit();
            if (defaultValue != null) Current.Value = defaultValue;
        }
        public string Value { get => Current.Value; set => Current.Value = value; }
        object IDrawableField.Value => Current.Value;
        double? IDrawableField<double?>.Value { get => double.TryParse(Current.Value, out var d) ? d : null; set => Current.Value = value.ToString(); }
        int? IDrawableField<int?>.Value { get => int.TryParse(Current.Value, out var d) ? d : null; set => Current.Value = value.ToString(); }

        public object TooltipContent => Config.Tooltip;
        public ITooltip GetCustomTooltip() => null;

        protected override void OnFocus(FocusEvent e)
        {
            OnPressed(new KeyBindingPressEvent<PlatformAction>(GetContainingInputManager().CurrentState, PlatformAction.SelectAll));
            base.OnFocus(e);
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