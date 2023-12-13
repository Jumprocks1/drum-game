using System;
using DrumGame.Game.Utils;
using osu.Framework.Graphics.OpenGL;
using osu.Framework.Graphics.Primitives;
using osuTK;

namespace DrumGame.Game.Graphics3D.View;

// the transforms for this are actually applied to the models/scene, so it's all reversed
public class Camera
{
    private Vector3 position;
    public Vector3 Position
    {
        get => position; set
        {
            if (Position == value) return;
            position = value;
            validMatrix = false;
        }
    }

    float orthographicVertical = 3;
    public float OrthographicVertical
    {
        get => orthographicVertical; set
        {
            if (orthographicVertical == value) return;
            orthographicVertical = value;
            validProjection = false;
        }
    }
    bool orthographic;
    public bool Orthographic
    {
        get => orthographic; set
        {
            if (orthographic == value) return;
            orthographic = value;
            validProjection = false;
        }
    }

    private float xRotation;
    public float XRotation
    {
        get => xRotation; set
        {
            if (xRotation == value) return;
            xRotation = Math.Clamp(value, (float)(-Math.PI / 2), (float)(Math.PI / 2));
            validMatrix = false;
        }
    }
    private float yRotation;
    public float YRotation
    {
        get => yRotation; set
        {
            if (yRotation == value) return;
            yRotation = value;
            validMatrix = false;
        }
    }


    bool validMatrix = false;
    Matrix4 _matrix4;
    public Matrix4 Matrix4 => ValidateMatrix();

    bool validProjection = false;
    Matrix4 projection;
    public Matrix4 Projection { get => ValidateProjection(); }


    private float fov = 90;
    public float FOV
    {
        get => fov; set
        {
            if (fov == value) return;
            fov = Math.Clamp(value, 1, 179);
            validProjection = false;
        }
    }

    public Quad TargetQuad
    {
        set
        {
            var x = value.TopLeft.X;
            var y = value.TopLeft.Y;
            var w = value.TopRight.X - x;
            var h = value.BottomLeft.Y - y;
            if (this.x != x)
            {
                validProjection = false;
                this.x = x;
            }
            if (this.y != y)
            {
                validProjection = false;
                this.y = y;
            }
            if (width != w)
            {
                validProjection = false;
                width = w;
            }
            if (height != h)
            {
                validProjection = false;
                height = h;
            }
        }
    }

    private float width = 1;
    public float Width => width;
    private float height = 1;
    public float Height => height;
    private float x = 0;
    private float y = 0;

    public float AspectRatio => width / height;

    private Matrix4 ValidateMatrix()
    {
        if (!validMatrix || !validProjection)
        {
            _matrix4 = Matrix4.CreateTranslation(Position) *
                Matrix4.CreateRotationY(YRotation) *
                Matrix4.CreateRotationX(XRotation) *
                Projection;
            validMatrix = true;
        }
        return _matrix4;
    }
    private Matrix4 globalProjection = GLUtil.Renderer.ProjectionMatrix;
    public Matrix4 GlobalProjection
    {
        get => globalProjection; set
        {
            if (globalProjection == value) return;
            globalProjection = value;
            validProjection = false;
        }
    }
    private Matrix4 ValidateProjection()
    {
        if (!validProjection)
        {
            // couldn't figure out how to do this non-sketchy, oh well
            var screenMat = new Matrix4(
                width * GlobalProjection.M11 / 2, 0f, 0f, 0f,
                0f, -height * GlobalProjection.M22 / 2, 0f, 0f,
                0f, 0f, 1f, 0f,
                (width / 2 + x) * GlobalProjection.M11 - 1, (height / 2 + y) * GlobalProjection.M22 + 1, 0f, 1f
            );
            var p = orthographic ? Matrix4.CreateOrthographic(orthographicVertical * width / height, orthographicVertical, 0.1f, 100f)
                : projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(fov), width / height, 0.1f, 100f);
            projection = p * screenMat;
            validProjection = true;
        }
        return projection;
    }

    public (Vector3 Origin, Vector3 Dir) MouseRay(Vector2 mousePosition)
    {
        if (orthographic)
        {
            mousePosition = (mousePosition - new Vector2(x, y)) * new Vector2(2 / width, -2 / height) - new Vector2(1, -1);
            return (-Position
                + Left() * orthographicVertical / 2 * width / height * mousePosition.X
                + Up() * orthographicVertical / 2 * mousePosition.Y, Direction());
        }


        var fovy = MathHelper.DegreesToRadians(FOV);
        var scale = (float)Math.Tan(fovy / 2);

        mousePosition = (mousePosition - new Vector2(x, y)) * new Vector2(2 / width, -2 / height) - new Vector2(1, -1);

        var dir = new Vector3(scale * AspectRatio * mousePosition.X, scale * mousePosition.Y, -1);

        var quat = Quaternion.FromAxisAngle(Vector3.UnitY, -YRotation) *
            Quaternion.FromAxisAngle(Vector3.UnitX, -XRotation);
        return (-Position, dir.TransformQ(quat).Normalized());
    }
    public Vector3 Direction() => (-Vector3.UnitZ).TransformQ(Quaternion.FromAxisAngle(Vector3.UnitY, -YRotation) *
            Quaternion.FromAxisAngle(Vector3.UnitX, -XRotation)).Normalized();
    public Vector3 Up() => Vector3.UnitY.TransformQ(Quaternion.FromAxisAngle(Vector3.UnitY, -YRotation) *
            Quaternion.FromAxisAngle(Vector3.UnitX, -XRotation)).Normalized();
    public Vector3 Left() => Vector3.UnitX.TransformQ(Quaternion.FromAxisAngle(Vector3.UnitY, -YRotation)).Normalized();
}
