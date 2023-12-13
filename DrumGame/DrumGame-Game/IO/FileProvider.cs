using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DrumGame.Game.Interfaces;

namespace DrumGame.Game.IO;

public class FileProvider : IFileProvider
{
    public readonly string Folder;
    public EnumerationOptions EnumerationOptions;
    public FileProvider(DirectoryInfo folder) : this(folder.FullName) { }
    public FileProvider(string folder)
    {
        Folder = folder;
    }
    public string FolderName => Path.GetFileName(Folder);

    public bool Copy(string path, string absolutePath)
    {
        try
        {
            File.Copy(Path.Join(Folder, path), absolutePath, false);
            return true;
        }
        catch { return false; }
    }

    public string BuildPath(string path) => Path.Join(Folder, path);

    public DateTime? CreationTimeUtc(string path) => File.GetCreationTimeUtc(BuildPath(path));

    public bool Exists(string path) => File.Exists(BuildPath(path));

    public IEnumerable<string> List(string path = null)
    {
        path = BuildPath(path);
        if (EnumerationOptions == null)
            return Directory.GetFiles(path).Select(e => Path.GetRelativePath(path, e));
        else
            return Directory.GetFiles(path, "*", EnumerationOptions).Select(e => Path.GetRelativePath(path, e));
    }

    public Stream Open(string path) => File.OpenRead(BuildPath(path));
    public Stream OpenCreate(string path) => File.Open(BuildPath(path), FileMode.Create);
}