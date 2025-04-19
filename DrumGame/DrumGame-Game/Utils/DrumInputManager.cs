using System.Collections.Generic;
using System.Reflection;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Skinning;
using osu.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Input.StateChanges.Events;
using osu.Framework.Input.States;
using osu.Framework.Platform;
using osuTK.Input;
using SDL;
using _SDL2 = SDL2.SDL;
using _SDL3 = SDL.SDL3;


namespace DrumGame.Game.Utils;

public class DrumInputManager : UserInputManager
{
    public GameHost GameHost => Host;
    public override bool HandleHoverEvents => !hiddenOverride && base.HandleHoverEvents;
    // public event Action<MousePositionChangeEvent> OnMousePositionChange;
    protected override void HandleMousePositionChange(MousePositionChangeEvent e)
    {
        // can add global mouse move event handlers here
        // this triggers even if outside the window
        if (hiddenOverride)
        {
            Host.Window.CursorState &= ~CursorState.Hidden;
            hiddenOverride = false;
        }
        base.HandleMousePositionChange(e);
    }

    protected override void HandleKeyboardKeyStateChange(ButtonStateChangeEvent<Key> keyboardKeyStateChange)
    {
        SkinManager.SetAltKey(keyboardKeyStateChange.State.Keyboard.AltPressed);
        base.HandleKeyboardKeyStateChange(keyboardKeyStateChange);
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        if (Child is PlatformActionContainer plat)
        {
            if (plat.Child is KeyBindingContainer<FrameworkAction> kb)
            {
                kb.OnLoadComplete += e =>
                {
                    var bindings = typeof(KeyBindingContainer)
                        .GetField("KeyBindings", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(kb) as IEnumerable<IKeyBinding>;
                    foreach (var binding in bindings)
                    {
                        var action = binding.Action as FrameworkAction?;
                        // unbind actions
                        // eventually we could unbind all actions
                        if (action == FrameworkAction.ToggleFullscreen ||
                            action == FrameworkAction.CycleFrameSync ||
                            action == FrameworkAction.CycleFrameStatistics ||
                            action == FrameworkAction.ToggleLogOverlay ||
                            action == FrameworkAction.CycleExecutionMode)
                            binding.KeyCombination = new KeyCombination(InputKey.None);
                    }
                };
            }
        }
    }

    private bool allowHideMouse = true;
    private bool hiddenOverride = false;
    public bool AllowHideMouse
    {
        set
        {
            allowHideMouse = value;
            if (hiddenOverride)
            {
                Host.Window.CursorState &= ~CursorState.Hidden;
                hiddenOverride = false;
            }
        }
    }

    public bool MouseForceHidden => hiddenOverride;

    public void HideMouse()
    {
        if (allowHideMouse && (Host.Window.CursorState & CursorState.Hidden) == 0)
        {
            Host.Window.CursorState |= CursorState.Hidden;
            hiddenOverride = true;
        }
    }

    nint[] _cursors;
    nint[] Cursors => _cursors ??= new nint[(int)_SDL2.SDL_SystemCursor.SDL_NUM_SYSTEM_CURSORS];
    _SDL2.SDL_SystemCursor CurrentCursor;
    void ResetCursor() => SetCursor(_SDL2.SDL_SystemCursor.SDL_SYSTEM_CURSOR_ARROW);
    void SetCursor(_SDL2.SDL_SystemCursor cursor)
    {
        if (CurrentCursor == cursor) return;
        var id = Cursors[(int)cursor];
        CurrentCursor = cursor;
        if (FrameworkEnvironment.UseSDL3)
        {
            unsafe
            {
                if (id == 0)
                    Cursors[(int)cursor] = id = (nint)_SDL3.SDL_CreateSystemCursor((SDL_SystemCursor)cursor);
                _SDL3.SDL_SetCursor((SDL_Cursor*)id);
            }
        }
        else
        {
            if (id == 0)
                Cursors[(int)cursor] = id = _SDL2.SDL_CreateSystemCursor(cursor);
            _SDL2.SDL_SetCursor(id);
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        if (Cursors != null)
        {
            for (var i = 0; i < Cursors.Length; i++)
            {
                if (Cursors[i] != 0)
                    if (FrameworkEnvironment.UseSDL3)
                        unsafe
                        {
                            _SDL3.SDL_DestroyCursor((SDL_Cursor*)Cursors[i]);
                        }
                    else
                        _SDL2.SDL_FreeCursor(Cursors[i]);
            }
        }
        base.Dispose(isDisposing);
    }

    void CheckCursor()
    {
        var hovered = HoveredDrawables;
        for (var i = 0; i < hovered.Count; i++)
        {
            if (hovered[i] is IHasCursor hasCursor)
            {
                if (hasCursor.Cursor is _SDL2.SDL_SystemCursor cursor)
                {
                    SetCursor(cursor);
                    return;
                }
            }
        }
        ResetCursor();
    }

    protected override void Update()
    {
        base.Update(); // this updates HoveredDrawables
        CheckCursor();
    }

    protected override MouseButtonEventManager CreateButtonEventManagerFor(MouseButton button)
    {

        switch (button)
        {
            case MouseButton.Left:
            case MouseButton.Middle:
            case MouseButton.Right:
                return new NoDragDistanceMouseButtonEventManager(button);

            default:
                return base.CreateButtonEventManagerFor(button);
        }
    }
    class NoDragDistanceMouseButtonEventManager : MouseButtonEventManager
    {
        public NoDragDistanceMouseButtonEventManager(MouseButton button) : base(button)
        {
            ChangeFocusOnClick = button == MouseButton.Left;
        }

        protected override Drawable HandleButtonDown(InputState state, List<Drawable> targets)
        {
            if (state.Mouse.IsPositionValid)
                MouseDownPosition = state.Mouse.Position;
            var ev = new MouseDownEvent(state, Button, MouseDownPosition);
            if (targets != null && EnableClick && DraggedDrawable?.DragBlocksClick != true)
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    var e = targets[i];
                    if (e.IsHovered)
                    {
                        if (Button == MouseButton.Right && e is IHasContextMenu)
                        {
                            if (Util.ContextMenuContainer.TriggerEvent(ev))
                            {
                                // all targets after i will not trigger MouseUp events
                                // since we insert at i, the current target (e) will be skipped
                                targets.Insert(i, Util.ContextMenuContainer);
                                return Util.ContextMenuContainer;
                            }
                        }
                        // this condition should match HandleButtonUp
                        if (((state.Keyboard.ControlPressed && e is IHasUrl)
                            || (e is IHasCommandInfo command && !command.DisableClick && command.CommandInfo != null))
                            && (Button == MouseButton.Left || Button == MouseButton.Right))
                        {
                            return e;
                        }
                    }
                    if (e.TriggerEvent(ev))
                        return e;
                }
            }

            // Since we don't call base, double clicks will never work. Don't think I care
            // return base.HandleButtonDown(state, targets);
            return null;
        }

        protected override void HandleButtonUp(InputState state, List<Drawable> targets)
        {
            if (Button == MouseButton.Left || Button == MouseButton.Right)
            {
                if (targets != null && EnableClick && DraggedDrawable?.DragBlocksClick != true)
                {
                    // MouseUp events cannot return `true` normally
                    // This is because `targets` only contains drawables that already handled the down event
                    // Because of this, we never want to `break` or `return` out of this loop
                    // we do still remove from `targets` to block regular click events
                    for (var i = targets.Count - 1; i >= 0; i--)
                    {
                        var e = targets[i];
                        if (e.IsHovered)
                        {
                            if (state.Keyboard.ControlPressed && e is IHasUrl url)
                            {
                                Util.InputManager.GameHost?.OpenUrlExternally(url.Url);
                                targets.RemoveAt(i);
                            }
                            else if (e is IHasCommandInfo command && !command.DisableClick && command.CommandInfo != null)
                            {
                                if (state.Keyboard.ControlPressed || Button == MouseButton.Right)
                                    Util.Palette.EditKeybind(command.CommandInfo);
                                else
                                {
                                    Util.InputManager.Schedule(() =>
                                    {
                                        Util.CommandController.ActivateCommand(command.CommandInfo);
                                        if (command is Components.CommandButton cb)
                                            cb.AfterActivate?.Invoke();
                                    });
                                }
                                targets.RemoveAt(i);
                            }
                        }
                    }
                }
            }
            base.HandleButtonUp(state, targets);
        }

        public override bool EnableDrag => true;

        public override bool EnableClick => true;

        public override bool ChangeFocusOnClick { get; }
        public override float ClickDragDistance => -1;
    }
}
