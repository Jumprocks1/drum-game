using System;
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
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Views.Settings;

[Cached]
public class SettingsView : ModalBase, IModal
{
    [Resolved] DrumGameConfigManager Config { get; set; }
    [Resolved] FrameworkConfigManager FrameworkConfig { get; set; }

    IModal Modal;
    void CloseModal()
    {
        if (Modal == null) return;
        RemoveInternal((Drawable)Modal, true);
        Modal = null;
    }
    // Very similar to CommandPaletteContainer.AddModal
    public void AddModal<T>() where T : IModal, new()
    {
        CloseModal();
        Modal = new T();
        Modal.CloseAction = CloseModal;
        var drawable = (Drawable)Modal;
        drawable.RelativeSizeAxes = Axes.Both;
        AddInternal(drawable);
    }

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

        y = 0f;
        var even = true;
        var settings = SettingsList.GetSettings(Config, FrameworkConfig);
        var depth = 0;
        foreach (var setting in settings)
        {
            var control = new SettingControl(setting, even)
            {
                Y = y,
                Depth = depth++
            };
            ScrollContainer.Add(control);
            y += control.Height;
            even = !even;
        }
        AddInternal(inner);
        Util.CommandController.RegisterHandlers(this);
    }

    protected override void Dispose(bool isDisposing)
    {
        Util.CommandController.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }

    [CommandHandler]
    public bool Close(CommandContext _)
    {
        if (Modal != null)
        {
            CloseModal();
            return true;
        }
        return false;
    }

    [CommandHandler] public void RevealInFileExplorer() => Config.RevealInFileExplorer();
    [CommandHandler] public void OpenExternally() => Config.OpenExternally();

    // public void Focus(InputManager _) => SearchBox.TakeFocus();
}
