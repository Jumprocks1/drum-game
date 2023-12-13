using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;

namespace DrumGame.Game.IO;

public class ZipFileProvider : IFileProvider, IDisposable
{
    public readonly ZipArchive Zip;
    public string ZipFileLocation;
    public ZipFileProvider(ZipArchive zip, string originalName)
    {
        Zip = zip;
        FolderName = originalName;
    }
    public ZipFileProvider(string zipFile)
    {
        ZipFileLocation = zipFile;
        Zip = ZipFile.OpenRead(zipFile);

        // It's possible to fix the filename encoding issues with this
        // System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        // Zip = new ZipArchive(File.OpenRead(zipFile), ZipArchiveMode.Read, false, System.Text.Encoding.GetEncoding(932));
        // Zip.ExtractToDirectory(Path.Join(Path.GetDirectoryName(zipFile), "test"));

        FolderName = Path.GetFileNameWithoutExtension(zipFile);
    }

    public string FolderName { get; private set; }

    string BuildPath(string localPath) => localPath ?? string.Empty;

    public bool Copy(string path, string absolutePath)
    {
        var entry = Zip.GetEntryCaseless(BuildPath(path));
        if (entry == null) return false;
        try
        {
            entry.ExtractToFile(absolutePath, false);
            return true;
        }
        catch { return false; }
    }
    public void Dispose() => Zip?.Dispose();

    public bool Exists(string path) => Zip.GetEntryCaseless(BuildPath(path)) != null;
    public Stream Open(string path) => Zip.GetEntryCaseless(BuildPath(path)).Open();
    public Stream OpenCreate(string path)
    {
        if (Zip.Mode == ZipArchiveMode.Read) throw new NotSupportedException("Writing to readonly ZipArchive not supported.");
        else
        {
            throw new NotImplementedException();
        }
    }

    public IEnumerable<string> List(string path = null)
    {
        if (path == null) return Zip.Entries.Select(e => e.FullName);
        if (path != null && !path.EndsWith("/")) path += "/";
        var basePath = BuildPath(path);
        return Zip.Entries.Where(e => e.FullName.StartsWith(basePath, StringComparison.InvariantCultureIgnoreCase))
            .Select(e => e.FullName.Substring(basePath.Length));
    }
    public DateTime? CreationTimeUtc(string path) => ZipFileLocation == null ? null : File.GetCreationTimeUtc(ZipFileLocation);
}