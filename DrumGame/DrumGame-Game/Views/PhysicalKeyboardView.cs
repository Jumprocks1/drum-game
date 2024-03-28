using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics;
using DrumGame.Game.Commands;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;
using System.Linq;
using osu.Framework.Graphics.UserInterface;
using DrumGame.Game.Containers;
using System.Collections.Generic;
using DrumGame.Game.Interfaces;

namespace DrumGame.Game.Views;

public class PhysicalKeyboardView : Container<PhysicalKeyboardView.KeyboardKey>
{
    Command _commandFilter;
    public Command CommandFilter
    {
        get => _commandFilter; set
        {
            if (_commandFilter == value) return;
            _commandFilter = value;
            UpdateColour();
        }
    }
    public new const float Height = 6.25f;
    public new const float Width = 22.5f;
    static FontUsage font = FrameworkFont.Regular.With(size: 20f / 50);

    public PhysicalKeyboardView()
    {
        base.Width = Width;
        base.Height = Height;
    }

    bool _onlyShowAvailable;
    public bool OnlyShowAvailable
    {
        get => _onlyShowAvailable; set
        {
            if (_onlyShowAvailable == value) return;
            _onlyShowAvailable = value;
            UpdateColour();
        }
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        foreach (var key in PhysicalKeyboard.SquareKeys)
        {
            Add(new KeyboardKey(key.Item1.X, key.Item1.Y, 1, 1, key.Item2, this));
        }
        foreach (var key in PhysicalKeyboard.SpecialKeys)
        {
            Add(new KeyboardKey(key.Item1.X, key.Item1.Y, key.Item3.X, key.Item3.Y, key.Item2, this));
        }
    }

    ModifierKey LoadedModifier;

    void UpdateChildren(UIEvent e)
    {
        var modifier = e.Modifier();
        if (LoadedModifier != modifier)
        {
            LoadedModifier = modifier;
            UpdateColour();
        }
    }
    void UpdateColour()
    {
        foreach (var child in Children) child.UpdateColour();
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        UpdateChildren(e);
        return base.OnKeyDown(e);
    }
    protected override void OnKeyUp(KeyUpEvent e)
    {
        UpdateChildren(e);
        base.OnKeyUp(e);
    }

    public IEnumerable<CommandInfo> Bindings(InputKey key)
    {
        var combo = new KeyCombo(LoadedModifier, key);
        if (Util.CommandController.KeyBindings.TryGetValue(combo, out var bind))
        {
            if (bind.Count == 0) return null;
            IEnumerable<CommandInfo> res = bind;
            if (CommandFilter != Commands.Command.None)
                res = bind.Where(e => e.Command == CommandFilter);
            if (OnlyShowAvailable)
                res = bind.Where(e => Util.CommandController[e.Command].HasHandlers);
            return res;
        }
        return null;
    }

    public class KeyboardKey : CompositeDrawable, IHasMarkupTooltip, IHasContextMenu
    {
        PhysicalKeyboardView Keyboard;
        CommandController Command => Util.CommandController;
        InputKey Key;

        public void UpdateColour()
        {
            var bindings = Keyboard.Bindings(Key);
            var hasBinding = bindings != null && bindings.Any();
            Box.Colour = hasBinding ? Colour4.SkyBlue :
                Keyboard.LoadedModifier.HasKey(Key) ? Colour4.LightCoral :
                Colour4.White;
        }

        public string MarkupTooltip
        {
            get
            {
                var bindings = Keyboard.Bindings(Key);
                if (bindings != null)
                    return $"{string.Join('\n', bindings.Select(e => $"<command>{e.Name}</c>"))}\n\nRight click to configure";
                else return null;
            }
        }

        public MenuItem[] ContextMenuItems
        {
            get
            {
                var bindings = Keyboard.Bindings(Key);
                if (bindings != null)
                {
                    var builder = ContextMenuBuilder.New(this);
                    foreach (var e in bindings)
                        // TODO retry using AddMarkup here eventually
                        // our issue was that MarkupText doesn't update it's width until after the menu is generated
                        // we should ditch TextFlowContainer to fix this
                        builder.Add($"Edit Keybind - {e.Name}", _ => Util.Palette.EditKeybind(e))
                            .Color(DrumColors.BrightYellow);
                    foreach (var e in bindings)
                    {
                        builder.Add(e);
                        if (Util.CommandController[e.Command].HasHandlers)
                            builder.Color(DrumColors.Command);
                        else builder.Disable();
                    }
                    return builder.Build();
                }
                return null;
            }
        }

        Box Box;
        public KeyboardKey(float x, float y, float width, float height, InputKey key, PhysicalKeyboardView keyboard)
        {
            Key = key;
            Keyboard = keyboard;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            AddInternal(Box = new Box
            {
                Width = width - 0.1f,
                Height = height - 0.1f,
                X = 0.05f,
                Y = 0.05f,
                Origin = Anchor.TopLeft,
            });
            UpdateColour();
            AddInternal(new SpriteText
            {
                X = width / 2,
                Y = 0.27f,
                Origin = Anchor.Centre,
                Text = HotkeyDisplay.KeyString(key),
                Colour = Colour4.Black,
                Font = font
            });
        }
    }
}
