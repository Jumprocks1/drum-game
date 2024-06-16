using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Browsers;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Overlays;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Midi;
using DrumGame.Game.Modals;
using DrumGame.Game.Stores;
using DrumGame.Game.Stores.Repositories;
using DrumGame.Game.Utils;
using DrumGame.Game.Views;
using DrumGame.Game.Views.Settings;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Logging;

namespace DrumGame.Game.Commands;

public class CommandPaletteContainer : Container
{
    // this prevents things below us from getting focus if we have an overlay
    public override bool RequestsFocus => HasOverlay;
    // could make an overlay list with interface
    // don't want to pass down input if we have a modal open
    bool HasOverlay => Palette.Visible || ModalStack.Count > 0;
    public override bool HandlePositionalInput => HasOverlay;
    public List<IModal> ModalStack = new();
    public CommandPalette Palette;
    IFocusManager focusManager;
    public CommandController CommandController;
    public RequestModal Request(RequestConfig config) => Push(new RequestModal(config), unique: false);
    public RequestModal RequestNumber(string title, string label, double value, Action<double> callback, string description = null)
        => RequestNullableNumber(title, label, value, e => { if (e != null) callback(e.Value); }, description);
    public RequestModal RequestNullableNumber(string title, string label, double? value, Action<double?> callback, string description = null)
        => Request(new RequestConfig
        {
            Title = title,
            Description = description,
            Field = new StringFieldConfig(label, value.ToString())
            {
                OnCommit = value => callback(double.TryParse(value, out var o) ? o : null)
            }
        });
    public RequestModal RequestString(string title, string label, string value, Action<string> callback, string description = null) =>
        Request(new RequestConfig
        {
            Title = title,
            Description = description,
            Field = new StringFieldConfig(label, value) { OnCommit = callback }
        });
    public FileRequest RequestFile(string title, string description, Action<string> callback) =>
        Push(new FileRequest(title, description, e => { if (e != null) callback(e); }));
    public CommandPaletteContainer()
    {
        Depth = -10;
        RelativeSizeAxes = Axes.Both;
        AddInternal(Palette = new CommandPalette
        {
            Depth = -1 // need to set depth for palette since it is always inside the container
        });
    }
    [BackgroundDependencyLoader]
    private void load(CommandController command)
    {
        CommandController = command;
        command.Palette = this;
        command.RegisterHandlers(this);
    }
    protected override void Dispose(bool isDisposing)
    {
        CommandController.RemoveHandlers(this);
        CommandController.Palette = null;
        _notificationOverlayOverlay?.Dispose();
        base.Dispose(isDisposing);
    }

    protected override void LoadComplete()
    {
        focusManager = GetContainingFocusManager();
        base.LoadComplete();
    }

    protected override bool OnKeyDown(KeyDownEvent e) => (HasOverlay && CommandController.HandleEvent(e)) || base.OnKeyDown(e);
    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (HasOverlay)
        {
            CloseSingle();
            return true;
        }
        return base.OnMouseDown(e);
    }
    protected override bool Handle(UIEvent e)
    {
        if (base.Handle(e)) return true;
        if (HasOverlay)
        {
            switch (e)
            {
                case ScrollEvent:
                case MouseEvent:
                case KeyboardEvent:
                    return true;
            }
        }
        return false;
    }

    bool closeBound = false;
    public void UpdateCloseBinding()
    {
        if (HasOverlay && !closeBound)
        {
            CommandController.RegisterHandler(Command.Close, CloseSingle);
            closeBound = true;
        }
        if (!HasOverlay && closeBound)
        {
            CommandController.RemoveHandler(Command.Close, CloseSingle);
            closeBound = false;
        }
    }

    public T Toggle<T>(Func<T> modal) where T : Drawable, IModal
    {
        var closed = false;
        foreach (var e in ModalStack.OfType<T>().ToList())
        {
            e.CloseAction();
            closed = true;
        }
        if (closed) return null;
        return Push(modal());
    }
    public T Toggle<T>() where T : Drawable, IModal, new() => Toggle(() => new T());
    public T Toggle<T>(T modal) where T : Drawable, IModal
    {
        var closed = false;
        foreach (var e in ModalStack.OfType<T>().ToList())
        {
            e.CloseAction();
            closed = true;
        }
        if (closed) return null;
        return Push(modal);
    }

    public bool ModalOpen<T>() => ModalStack.OfType<T>().Any();

    // note that if this is used with basic `RequestModal`s, they will kill eachother with unique
    // this is typically desirable
    public T Push<T>(T modal, bool keepAliveOnClose = false, bool unique = true) where T : Drawable, IModal
    {
        if (unique)
        {
            foreach (var e in ModalStack.OfType<T>().ToList())
                e.CloseAction();
        }
        modal.RelativeSizeAxes = Axes.Both;
        modal.CloseAction = () =>
        {
            if (!ModalStack.Remove(modal)) return; // already closed
            RemoveInternal(modal, !keepAliveOnClose);
            if (keepAliveOnClose) modal.Alpha = 0;

            UpdateCloseBinding();
        };
        ModalStack.Add(modal);
        UpdateCloseBinding();
        if (keepAliveOnClose) modal.Alpha = 1;
        AddInternal(modal);
        if (modal is IAcceptFocus f)
            ScheduleAfterChildren(() => f.Focus(focusManager));
        else
            ScheduleAfterChildren(() => focusManager.TriggerFocusContention(this));
        // we could add another interface called IConflictWith which just takes a Drawable (or object) and returns true or false
        // we would run all modals on the stack through this function and close the conflicts
        return modal;
    }

    // This always pushes a new `T` instead of just resurfacing an old `T`
    public T PushNew<T>(bool unique = true) where T : Drawable, IModal, new() => Push(new T(), unique: unique);
    public T GetModal<T>() where T : Drawable, IModal => ModalStack.OfType<T>().LastOrDefault();

    public T Push<T>(bool unique = true) where T : Drawable, IModal, new()
    {
        T modal = null;
        if (unique)
        {
            modal = GetModal<T>();
            if (modal != null)
            {
                // we do this to bring it back to the front when we add it later
                ModalStack.Remove(modal);
                RemoveInternal(modal, false);
            }
        }
        return Push(modal ?? new());
    }
    [CommandHandler] public void EditKeybinds() => Push<KeybindEditor>();
    [CommandHandler] public void OpenSettings() => Push<SettingsView>();
    [CommandHandler] public void OpenSkinSettings() => Push<OverlayModal<SkinSettingsView>>();
    [CommandHandler] public void Notifications() => Push(NotificationOverlayOverlay, true);
    [CommandHandler] public void OpenKeyboardView() => Push<OverlayModal<KeyBindingBrowser>>();
    [CommandHandler] public void OpenKeyboardDrumEditor() => Push<OverlayModal<KeyboardMappingEditor>>();
    [CommandHandler] public void ViewDrumLegend() => Push<OverlayModal<Notation.DrumLegend>>();
    [CommandHandler] public void ViewMidi() => Push<OverlayModal<MidiView>>();
    [CommandHandler] public void ViewRepositories() => Push<RepositoryViewer>();
    [CommandHandler] public void ConfigureMapLibraries() => Toggle<MapLibraryView>();


    OverlayModal<NotificationOverlay> _notificationOverlayOverlay;
    OverlayModal<NotificationOverlay> NotificationOverlayOverlay => _notificationOverlayOverlay ??= new();
    public NotificationOverlay NotificationOverlay => NotificationOverlayOverlay.Child;

    public void Close<T>() where T : IModal
    {
        for (var i = ModalStack.Count - 1; i >= 0; i--)
        {
            if (ModalStack[i] is T)
                ModalStack[i].CloseAction();
        }
        UpdateCloseBinding();
    }
    public void CloseSingle()
    {
        if (Palette.Visible)
            Palette.Hide();
        else if (ModalStack.Count > 0)
            ModalStack[^1].CloseAction();
        UpdateCloseBinding();
    }

    public void ClosePalette() { Palette.Hide(); UpdateCloseBinding(); }

    public void CloseAll()
    {
        Palette.Hide();
        while (ModalStack.Count > 0) // we want to make sure CloseAction handles the ModalStack popping
            ModalStack[^1].CloseAction();
        UpdateCloseBinding();
    }

    public void ScheduleAction(Action action, bool forceScheduled = false) => Scheduler.Add(action, forceScheduled);
    public void ShowMessage(string message, LogLevel logLevel = LogLevel.Important, MessagePosition position = MessagePosition.Top)
    {
        Logger.Log(message, level: logLevel);
        Scheduler.Add(() =>
        {
            var textOverlay = new TextOverlay(message, position);
            Add(textOverlay);
            textOverlay.Touch();
            textOverlay.OnDisappear = () => Remove(textOverlay, true);
        }, false);
    }
    public void UserError(string message) => ShowMessage(message, logLevel: LogLevel.Error);
    public void UserError(string message, Exception exception)
    {
        Logger.Error(exception, message);
        ShowMessage(message + ", see log for details", logLevel: LogLevel.Error);
    }
    public void UserError(Exception exception)
    {
        Logger.Error(exception, "User error");
        UserError(exception.Message);
    }

    public void EditKeybind(CommandInfo commandInfo, int index)
    {
        ClosePalette();
        var keybindModal = new KeybindModal(commandInfo, index);
        Push(keybindModal);
        keybindModal.Close = e =>
        {
            keybindModal.CloseAction();
            if (e.NewBindRequested)
                EditKeybind(commandInfo, -1);
        };
    }
    public void EditKeybind(CommandInfo commandInfo) => EditKeybind(commandInfo, commandInfo.Bindings.Count - 1);
}
