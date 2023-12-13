using System;
using DrumGame.Game.Graphics3D.Shapes;
using DrumGame.Game.Graphics3D.View;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osuTK;
using osuTK.Graphics.ES30;
using osuTK.Input;

namespace DrumGame.Game.Graphics3D;

public class Gizmo : ShapeModel3D, IViewportControl
{
    public enum GizmoMode
    {
        None,
        AxisX,
        AxisY,
        AxisZ,
        AxisAll
    }
    static readonly Vector3 DraggingColour = Colour4.CornflowerBlue.Vector3();
    static readonly Vector3 HoverColour = Colour4.DarkRed.Vector3();
    static readonly Vector3 BaseColour = Colour4.Gray.Vector3();
    Vector3 _position;
    public (Vector2 Mouse, Vector3 Position) DragStart; // Position when we started dragging, used for locked translations
    GizmoMode Mode;
    const float Radius = 0.05f;
    public Gizmo(Vector3 position) : base(new Sphere(Radius, 2).Flat())
    {
        _position = position;
        UpdateModelMatrix();
        Colour = Colour4.DarkRed.Vector3();
    }

    void UpdateModelMatrix()
    {
        ModelMatrix = Matrix4.CreateTranslation(_position);
    }

    public Action<Vector3> PositionChanged;

    public Vector3 Position
    {
        get => _position; set
        {
            if (_position == value) return;
            _position = value;
            UpdateModelMatrix();
            PositionChanged?.Invoke(_position);
        }
    }

    public bool OnKeyPress(Key key)
    {
        if (Mode != GizmoMode.None)
        {
            if (key == Key.X)
            {
                Mode = Mode == GizmoMode.AxisX ? GizmoMode.AxisAll : GizmoMode.AxisX;
                return true;
            }
            else if (key == Key.Y)
            {
                Mode = Mode == GizmoMode.AxisY ? GizmoMode.AxisAll : GizmoMode.AxisY;
                return true;
            }
            else if (key == Key.Z)
            {
                Mode = Mode == GizmoMode.AxisZ ? GizmoMode.AxisAll : GizmoMode.AxisZ;
                return true;
            }
        }
        return false;
    }

    public void Update(ViewportControlContext context)
    {
        var pressed = context.MouseState.IsPressed(MouseButton.Left);
        if (!pressed) Mode = GizmoMode.None;
        var highlight = false;
        var ray = context.MouseRay;
        var diff = (_position - ray.Origin); // offset from camera to gizmo
        if (Mode != GizmoMode.None)
        {
            var delta = context.MousePosition - DragStart.Mouse;
            var axisRestriction = Mode switch
            {
                GizmoMode.AxisX => Vector3.UnitX,
                GizmoMode.AxisY => Vector3.UnitY,
                GizmoMode.AxisZ => Vector3.UnitZ,
                _ => Vector3.One,
            };
            if (delta != Vector2.Zero)
            {
                var camera = context.Camera;
                var scaledDelta = delta * new Vector2(2 / camera.Height, -2 / camera.Height);
                var cameraDirection = camera.Direction();
                var left = camera.Left();
                var up = camera.Up();
                var fovy = MathHelper.DegreesToRadians(camera.FOV);
                var scale = camera.Orthographic ? camera.OrthographicVertical / 2 :
                     (float)Math.Tan(fovy / 2) * Vector3.Dot(diff, cameraDirection);
                var change = (scaledDelta.X * left + scaledDelta.Y * up) * scale;
                Position = DragStart.Position + change * axisRestriction;
            }
        }
        else
        {
            var dot = Vector3.Dot(ray.Dir, diff);
            var discrim = dot * dot - (diff.LengthSquared - Radius * Radius);
            // if discrim is < 0, there's no chance of collision, front or behind
            // if center is in front of us, dot will be positive
            if (discrim >= 0 && dot > 0)
            {
                // we only care about the front face of the sphere (the one with the smaller length to camera)
                // if we hit the front face, we also hit the back face
                // but if we only hit the backface, this means the camera is inside the sphere
                // var frontPosition = dot * dot - discrim;
                if (dot * dot > discrim)
                {
                    highlight = true;
                    if (pressed)
                    {
                        Mode = GizmoMode.AxisAll;
                        DragStart = (context.MousePosition, _position);
                    }
                }
            }
        }
        Colour = Mode != GizmoMode.None ? DraggingColour :
            highlight ? HoverColour : BaseColour;
    }

    public override void Draw(RenderContext context)
    {
        GL.Clear(ClearBufferMask.DepthBufferBit);
        base.Draw(context);
    }
}

