using System;
using glTFLoader.Schema;
using osu.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osuTK;
using osuTK.Graphics.ES30;
using static glTFLoader.Schema.Accessor;
using SDL;
using _SDL2 = SDL2.SDL;
using _SDL3 = SDL.SDL3;

namespace DrumGame.Game.Utils;

public static class GLUtil
{
    public enum GLAttribPosition
    {
        Position = 0,
        Normal = 1,
        TexCoord = 2,
        Joints = 3,
        Weights = 4
    }
    public static GLAttribPosition AttribPosition(string attribute) => attribute switch
    {
        "POSITION" => GLAttribPosition.Position,
        "NORMAL" => GLAttribPosition.Normal,
        "TEXCOORD_0" => GLAttribPosition.TexCoord,
        "JOINTS_0" => GLAttribPosition.Joints,
        "WEIGHTS_0" => GLAttribPosition.Weights,
        _ => throw new NotSupportedException()
    };
    public static DrawElementsType ElementsType(this ComponentTypeEnum e) => e switch
    {
        ComponentTypeEnum.UNSIGNED_BYTE => DrawElementsType.UnsignedByte,
        ComponentTypeEnum.UNSIGNED_SHORT => DrawElementsType.UnsignedShort,
        ComponentTypeEnum.UNSIGNED_INT => DrawElementsType.UnsignedInt,
        _ => throw new ArgumentException()
    };
    public static int Size(this Accessor.TypeEnum typeEnum) => typeEnum switch
    {
        Accessor.TypeEnum.SCALAR => 1,
        Accessor.TypeEnum.VEC2 => 2,
        Accessor.TypeEnum.VEC3 => 3,
        Accessor.TypeEnum.VEC4 => 4,
        _ => throw new ArgumentException()
    };
    public static int Size(this ComponentTypeEnum typeEnum) => typeEnum switch
    {
        ComponentTypeEnum.UNSIGNED_BYTE => 1,
        ComponentTypeEnum.BYTE => 1,
        ComponentTypeEnum.UNSIGNED_SHORT => 2,
        ComponentTypeEnum.SHORT => 2,
        ComponentTypeEnum.UNSIGNED_INT => 4,
        ComponentTypeEnum.FLOAT => 4,
        _ => throw new ArgumentException()
    };

    public const int Multisample = 2;

    public static void StartSDLMultisample()
    {
        // if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows && Multisample > 0)
        if (FrameworkEnvironment.UseSDL3)
        {
            _ = _SDL3.SDL_InitSubSystem(SDL_InitFlags.SDL_INIT_VIDEO);
            // this doesn't actually do anything if we're running D3D11
            _ = _SDL3.SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_MULTISAMPLEBUFFERS, 1);
            _ = _SDL3.SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_MULTISAMPLESAMPLES, Multisample);
        }
        else
        {
            _ = _SDL2.SDL_InitSubSystem(_SDL2.SDL_INIT_VIDEO);
            // this doesn't actually do anything if we're running D3D11
            // https://github.com/libsdl-org/SDL/issues/8398
            _ = _SDL2.SDL_GL_SetAttribute(_SDL2.SDL_GLattr.SDL_GL_MULTISAMPLEBUFFERS, 1);
            _ = _SDL2.SDL_GL_SetAttribute(_SDL2.SDL_GLattr.SDL_GL_MULTISAMPLESAMPLES, Multisample);
        }
    }
    public static void StopSDLMultisample()
    {
        // if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows && Multisample > 0)
        if (FrameworkEnvironment.UseSDL3)
        {
            _SDL3.SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_VIDEO);
        }
        else
        {
            _SDL2.SDL_QuitSubSystem(_SDL2.SDL_INIT_VIDEO);
        }
    }

    public static Vector3 Vector3(this Colour4 colour) => new Vector3(colour.R, colour.G, colour.B);


    // the original OpenTK implementation for this seems to not work
    public static Vector3 TransformQ(this in Vector3 vec, in Quaternion quat)
    {
        var xyz = quat.Xyz;
        var w = quat.W;
        return 2 * osuTK.Vector3.Dot(xyz, vec) * xyz
            + (w * w - osuTK.Vector3.Dot(xyz, xyz)) * vec
            + 2 * w * osuTK.Vector3.Cross(xyz, vec);
    }
    public static int ProgramId(this osu.Framework.Graphics.Shaders.IShader shader) => (int)(shader.GetType().GetField("programID",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(shader));
    public static IRenderer Renderer => Util.Host.Renderer;
}

