using System.Collections.Generic;
using System.Reflection;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Skinning;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.StateChanges.Events;
using osu.Framework.Input.States;
using osu.Framework.Platform;
using osuTK.Input;


namespace DrumGame.Game.Utils;

public class DrumInputManager : UserInputManager
{
    public GameHost GameHost => Host;
    public override bool HandleHoverEvents => !hiddenChanged && base.HandleHoverEvents;
    // public event Action<MousePositionChangeEvent> OnMousePositionChange;
    protected override void HandleMousePositionChange(MousePositionChangeEvent e)
    {
        // can add global mouse move event handlers here
        // this triggers even if outside the window
        if (hiddenChanged)
        {
            Host.Window.CursorState &= ~CursorState.Hidden;
            hiddenChanged = false;
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
    private bool hiddenChanged = false;
    public bool AllowHideMouse
    {
        set
        {
            allowHideMouse = value;
            if (hiddenChanged)
            {
                Host.Window.CursorState &= ~CursorState.Hidden;
                hiddenChanged = false;
            }
        }
    }

    public void HideMouse()
    {
        if (allowHideMouse && (Host.Window.CursorState & CursorState.Hidden) == 0)
        {
            Host.Window.CursorState |= CursorState.Hidden;
            hiddenChanged = true;
        }
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
            if (Button == MouseButton.Left || Button == MouseButton.Right)
            {
                if (targets != null && EnableClick && DraggedDrawable?.DragBlocksClick != true)
                {
                    foreach (var e in targets)
                    {
                        if (e.IsHovered)
                        {
                            if (Button == MouseButton.Right && e is IHasContextMenu)
                            {
                                // make sure the click gets dumped into the ContextMenuContainer
                                targets.Clear();
                                targets.Add(Util.ContextMenuContainer);
                                break;
                            }
                            // this condition should match HandleButtonUp
                            if ((state.Keyboard.ControlPressed && e is IHasUrl)
                                || (e is IHasCommandInfo command && !command.DisableClick && command.CommandInfo != null))
                            {
                                // basically just MouseDown => true
                                // prevents mouse down from triggering other drawables
                                if (state.Mouse.IsPositionValid)
                                    MouseDownPosition = state.Mouse.Position;
                                return e;
                            }
                        }
                    }
                }
            }
            return base.HandleButtonDown(state, targets);
        }

        protected override void HandleButtonUp(InputState state, List<Drawable> targets)
        {
            if (Button == MouseButton.Left || Button == MouseButton.Right)
            {
                if (targets != null && EnableClick && DraggedDrawable?.DragBlocksClick != true)
                {
                    foreach (var e in targets)
                    {
                        if (e.IsHovered)
                        {
                            if (state.Keyboard.ControlPressed && e is IHasUrl url) { Util.InputManager.GameHost?.OpenUrlExternally(url.Url); break; }
                            if (e is IHasCommandInfo command && !command.DisableClick && command.CommandInfo != null)
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
                                targets = null; // prevent click from propagating
                                break;
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
