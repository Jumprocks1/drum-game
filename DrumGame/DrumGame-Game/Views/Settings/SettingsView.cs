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
    [Resolved] CommandController Command { get; set; }
    // SearchTextBox SearchBox;
    protected override bool OnMouseDown(MouseDownEvent e)
    {
        return CloseControls() || base.OnMouseDown(e);
    }

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

    bool CloseControls()
    {
        var closed = false;
        foreach (var control in ScrollContainer.OfType<SettingControl>())
        {
            if (control.Info.Open)
            {
                control.Close();
                closed = true;
            }
        }
        return closed;
    }
    public Action CloseAction { get; set; }

    DrumScrollContainer ScrollContainer;

    [BackgroundDependencyLoader]
    private void load()
    {
        RelativeSizeAxes = Axes.Both;
        AddInternal(new ModalBackground(() => CloseAction?.Invoke()));
        var inner = new ClickableContainer(() => CloseControls())
        {
            RelativeSizeAxes = Axes.Both,
            Width = 0.8f,
            Height = 0.9f,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre
        };
        inner.Add(new Box
        {
            Colour = DrumColors.DarkBackground,
            RelativeSizeAxes = Axes.Both
        });
        var headerSize = 90;
        inner.Add(new SpriteText
        {
            Text = "Settings",
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Font = FrameworkFont.Regular.With(size: 40),
            Y = 5
        });
        inner.Add(new CommandIconButton(Commands.Command.EditKeybinds, FontAwesome.Regular.Keyboard, 40)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Y = 5,
            X = -10
        });
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
        var y = 0f;
        var even = true;
        var settings = SettingsList.GetSettings(Config, FrameworkConfig);
        var depth = 0;
        foreach (var setting in settings)
        {
            SettingControl control = null;
            control = new SettingControl(setting, even)
            {
                Y = y,
                Action = () =>
                {
                    CloseControls();
                    control.Info.OnClick(control);
                },
                Depth = depth++
            };
            ScrollContainer.Add(control);
            y += control.Height;
            even = !even;
        }
        AddInternal(inner);
        Command.RegisterHandlers(this);
    }

    protected override void Dispose(bool isDisposing)
    {
        Command.RemoveHandlers(this);
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
        return CloseControls();
    }

    [CommandHandler] public void RevealInFileExplorer() => Config.RevealInFileExplorer();
    [CommandHandler] public void OpenExternally() => Config.OpenExternally();

    // public void Focus(InputManager _) => SearchBox.TakeFocus();

    class ClickableContainer : Container
    {
        readonly Action action;
        public ClickableContainer(Action action) { this.action = action; }
        protected override bool OnMouseDown(MouseDownEvent e) { action(); return true; }
    }
}
