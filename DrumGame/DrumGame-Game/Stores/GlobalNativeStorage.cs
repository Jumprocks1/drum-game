
using System;
using System.Collections.Generic;
using System.IO;
using osu.Framework.Platform;

namespace DrumGame.Game.Stores;

// NativeStorage without any restrictions on path locations
// Not a security concern since File.Open works globally anyways
public class GlobalNativeStorage : NativeStorage
{
    public GlobalNativeStorage(GameHost host = null) : base("", host)
    {
    }
    public override string GetFullPath(string path, bool createIfNotExisting = false)
    {
        var resolvedPath = Path.GetFullPath(path);
        if (createIfNotExisting) Directory.CreateDirectory(Path.GetDirectoryName(resolvedPath));
        return resolvedPath;
    }

    // these don't make sense in a global context
    public override IEnumerable<string> GetDirectories(string path) => throw new NotImplementedException();
    public override IEnumerable<string> GetFiles(string path, string pattern = "*") => throw new NotImplementedException();
}