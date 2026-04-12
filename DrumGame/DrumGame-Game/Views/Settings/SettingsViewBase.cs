using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Modals;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;

namespace DrumGame.Game.Views.Settings;

public abstract class SettingsViewBase : CompositeDrawable, IHandleSettingInfo, IAcceptFocus
{
    protected DrumScrollContainer ScrollContainer;
    protected SearchTextBox SearchBox;
    protected ModalForeground Inner;

    // currently should just be sprite text + settings controls
    // could make an interface for it, but wasn't needed yet
    public List<Drawable> SettingsElements = new();

    public SettingsViewBase()
    {
        RelativeSizeAxes = Axes.Both;
        Util.CommandController.RegisterHandlers(this);
    }

    public abstract string Title { get; }
    protected abstract void RenderSettings();

    [BackgroundDependencyLoader]
    private void load()
    {
        Inner = new ModalForeground(Axes.None)
        {
            Width = 800,
            Height = 0.9f,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.Y
        };
        var y = 5f;
        Inner.Add(new SpriteText
        {
            Text = Title,
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Font = FrameworkFont.Regular.With(size: 40),
            Y = y
        });
        y += 45f;
        Inner.Add(SearchBox = new SearchTextBox
        {
            RelativeSizeAxes = Axes.X,
            Height = 35,
            Y = y
        });
        SearchBox.OnCommit += (_, __) => SingleControl?.Action?.Invoke();

        var headerSize = 85;
        Inner.Add(new Container
        {
            Child = ScrollContainer = new DrumScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
            },
            Padding = new MarginPadding { Top = headerSize, Bottom = FooterSize },
            RelativeSizeAxes = Axes.Both,
        });

        RenderSettings();
        if (SearchBox != null)
            SearchBox.Current.BindValueChanged(_ => UpdateDisplay(), true);
        else
            UpdateDisplay();

        AddInternal(Inner);
    }

    public virtual float FooterSize => 0f;

    public string[] CompileSearch()
    {
        if (SearchBox == null) return null;
        var s = SearchBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return s;
    }

    SettingControl SingleControl; // if search results in a single settings control, this gets set
    public void UpdateDisplay()
    {
        SingleControl = null;
        var search = CompileSearch();
        var even = true; // even fields have lighter background
        float nextY = 0;
        SettingsBlockHeader pendingBlockHeader = null; // gets skipped if there's no elements under it
        void checkHeader() // hides block header if there's no elements inside of it
        {
            if (pendingBlockHeader != null)
            {
                pendingBlockHeader.Alpha = 1;
                if (nextY != 0)
                    nextY += CommandPalette.Margin; // add extra space between header and previous section
                pendingBlockHeader.Y = nextY;
                even = true;
                nextY += pendingBlockHeader.Height;
                pendingBlockHeader = null;
            }
        }

        var visibleCount = 0;

        foreach (var drawable in SettingsElements)
        {
            if (drawable is SettingControl control)
            {
                var visible = control.MatchesSearch(search);
                if (visible)
                {
                    visibleCount += 1;
                    SingleControl = visibleCount == 1 ? control : null;
                    checkHeader();
                    // have to update after checkHeader, since that sets `even`
                    control.UpdateDisplay(even, visible);
                    control.Y = nextY;
                    even = !even;
                    nextY += drawable.Height;
                }
                else control.UpdateDisplay(even, visible);
            }
            else if (drawable is SettingsBlockHeader blockHeader)
            {
                if (pendingBlockHeader != null)
                    pendingBlockHeader.Alpha = 0; // 2 headers in a row => hide last one
                pendingBlockHeader = blockHeader;
            }
        }
        if (pendingBlockHeader != null)
            pendingBlockHeader.Alpha = 0;
    }

    int NextDepth;
    public void AddSetting(SettingInfo setting)
    {
        var control = new SettingControl(setting)
        {
            Depth = NextDepth++,
            BlockHeader = LastBlockHeader
        };
        ScrollContainer.Add(control);
        SettingsElements.Add(control);
    }
    public SettingsBlockHeader LastBlockHeader;
    public void AddBlockHeader(string text)
    {
        var blockHeaderFontSize = 30;
        ScrollContainer.Add(LastBlockHeader = new SettingsBlockHeader
        {
            Text = text,
            Height = blockHeaderFontSize + CommandPalette.Margin / 2,
            Font = FrameworkFont.Regular.With(size: blockHeaderFontSize),
            X = CommandPalette.Margin,
        });
        SettingsElements.Add(LastBlockHeader);
    }

    protected override void Dispose(bool isDisposing)
    {
        Util.CommandController.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }

    public void Focus(IFocusManager focusManager) => focusManager.ChangeFocus(SearchBox);
}
