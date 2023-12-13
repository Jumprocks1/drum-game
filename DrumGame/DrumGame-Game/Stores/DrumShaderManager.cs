using DrumGame.Game.Utils;
using osu.Framework.Graphics.Shaders;
using osu.Framework.IO.Stores;

namespace DrumGame.Game.Stores;

public class DrumShaderManager : ShaderManager
{
    public DrumShaderManager(IResourceStore<byte[]> store) : base(GLUtil.Renderer, store)
    {
    }
}
