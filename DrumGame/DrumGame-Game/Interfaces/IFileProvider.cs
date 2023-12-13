using System;
using System.Collections.Generic;
using System.IO;

namespace DrumGame.Game.Interfaces;

public interface IFileProvider
{
    public string FolderName { get; }
    /// Make sure to dispose any files created with this
    public Stream Open(string path);
    public Stream OpenCreate(string path);
    public bool Copy(string path, string absolutePath); // false if copy fails
    public bool Exists(string path);
    public DateTime? CreationTimeUtc(string path);
    public IEnumerable<string> List(string path = null);
    public Stream TryOpen(params string[] paths)
    {
        foreach (var path in paths)
        {
            if (Exists(path)) return Open(path);
        }
        return null;
    }
}