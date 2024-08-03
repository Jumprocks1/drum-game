using DrumGame.Game.Commands;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;

using IKey = osuTK.Input.Key;

namespace DrumGame.Game.Modals;

public class KeyComboFieldConfig : FieldConfigBase<KeyCombo>
{
    public override IDrawableField<KeyCombo> Render(RequestModal modal) => new KeyComboField(modal, this);

    public KeyComboFieldConfig(string label = null, KeyCombo? defaultValue = null)
    {
        Label = label;
        DefaultValue = defaultValue ?? KeyCombo.None;
    }

    public class KeyComboField : CompositeDrawable, IDrawableField<KeyCombo>, IHasCustomTooltip
    {
        public FieldConfigBase Config { get; }
        public KeyCombo Value { get; set; }
        RequestModal Modal;
        public SpriteText Text;
        FillFlowContainer KeyDisplay;
        public KeyComboField(RequestModal modal, KeyComboFieldConfig config) : this(modal, config, config.DefaultValue) { }
        public KeyComboField(RequestModal modal, FieldConfigBase config, KeyCombo defaultValue)
        {
            Modal = modal;
            Height = 30;
            RelativeSizeAxes = Axes.X;
            Config = config;
            Value = defaultValue;
        }
        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(KeyDisplay = new FillFlowContainer
            {
                Direction = FillDirection.Horizontal,
                AutoSizeAxes = Axes.X,
                RelativeSizeAxes = Axes.Y
            });
            KeyDisplay.Add(Text = new SpriteText
            {
                Font = DrumFont.Regular.With(size: 18),
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft
            });
            UpdateBindingContainer(Value);
        }
        void UpdateText()
        {
            var preText = "";
            if (Value == KeyCombo.None)
                preText = "No key set";
            Text.Text = HasFocus ? $"{preText} - press keys to change" : $"{preText} - click to change";
        }
        public object TooltipContent => Config.Tooltip;
        public ITooltip GetCustomTooltip() => null;
        public override bool AcceptsFocus => true;


        protected override void OnFocus(FocusEvent e)
        {
            UpdateText();
            base.OnFocus(e);
        }

        protected override void OnFocusLost(FocusLostEvent e)
        {
            UpdateText();
            base.OnFocusLost(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            return true;
        }

        public void UpdateBindingContainer(KeyCombo combo)
        {
            KeyDisplay.Remove(Text, false);
            KeyDisplay.Clear();
            Value = combo;
            HotkeyDisplay.RenderHotkey(KeyDisplay, Value);
            KeyDisplay.Add(Text);
            UpdateText();
        }
        public void UpdateBindingContainer(osu.Framework.Input.States.KeyboardState keyboard, InputKey mainKey)
            => UpdateBindingContainer(new KeyCombo(keyboard.Modifier(), mainKey));
        public void UpdateBindingContainer(InputState state)
        {
            var keyboard = state.Keyboard;
            var mainKey = InputKey.None;
            foreach (var key in keyboard.Keys)
            {
                if (key != IKey.ShiftLeft && key != IKey.ShiftRight && key != IKey.AltLeft && key != IKey.AltRight &&
                    key != IKey.ControlLeft && key != IKey.ControlRight)
                {
                    mainKey = KeyCombination.FromKey(key);
                    break;
                }
            }
            UpdateBindingContainer(keyboard, mainKey);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (HasFocus)
            {
                if (e.Key == IKey.Escape)
                {
                    GetContainingFocusManager().TriggerFocusContention(this);
                    return true;
                }
                else if (e.Key == IKey.Enter || e.Key == IKey.KeypadEnter)
                {
                    if (Value.Key != InputKey.None)
                    {
                        Modal?.Commit();
                        return true;
                    }
                }
                UpdateBindingContainer(e.CurrentState);
                return true;
            }
            return base.OnKeyDown(e);
        }
    }
}
