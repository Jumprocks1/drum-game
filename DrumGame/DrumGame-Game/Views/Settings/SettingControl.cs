using System;
using System.Linq;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;

namespace DrumGame.Game.Views.Settings;

public class SettingControl : BasicButton, IHasCommand, IHasCursor
{
    public SDL2.SDL.SDL_SystemCursor? Cursor
        => Info.GetType().GetMethod(nameof(SettingInfo.OnClick)).DeclaringType != typeof(SettingInfo) ?
            SDL2.SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_HAND : null;
    public DrumScrollContainer ScrollContainer => Parent.Parent as DrumScrollContainer;

    string IHasMarkupTooltip.MarkupTooltip
    {
        get
        {
            var tooltip = Info.Tooltip ?? Info.Description;
            if (tooltip != null && Command != Command.None)
            {
                if (tooltip.Contains('\n'))
                    return $"{tooltip}\n{IHasCommand.GetMarkupTooltip(Command)}";
                else
                    return $"{tooltip} - {IHasCommand.GetMarkupTooltip(Command)}";
            }
            return tooltip ?? IHasCommand.GetMarkupTooltip(Command);
        }
    }

    public IAcceptFocus FocusTarget;
    public void Focus()
    {
        var target = FocusTarget ?? Children.OfType<IAcceptFocus>().FirstOrDefault();
        target?.Focus(GetContainingFocusManager());
    }
    public void Clicked()
    {
        if (Command != Command.None)
            Util.CommandController.ActivateCommand(Command);
        else
            Focus();
    }

    public Command Command { get; set; }

    protected override SpriteText CreateText()
    {
        return new SpriteText
        {
            Font = FrameworkFont.Regular.With(size: 24),
            X = SideMargin,
            Y = 3,
        };
    }
    protected override void Dispose(bool isDisposing)
    {
        Info.Dispose();
        base.Dispose(isDisposing);
    }
    public const float SideMargin = 20;
    public const float InputWidth = 300;
    public SettingInfo Info;
    public SettingControl(SettingInfo info)
    {
        Info = info;
        Height = info.Height;
        RelativeSizeAxes = Axes.X;
        Text = info.Label;
        info.Render(this);
        info.AfterRender?.Invoke(this);
        Action = () => Info.OnClick(this);
    }

    string _filterString;
    public string FilterString => _filterString ??= MakeFilterString();
    string MakeFilterString() => $"{Info.Label} {Info.Tags} {BlockHeader?.FilterString}";
    public SettingsBlockHeader BlockHeader;

    public bool MatchesSearch(string[] search)
    {
        if (search == null || search.Length == 0) return true;
        return search.All(e => FilterString.Contains(e, StringComparison.OrdinalIgnoreCase));
    }

    // this should be called shortly after adding the control to it's parent
    public void UpdateDisplay(bool even)
    {
        BackgroundColour = even ? DrumColors.RowHighlight : DrumColors.RowHighlightSecondary;
    }
    public void UpdateDisplay(bool even, bool visible)
    {
        if (visible)
        {
            Alpha = 1;
            UpdateDisplay(even);
        }
        else
        {
            Alpha = 0;
        }
    }

    public void AddCommandIconButton(Command command, IconUsage icon)
    {
        Add(new CommandIconButton(command, icon, Height)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = -SideMargin - InputWidth - 5
        });
    }
    public void AddIconButton(Action action, IconUsage icon, string tooltip)
    {
        Add(new IconButton(action, icon, Height)
        {
            MarkupTooltip = tooltip,
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = -SideMargin - InputWidth - 5
        });
    }
}
