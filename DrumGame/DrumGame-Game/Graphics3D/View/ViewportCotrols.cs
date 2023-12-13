using System;
using System.Collections.Generic;
using DrumGame.Game.Commands;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;
using MouseState = osu.Framework.Input.States.MouseState;

namespace DrumGame.Game.Graphics3D.View;

public class ViewportControlContext
{
    public (Vector3 Origin, Vector3 Dir) MouseRay;
    public InputManager InputManager;
    public Vector2 RawDelta;
    public Viewport3D Viewport;


    public MouseState MouseState => InputManager.CurrentState.Mouse;
    public Vector2 MousePosition => InputManager.CurrentState.Mouse.Position;
    public Camera Camera => Viewport.Camera;
}
public interface IViewportControl
{
    void Update(ViewportControlContext context);
    bool OnKeyPress(Key key) => false;
}
public partial class Viewport3D
{
    public List<IViewportControl> Controls = new();
    public const float Sensitivity = 0.005f;
    public const double Speed = 4;
    InputManager InputManager; // assigned in LoadComplete
    bool cameraControl = true;
    Vector2? LastMousePosition = null;
    public Vector3 FocusTarget;
    void UpdateCameraControls()
    {
        if (cameraControl)
        {
            if (InputManager != null)
            {
                var state = InputManager.CurrentState.Keyboard;
                var dt = Time.Elapsed;
                var vec = Vector3.Zero;
                if (state.Keys.IsPressed(Key.W)) vec += Vector3.UnitZ;
                if (state.Keys.IsPressed(Key.A)) vec += Vector3.UnitX;
                if (state.Keys.IsPressed(Key.S)) vec -= Vector3.UnitZ;
                if (state.Keys.IsPressed(Key.D)) vec -= Vector3.UnitX;
                if (vec != Vector3.Zero)
                {
                    vec *= Matrix3.CreateRotationY(-Camera.YRotation);
                }
                if (state.Keys.IsPressed(Key.Space)) vec -= Vector3.UnitY;
                if (state.ShiftPressed) vec += Vector3.UnitY;
                if (state.ControlPressed) vec *= 2.5f;
                if (vec != Vector3.Zero) Camera.Position += vec * (float)(dt * Speed / 1000);
            }
        }
    }
    void UpdateMouseControl()
    {
        if (Controls.Count > 0)
        {
            var position = InputManager.CurrentState.Mouse.Position;
            var context = new ViewportControlContext
            {
                MouseRay = Camera.MouseRay(position),
                InputManager = InputManager,
                RawDelta = position - (LastMousePosition ?? position),
                Viewport = this
            };
            LastMousePosition = position;

            foreach (var control in Controls) control.Update(context);
        }
    }

    [CommandHandler] public void ControlCamera() => cameraControl = !cameraControl;
    protected override bool Handle(UIEvent e)
    {
        if (cameraControl)
        {
            switch (e)
            {
                case KeyDownEvent kd:
                    if (cameraControl)
                    {
                        // don't want to trigger commands such as Ctrl+W
                        if (kd.Key == Key.W || kd.Key == Key.A || kd.Key == Key.S || kd.Key == Key.D
                            || kd.Key == Key.Space)
                            return true;
                        if (kd.Key == Key.Keypad1 || kd.Key == Key.Keypad3 || kd.Key == Key.Keypad7) // front view
                        {
                            var target = kd.Key switch
                            {
                                Key.Keypad3 => ((float)(-Math.PI / 2), 0f),
                                Key.Keypad7 => (0f, (float)(Math.PI / 2)),
                                _ => (0f, 0f)
                            };
                            if (Camera.YRotation == target.Item1 && Camera.XRotation == target.Item2)
                            {
                                if (target.Item2 != 0)
                                {
                                    target.Item2 -= (float)Math.PI;
                                }
                                else
                                {
                                    target.Item1 += (float)Math.PI;
                                }
                            }
                            Camera.YRotation = target.Item1;
                            Camera.XRotation = target.Item2;
                            Camera.Position = Camera.Direction() * (Camera.Position + FocusTarget).Length - FocusTarget;
                            return true;
                        }
                        if (kd.Key == Key.Keypad5)
                        {
                            Camera.Orthographic = !Camera.Orthographic;
                            return true;
                        }
                    }
                    break;
                case MouseMoveEvent mv:
                    if (e.CurrentState.Mouse.Buttons.IsPressed(MouseButton.Right))
                    {
                        var delta = mv.Delta;
                        // this kinda works but ehh pretty bad
                        // var pos = Handler.ScreenSpaceDrawQuad.Centre;
                        // Console.WriteLine(pos - mv.ScreenSpaceMousePosition);
                        // delta -= pos - mv.ScreenSpaceMousePosition;
                        // var point = Handler.Window.PointToScreen(new System.Drawing.Point((int)(pos.X + 0.5), (int)(pos.Y + 0.5)));
                        // osuTK.Input.Mouse.SetPosition(point.X, point.Y);
                        Camera.XRotation += delta.Y * Sensitivity;
                        Camera.YRotation += delta.X * Sensitivity;
                        return true;
                    }
                    break;
            }
        }
        return base.Handle(e);
    }
}

