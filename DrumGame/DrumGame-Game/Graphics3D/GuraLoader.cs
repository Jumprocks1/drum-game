using DrumGame.Game.Stores;
using osuTK.Input;

namespace DrumGame.Game.Graphics3D;

public class GuraLoader : glTFScene
{
    public const string FILE = @"../../3d/gura.glb";
    public GuraLoader(FileSystemResources resources) : base(resources.GetAbsolutePath(FILE))
    {
    }


    public void OnKeyDown(Key key)
    {
    }
}

