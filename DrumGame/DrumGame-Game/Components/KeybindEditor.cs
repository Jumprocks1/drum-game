
using System;
using System.Collections.Generic;
using DrumGame.Game.Commands;
using DrumGame.Game.Containers;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Components;

public class KeybindEditor : CompositeDrawable, IModal, IAcceptFocus
{
    public Action CloseAction { get; set; }
    public const float Spacing = 4;
    public const float SideSpacing = 24;
    public const float RowHeight = 22;
    public static readonly FontUsage Font = FrameworkFont.Regular.With(size: RowHeight);
    [Resolved] CommandController Command { get; set; }
    [Resolved] KeybindConfigManager ConfigManager { get; set; }
    string search = null;
    ScrollContainer<Drawable> ScrollContainer;
    SearchTextBox SearchBox;
    Container Inner;
    public KeybindEditor()
    {
        var textBoxSize = 40;
        AddInternal(SearchBox = new SearchTextBox
        {
            RelativeSizeAxes = Axes.X,
            Height = textBoxSize
        });
        AddInternal(Inner = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Padding = new MarginPadding { Top = textBoxSize }
        });
        Inner.Add(new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = DrumColors.DarkBackground
        });
        SearchBox.OnCommit += (_, __) =>
        {
            if (ScrollContainer?.Children[0] is KeybindEditorButton target)
            {
                target.TriggerClick();
            }
        };
        SearchBox.Current.ValueChanged += e =>
        {
            search = e.NewValue;
            UpdateHotkeyList();
        };
        var x = -10f;
        const float spacing = 6;
        AddInternal(new CommandIconButton(Commands.Command.OpenKeyboardView, FontAwesome.Regular.Keyboard, 40)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = x
        });
        x -= 40 + spacing;
        AddInternal(new CommandIconButton(Commands.Command.RevealInFileExplorer, FontAwesome.Solid.FolderOpen, 32)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = x,
            Y = 4
        });
        x -= 32 + spacing;
        AddInternal(new CommandIconButton(Commands.Command.OpenExternally, FontAwesome.Solid.FileAlt, 24)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = x,
            Y = 8
        });
        x -= 24 + spacing;
        AddInternal(new CommandIconButton(Commands.Command.ReloadKeybindsFromFile, FontAwesome.Solid.Sync, 24)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = x,
            Y = 8
        });
        ScrollContainer = new NoDragScrollContainer()
        {
            RelativeSizeAxes = Axes.Both
        };
        Inner.Add(ScrollContainer);
    }
    [BackgroundDependencyLoader]
    private void load()
    {
        UpdateHotkeyList();
        ConfigManager.KeybindChanged += UpdateHotkeyList;
        Command.RegisterHandlers(this);
    }
    public void Focus(IFocusManager _) => SearchBox.TakeFocus();
    protected override void Dispose(bool isDisposing)
    {
        Command.RemoveHandlers(this);
        ConfigManager.KeybindChanged -= UpdateHotkeyList;
        base.Dispose(isDisposing);
    }
    protected override bool OnMouseDown(MouseDownEvent e)
    {
        return base.OnMouseDown(e) || true; // prevent closing editor when we click somewhere inside
    }
    [CommandHandler] public void RevealInFileExplorer() => ConfigManager.RevealInFileExplorer();
    [CommandHandler] public void OpenExternally() => ConfigManager.OpenExternally();

    public List<CommandInfo> FilteredCommands = new();

    public void UpdateFilter()
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            FilteredCommands = Command.OrderedCommands;
        }
        else
        {
            FilteredCommands = new();
            var s = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var commands = Command.OrderedCommands;
            for (var i = 0; i < commands.Count; i++)
            {
                var c = commands[i];
                if (c.MatchesSearch(s))
                {
                    FilteredCommands.Add(c);
                }
            }
        }
    }
    public void UpdateHotkeyList()
    {
        UpdateFilter();
        ScrollContainer.Clear();
        var commands = FilteredCommands;
        if (commands.Count == 0)
        {
            ScrollContainer.Add(new SpriteText
            {
                Text = "No commands found",
                Y = Spacing,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Font = Font.With(size: 40)
            });
        }
        var y = 0f;
        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            var hasBindings = command.Bindings.Count > 0;
            for (var j = 0; j < Math.Max(1, command.Bindings.Count); j++)
            {
                var k = j; // store for lambda
                var row = new KeybindEditorButton
                {
                    Height = RowHeight + Spacing * 2,
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = y,
                    BackgroundColour = i % 2 == 0 ? DrumColors.RowHighlight : DrumColors.RowHighlightSecondary,
                    Action = () => { Util.Palette.EditKeybind(command, hasBindings ? k : -1); },
                    Text = command.Name
                };
                y += row.Height;

                if (hasBindings)
                {
                    var hotkeyContainer = new FillFlowContainer
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -SideSpacing - 8, // 8 for scrollbar
                        Direction = FillDirection.Horizontal,
                        AutoSizeAxes = Axes.X
                    };
                    HotkeyDisplay.RenderHotkey(hotkeyContainer, command.Bindings[j]);
                    row.Add(hotkeyContainer);
                }
                ScrollContainer.Add(row);
            }
        }
    }
}
public class KeybindEditorButton : BasicButton
{
    protected override SpriteText CreateText()
    {
        return new SpriteText
        {
            Depth = -1,
            Origin = Anchor.CentreLeft,
            Anchor = Anchor.CentreLeft,
            Font = KeybindEditor.Font,
            X = KeybindEditor.SideSpacing
        };
    }
}

