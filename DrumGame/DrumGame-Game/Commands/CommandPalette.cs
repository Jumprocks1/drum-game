using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osuTK.Input;

namespace DrumGame.Game.Commands;

public class CommandPalette : CompositeDrawable
{
    public bool Visible => Alpha != 0;
    const float SearchHeight = 30;
    PaletteSearch Input;
    CommandController CommandController => Util.CommandController;
    DrumScrollContainer ScrollContainer;
    Container ScrollPadder;
    const int CommandDisplayCount = 12; // number of commands displayed before requiring scroll
                                        // note that this should match the width of the scrollbar in our scroll container
    public new const float Margin = 8;
    Container ButtonContainer = new()
    {
        Padding = new MarginPadding(Margin),
        AutoSizeAxes = Axes.Y,
        RelativeSizeAxes = Axes.X
    };
    private int? _targetI;
    public int? TargetI
    {
        get => _targetI; set
        {
            if (FilteredCommands.Count == 0) _targetI = null;
            else
            {
                _targetI = value?.Mod(FilteredCommands.Count);
                Target = _targetI.HasValue ? FilteredCommands[_targetI.Value] : null;
            }
        }
    }
    bool RequireHandler = false;
    public CommandInfo Target = null;
    List<CommandInfo> FilteredCommands = new();
    public CommandPalette()
    {
        Hide();
        AutoSizeAxes = Axes.Y;
        Width = 0.5f;
        RelativeSizeAxes = Axes.X;
        Anchor = Anchor.TopCentre;
        Origin = Anchor.TopCentre;
        AddInternal(new Box
        {
            Colour = DrumColors.DarkBorder,
            RelativeSizeAxes = Axes.Both
        });
        AddInternal(new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Padding = new MarginPadding { Bottom = 2, Left = 2, Right = 2 },
            Children = new Drawable[] {
                new Box
                {
                    Colour = DrumColors.DarkBackground,
                    RelativeSizeAxes = Axes.Both
                },
                ButtonContainer
            }
        });
        ButtonContainer.Add(Input = new PaletteSearch
        {
            Height = SearchHeight,
            RelativeSizeAxes = Axes.X,
        });
        Input.OnCommit += (_, _) =>
        {
            if (FilteredCommands.Count > 0)
            {
                Hide();
                CommandController.ActivateCommand(Target);
            }
        };
        Input.Current.ValueChanged += v => UpdateSearch(v.NewValue);
        ButtonContainer.Add(ScrollPadder = new Container
        {
            RelativeSizeAxes = Axes.X,
            Y = SearchHeight + Margin,
            // this lets us place the scrollbar into the side margin
            // we need this separate container since BasicScrollContainer has to have masking enabled, negative padding would be masked
            Padding = new MarginPadding { Right = -Margin },
            Child = ScrollContainer = new DrumScrollContainer
            {
                RelativeSizeAxes = Axes.Both
            }
        });
    }
    protected override bool OnMouseDown(MouseDownEvent e) => true; // prevent closing
    public void UpdateSearchDisplay()
    {
        // This is expensive but who cares. Can fix this when we add virtualization
        ScrollContainer.Clear();  // we really shouldn't clear this when we are just changing our focus
        CommandPaletteButton targetButton = null;
        var y = 0f;
        for (var i = 0; i < FilteredCommands.Count; i++)
        {
            var c = FilteredCommands[i];
            var b = new CommandPaletteButton(c)
            {
                Y = y,
                Padding = new MarginPadding { Right = Margin },
                Action = () => { Hide(); CommandController.ActivateCommand(c); }
            };
            ScrollContainer.Add(b);
            if (Target == c) targetButton = b;
            y += CommandPaletteButton.Height;
        }
        if (y == 0)
        {
            ScrollContainer.Add(new CommandButtonBase
            {
                Padding = new MarginPadding { Right = Margin },
                Text = "No matching commands"
            });
            y += CommandButtonBase.Height;
        }
        ScrollPadder.Height = Math.Min(CommandPaletteButton.Height * CommandDisplayCount, y); // make sure to set height before trying to scroll
        if (targetButton != null)
        {
            targetButton.BackgroundColour = DrumColors.ActiveButton;
            ScrollContainer.ScrollIntoView(targetButton);
        }
    }
    IEnumerable<CommandInfo> OrderedCommands;
    public void UpdateSearch(bool force = false) => UpdateSearch(Input.Current.Value, force);
    public void UpdateSearch(string search, bool force = false)
    {
        // if this ever needs optimization, the filtering portion takes practically no time (< 0.1ms)
        //    the easiest optimization would be to not reset the filter if the new filter contains the old filter
        //    we could also compile the search into an Action and apply it to get better performance
        //    both of these optimization would be nothing though compared to virtualization
        // rendering the commands takes ~10ms in the worst case scenario
        // this could be brought down to <1ms with virtualization
        if (!force && !Visible) return;
        search ??= string.Empty;
        var s = search.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        FilteredCommands = new();
        var orderedCommands = OrderedCommands ?? CommandController.OrderedCommands;
        var defaultCommands = CommandController.DefaultCommandInfos;
        var targetFound = false;
        var i = 0;
        foreach (var c in orderedCommands)
        {
            if (c != null) // null => not registered
            {
                if (c.MatchesSearch(s) && (!RequireHandler || (defaultCommands[(int)c.Command].HasHandlers)))
                {
                    Target ??= c;
                    if (c == Target)
                    {
                        targetFound = true;
                        _targetI = i;
                    }
                    FilteredCommands.Add(c);
                    i += 1;
                }
            }
        }
        if (!targetFound) TargetI = 0;
        UpdateSearchDisplay();
    }
    protected override bool OnKeyDown(KeyDownEvent e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (TargetI.HasValue)
                {
                    TargetI += 1;
                    UpdateSearchDisplay();
                }
                return true;
            case Key.Up:
                if (TargetI.HasValue)
                {
                    TargetI -= 1;
                    UpdateSearchDisplay();
                }
                return true;
            case Key.PageDown:
                if (TargetI.HasValue)
                {
                    TargetI = Math.Min(FilteredCommands.Count - 1, TargetI.Value + (CommandDisplayCount - 1));
                    UpdateSearchDisplay();
                }
                return true;
            case Key.PageUp:
                if (TargetI.HasValue)
                {
                    TargetI = Math.Max(0, TargetI.Value - (CommandDisplayCount - 1));
                    UpdateSearchDisplay();
                }
                return true;
        }
        if (e.ControlPressed)
        {
            switch (e.Key)
            {
                case Key.Home:
                    if (TargetI.HasValue)
                    {
                        TargetI = 0;
                        UpdateSearchDisplay();
                    }
                    return true;
                case Key.End:
                    if (TargetI.HasValue)
                    {
                        TargetI = FilteredCommands.Count - 1;
                        UpdateSearchDisplay();
                    }
                    return true;
            }
        }
        return base.OnKeyDown(e);
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        CommandController.RegisterHandlers(this);
        UpdateSearch();
    }
    protected override void Dispose(bool isDisposing)
    {
        CommandController.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }

    public bool EditKeybind(CommandContext _)
    {
        if (Visible && FilteredCommands.Count > 0)
        {
            Util.Palette.EditKeybind(Target);
            return true;
        }
        return false;
    }

    public void ShowCommandList(bool requireHandler, Command[] commands = null)
    {
        RequireHandler = requireHandler;
        OrderedCommands = commands?.Select(e => CommandController[e]);
        if (Visible) UpdateSearch();
        else Show();
    }

    [CommandHandler] public void ShowAllCommands() => ShowCommandList(false);
    [CommandHandler] public void ShowAvailableCommands() => ShowCommandList(true);
    public bool Close(CommandContext _ = null)
    {
        if (!Visible) return false;
        Hide();
        return true;
    }

    public override void Hide()
    {
        if (!Visible) return;
        CommandController?.RemoveHandler(Command.EditKeybind, EditKeybind);
        CommandController?.RemoveHandler(Command.Close, Close);
        base.Hide();
    }

    public override void Show()
    {
        if (Visible) return;
        CommandController?.RegisterHandler(Command.EditKeybind, EditKeybind);
        CommandController?.RegisterHandler(Command.Close, Close);
        _targetI = null;
        Target = null;
        if (Input != null) Input.Current.Value = string.Empty;
        Schedule(() => GetContainingFocusManager().ChangeFocus(Input));
        UpdateSearch(true);
        base.Show();
    }

    class PaletteSearch : SearchTextBox
    {
        protected override bool OnKeyDown(KeyDownEvent e)
        {
            // if we don't do this, it will just trigger a commit and we won't be able to trigger EditKeybind
            // an alternative to this (which would still allow users to disable it) would be to just always return false here
            // this would cause Ctrl+Enter to go up to the command controller where it could trigger any proper binding
            if (e.ControlPressed && (e.Key == Key.KeypadEnter || e.Key == Key.Enter))
            {
                if (Util.CommandController.ActivateCommand(Command.EditKeybind)) return true;
            }
            return base.OnKeyDown(e);
        }
        public PaletteSearch() : base("Type to search commands") { }
    }
}
