using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Layout;
using osuTK;

namespace DrumGame.Game.Containers;

public class DrumContextMenu : Menu
{
    readonly LayoutValue SizeCache;
    public DrumContextMenu() : base(Direction.Vertical)
    {
        BackgroundColour = DrumColors.DarkBackground;
        MaskingContainer.BorderColour = DrumColors.LightBorder;
        MaskingContainer.BorderThickness = 2; // technically this border eats into the item size a little
        ItemsContainer.Padding = new MarginPadding { Vertical = 8 };

        SizeCache = (LayoutValue)ItemsContainer.GetType().GetProperty("SizeCache").GetValue(ItemsContainer);
    }

    protected override Menu CreateSubMenu() => new DrumContextMenu()
    {
        Anchor = Direction == Direction.Horizontal ? Anchor.BottomLeft : Anchor.TopRight
    };

    protected override DrawableMenuItem CreateDrawableMenuItem(MenuItem item) => new DrumDrawableMenuItem(item);

    protected override ScrollContainer<Drawable> CreateScrollContainer(Direction direction) => new DrumScrollContainer(direction)
    {
        ClampExtension = 0
    };


    protected override void UpdateAfterChildren()
    {
        // Copied from UpdateAfterChildren for Menu
        if (!SizeCache.IsValid)
        {
            float width = 0;

            foreach (var item in Children.Cast<DrumDrawableMenuItem>())
                width = Math.Max(width, item.MinimumDrawWidth);

            width = Math.Min(MaxWidth, width);
            float height = Math.Min(MaxHeight, ItemsContainer.Height);

            // Regardless of the above result, if we are relative-sizing, just use the stored width/height
            width = RelativeSizeAxes.HasFlagFast(Axes.X) ? Width : width;
            height = RelativeSizeAxes.HasFlagFast(Axes.Y) ? Height : height;

            if (State == MenuState.Closed && Direction == Direction.Horizontal)
                width = 0;
            if (State == MenuState.Closed && Direction == Direction.Vertical)
                height = 0;

            UpdateSize(new Vector2(width, height));

            SizeCache.Validate();
        }
    }

    public class DrumDrawableMenuItem : DrawableMenuItem, IHasMarkupTooltip
    {
        bool Disabled => Item.Action.Disabled;
        public new DrumMenuItem Item => (DrumMenuItem)base.Item;
        public string MarkupTooltip { get; set; }

        public DrumDrawableMenuItem(MenuItem item)
            : base(item)
        {
            MarkupTooltip = Item.MarkupTooltip;
            BackgroundColour = DrumColors.DarkBackground;
            BackgroundColourHover = item.Action.Disabled ? DrumColors.FieldBackground : DrumColors.DarkActiveButton;
            Foreground.AutoSizeAxes = Axes.Y;
            Foreground.RelativeSizeAxes = Axes.X;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            MinimumDrawWidth += text.DrawWidth;
            if (hotkeyDisplay != null)
            {
                foreach (var child in hotkeyDisplay.Children)
                    MinimumDrawWidth += child.DrawWidth;
            }
        }

        public float MinimumDrawWidth = 0;

        Drawable text;
        FillFlowContainer hotkeyDisplay;

        const float Spacing = 16;

        protected override Drawable CreateContent()
        {
            var col = Item.TextColor;
            if (Item.MarkupText) text = new MarkupText { Font = FrameworkFont.Regular, AutoSizeAxes = Axes.X };
            else text = new SpriteText { Font = FrameworkFont.Regular, };
            text.Anchor = Anchor.CentreLeft;
            text.Origin = Anchor.CentreLeft;
            text.Height = 20;
            text.X = 16;
            text.Colour = Disabled ? col.Darken(0.5f) : col;

            MinimumDrawWidth += Spacing * 2; // text padding

            if (Item.Bindings != null)
            {
                var container = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 20
                };
                if (text is IHasText t)
                    t.Text = Item.Text.Value;
                container.Add(text);

                MinimumDrawWidth += Spacing; // spacing between hotkey and text

                hotkeyDisplay = HotkeyDisplay.RenderKeys(Item.Bindings);
                hotkeyDisplay.X = -Spacing;
                hotkeyDisplay.Anchor = Anchor.CentreRight;
                hotkeyDisplay.Origin = Anchor.CentreRight;

                container.Add(hotkeyDisplay);
                return container;
            }
            return text;
        }
    }
}

public class DrumMenuItem : MenuItem
{
    public DrumMenuItem(string text, Action action) : base(text, action) { }
    public Colour4 TextColor = Colour4.White;
    public bool MarkupText; // applies markup to supplied text
    public List<KeyCombo> Bindings;
    public string MarkupTooltip;
}

public static class ContextMenuBuilder
{
    public static ContextMenuBuilder<T> New<T>(T t) => new(t);
}

public class ContextMenuBuilder<T>
{
    List<DrumMenuItem> items = new();
    public readonly T Target;
    public ContextMenuBuilder(T target)
    {
        Target = target;
    }
    public ContextMenuBuilder<T> Modify(Action<ContextMenuBuilder<T>> modify)
    {
        modify?.Invoke(this);
        return this;
    }
    public ContextMenuBuilder<T> Add(string name, Action<T> action)
    {
        items.Add(new DrumMenuItem(name, () => action(Target)));
        return this;
    }
    public ContextMenuBuilder<T> AddMarkup(string markupText, Action<T> action)
    {
        items.Add(new DrumMenuItem(markupText, () => action(Target)) { MarkupText = true });
        return this;
    }
    public ContextMenuBuilder<T> Add(Command command) => Add(Util.CommandController[command]);
    public ContextMenuBuilder<T> Add(CommandInfo command)
    {
        var menuItem = new DrumMenuItem(command.Name, () => Util.CommandController.ActivateCommand(command.WithParameter(Target)))
        {
            // we don't display the main command tooltip since that's already represented in the menu
            // the menu already shows hotkeys + name 
            MarkupTooltip = command.HelperMarkup,
            Bindings = command.Bindings
        };
        items.Add(menuItem);
        return this;
    }
    public ContextMenuBuilder<T> Hide(bool hide = true)
    {
        if (hide) items.RemoveAt(items.Count - 1);
        return this;
    }
    public ContextMenuBuilder<T> Danger()
    {
        items[^1].TextColor = DrumColors.BrightDangerText;
        return this;
    }
    public ContextMenuBuilder<T> Tooltip(string markupTooltip)
    {
        items[^1].MarkupTooltip = markupTooltip;
        return this;
    }
    public ContextMenuBuilder<T> Color(Colour4 color)
    {
        items[^1].TextColor = color;
        return this;
    }
    public ContextMenuBuilder<T> Disabled(bool disabled)
    {
        items[^1].Action.Disabled = disabled;
        return this;
    }
    public ContextMenuBuilder<T> Disabled(string reason)
    {
        if (reason != null)
        {
            items[^1].Action.Disabled = true;
            items[^1].MarkupTooltip = reason;
        }
        return this;
    }
    public ContextMenuBuilder<T> Disable()
    {
        items[^1].Action.Disabled = true;
        return this;
    }
    public DrumMenuItem[] Build() => items.ToArray();
}
