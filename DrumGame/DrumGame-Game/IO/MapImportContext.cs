using System;
using System.Collections.Generic;

namespace DrumGame.Game.API;

// I hate this class. Context's like this in C# are awful.
// Currently, this class is confusing and annoying with async code
public class MapImportContext : IDisposable
{
    [ThreadStatic]
    public static MapImportContext Current;

    // make sure all of these fields are copied in the constructor
    public string Url;
    public string Path;
    public string Author;
    public readonly List<string> NewMaps;

    public bool MapsFound => NewMaps.Count > 0;

    public MapImportContext(bool setActive = true)
    {
        if (Current != null)
        {
            Path = Current.Path;
            Url = Current.Url;
            NewMaps = Current.NewMaps;
            Author = Current.Author;
        }
        else
        {
            NewMaps = new();
        }
        if (setActive) SetActive();
    }

    public void SetActive()
    {
        if (Current == this) return;
        Previous = Current;
        Current = this;
    }

    public MapImportContext(string path, string url = null) : this()
    {
        Url = url;
        Path = path;
    }

    public MapImportContext Previous;
    public void Dispose()
    {
        Current = Previous;
        Previous = null;
    }
}