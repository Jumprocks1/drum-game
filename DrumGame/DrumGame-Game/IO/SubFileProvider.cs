using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;

namespace DrumGame.Game.IO;

public class SubFileProvider : IFileProvider
{
    public readonly IFileProvider Parent;
    public readonly string SubPath;
    public SubFileProvider(IFileProvider parent, string subPath)
    {
        Parent = parent;
        // `\` doesn't work with zip usually
        SubPath = subPath.Replace("\\", "/");
    }

    public string BuildPath(string path)
    {
        if (string.IsNullOrEmpty(SubPath)) return path;
        return SubPath + "/" + path;
    }

    public string FolderName => string.IsNullOrEmpty(SubPath) ? Parent.FolderName : Path.GetFileName(SubPath);
    public bool Copy(string path, string absolutePath) => Parent.Copy(BuildPath(path), absolutePath);
    public bool Exists(string path) => Parent.Exists(BuildPath(path));
    public IEnumerable<string> List(string path = null) => Parent.List(BuildPath(path));
    public Stream Open(string path) => Parent.Open(BuildPath(path));
    public Stream OpenCreate(string path) => Parent.OpenCreate(BuildPath(path));
    public DateTime? CreationTimeUtc(string path) => Parent.CreationTimeUtc(BuildPath(path));
}