using System;
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

namespace DrumGame.Game.Views.Settings;

[Cached]
public class SettingsView : ModalBase, IModal, IHandleSettingInfo
{
    [Resolved] DrumGameConfigManager Config { get; set; }
    [Resolved] FrameworkConfigManager FrameworkConfig { get; set; }

    public Action CloseAction { get; set; }

    DrumScrollContainer ScrollContainer;

    [BackgroundDependencyLoader]
    private void load()
    {
        RelativeSizeAxes = Axes.Both;
        AddInternal(new ModalBackground(() => CloseAction?.Invoke()));
        var inner = new ModalForeground(Axes.None)
        {
            Width = 800,
            Height = 0.9f,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.Y
        };
        var headerSize = 90;
        var y = 5f;
        inner.Add(new SpriteText
        {
            Text = "Settings",
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Font = FrameworkFont.Regular.With(size: 40),
            Y = y
        });
        var x = -10f;
        const float spacing = 6;
        inner.Add(new CommandIconButton(Command.EditKeybinds, FontAwesome.Regular.Keyboard, 40)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Y = y,
            X = x
        });
        x -= 40 + spacing;
        inner.Add(new CommandIconButton(Command.RevealInFileExplorer, FontAwesome.Solid.FolderOpen, 32)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = x,
            Y = 4 + y
        });
        x -= 32 + spacing;
        inner.Add(new CommandIconButton(Command.OpenExternally, FontAwesome.Solid.FileAlt, 24)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = x,
            Y = 8 + y
        });
        x -= 24 + spacing;
        // inner.Add(SearchBox = new SearchTextBox
        // {
        //     RelativeSizeAxes = Axes.X,
        //     Height = 40,
        //     Y = 50
        // });

        inner.Add(new Container
        {
            Child = ScrollContainer = new DrumScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
            },
            Padding = new MarginPadding { Top = headerSize },
            RelativeSizeAxes = Axes.Both,
        });

        SettingsList.RenderSettings(this, Config, FrameworkConfig);

        AddInternal(inner);
        Util.CommandController.RegisterHandlers(this);
    }
    bool Even = true; // even fields have lighter background
    int NextDepth;
    float NextY;
    public void AddSetting(SettingInfo setting)
    {
        var control = new SettingControl(setting, Even)
        {
            Y = NextY,
            Depth = NextDepth++
        };
        ScrollContainer.Add(control);
        NextY += control.Height;
        Even = !Even;
    }

    protected override void Dispose(bool isDisposing)
    {
        Util.CommandController.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }

    [CommandHandler] public void RevealInFileExplorer() => Config.RevealInFileExplorer();
    [CommandHandler] public void OpenExternally() => Config.OpenExternally();

    // public void Focus(InputManager _) => SearchBox.TakeFocus();
}
