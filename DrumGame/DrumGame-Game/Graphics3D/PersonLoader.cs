using DrumGame.Game.Stores;
using osuTK.Input;

namespace DrumGame.Game.Graphics3D;

public class PersonLoader : glTFScene
{
    public const string FILE = @"../../3d/person.glb";
    public PersonLoader(FileSystemResources resources) : base(resources.GetAbsolutePath(FILE))
    {
    }
}

