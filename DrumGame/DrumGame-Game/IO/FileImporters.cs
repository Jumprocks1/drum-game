using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DrumGame.Game.API;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Formats;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Browsers;
using DrumGame.Game.Commands;
using DrumGame.Game.Commands.Requests;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Development;
using osu.Framework.Logging;

namespace DrumGame.Game.IO;

public static class FileImporters
{
    public static MapImportContext NextContext;
    static MapStorage MapStorage => Util.DrumGame.MapStorage;

    public static string DirectDownloadUrl(string url)
    {
        var driveRegex = new Regex(@"^https://drive.google.com/file/d/([a-zA-Z0-9\-_]{33})");
        var match = driveRegex.Match(url);
        if (match.Success)
        {
            var driveId = match.Groups[1].Value;
            return $"https://drive.google.com/uc?id={driveId}&export=download";
        }
        return url;
    }

    public static void DownloadAndOpenFile(string url)
    {
        Logger.Log($"Downloading {url}", level: LogLevel.Important);
        var contextCapture = MapImportContext.Current;
        var tempPath = Path.GetFileName(url);
        if (!tempPath.EndsWith(".bjson"))
            tempPath = "temp-" + tempPath;
        tempPath = Util.Resources.GetTemp(tempPath);
        var task = new DownloadTask(url, tempPath);
        task.OnSuccess += __ =>
        {
            var path = task.OutputPath;
            Util.UpdateThread.Scheduler.Add(() =>
            {
                try
                {
                    using var context = contextCapture ?? new MapImportContext();
                    context.Url = url;
                    context.SetActive();
                    _ = OpenFile(path);
                }
                finally
                {
                    try
                    {
                        if (!DebugUtils.IsDebugBuild) File.Delete(path);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, $"Failed to delete {path}");
                    }
                }
            });
        };
        task.Enqueue();
    }

    // returns true if the import was succesfully STARTED
    // does not indicate if an import has completed
    public static async Task<bool> OpenFile(string path)
    {
        using var n = NextContext;
        n?.SetActive();
        using var context = new MapImportContext(path);
        Logger.Log($"Openning {path}");
        try
        {
            var fileName = Path.GetFileName(path);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".osz")
                OszBeatmapLoader.ImportOsz(MapStorage, path);
            else if (ext == ".osu")
                OszBeatmapLoader.ImportOsu(MapStorage, path);
            else if (ext == ".bjson") // we should also make a FileProvider .bjson importer
                ImportBJson(path);
            else if (ext == ".dtx") await DtxLoader.ImportDtx(path);
            else if (fileName.Equals("song.ini", StringComparison.InvariantCultureIgnoreCase)) SongIniLoader.ImportSongIni(path);
            else if (fileName.ToLowerInvariant() == "set.def") await DtxLoader.ImportDef(path);
            else if (ext == ".zip")
            {
                using var provider = new ZipFileProvider(path);
                await OpenFileProvider(provider);
            }
            else if (ext == ".7z" || ext == ".rar")
            {
                var provider = SevenZip.Open(path); // probably should dispose this
                await OpenFileProvider(provider);
            }
            else if (DebugUtils.IsDebugBuild && Directory.Exists(path)) // allow folder open in Debug mode
            {
                await OpenFileProvider(new FileProvider(path)
                {
                    EnumerationOptions = new EnumerationOptions { MaxRecursionDepth = 3, RecurseSubdirectories = true }
                });
            }
            else
                return false;
            if (context.NewMaps.Count > 0)
                BeatmapCarousel.Current?.JumpToNewMap(context.NewMaps[^1]);
            else
            {
                return false;
            }
        }
        catch (Exception e)
        {
            Util.Palette.ShowMessage($"Failed while opening {path}, see log for exception");
            Logger.Error(e, $"Failed while opening {path}");
        }
        if (n == NextContext) NextContext = null; // successfully consumed context
        return true;
    }

    public static bool DeleteIfTemp(string path)
    {
        var folder = new DirectoryInfo(Path.GetDirectoryName(path)).Name;
        if (folder == "temp")
        {
            File.Delete(path);
            return true;
        }
        return false;
    }

    public static void ImportBJson(string fullPath)
    {
        var fileName = Path.GetFileName(fullPath);
        var outputTarget = Path.Join(MapStorage.AbsolutePath, fileName);
        if (outputTarget != fullPath)
        {
            var provider = new FileProvider(Path.GetDirectoryName(fullPath));
            var map = ImportBJsonInProvider(fileName, provider);
            if (map != null)
                YouTubeDL.TryFixAudio(map, _ => Util.ActivateCommandUpdateThread(Command.Refresh));
            DeleteIfTemp(fullPath);
        }
    }

    static Beatmap ImportBJsonInProvider(string fullName, IFileProvider provider)
    {
        // don't need try catch here since it's handled by callers
        var name = Path.GetFileName(fullName);
        var outputTarget = Path.Join(MapStorage.AbsolutePath, name);
        var map = BJsonFormat.Instance.Load(provider.Open(fullName), null, fullName, false, false);
        map.Source = new BJsonSource(outputTarget, BJsonFormat.Instance);
        void TryCopy(string zipPath, string localFolder, Action<string> set)
        {
            if (zipPath != null)
            {
                zipPath = Path.Join(Path.GetDirectoryName(fullName), zipPath);
                zipPath = zipPath.Replace('\\', '/');
                var newValue = localFolder + "/" + Path.GetFileName(zipPath);
                set(newValue);
                if (provider.Exists(zipPath))
                {
                    var assetPath = map.FullAssetPath(newValue);
                    if (!File.Exists(assetPath))
                    {
                        try
                        {
                            provider.Copy(zipPath, assetPath);
                            Logger.Log($"copied to {assetPath}");
                        }
                        catch (Exception e) { Logger.Error(e, $"failed to copy {localFolder} {zipPath}"); }
                    }
                }
                else Logger.Log($"{zipPath} not found");
            }
        }
        TryCopy(map.Audio, "audio", e => map.Audio = e);
        TryCopy(map.Image, "images", e => map.Image = e);
        map.SaveToDisk(MapStorage);
        Logger.Log($"imported {fullName} to {outputTarget}");
        return map;
    }

    public static async Task<bool> OpenFileProvider(IFileProvider provider)
    {
        var context = MapImportContext.Current;
        foreach (var fullName in provider.List())
        {
            var name = Path.GetFileName(fullName);
            var entryExt = Path.GetExtension(fullName);
            if (entryExt == ".bjson")
            {
                ImportBJsonInProvider(fullName, provider);
            }
            else if (name.Equals("set.def", StringComparison.OrdinalIgnoreCase))
            {
                await DtxLoader.ImportDef(new SubFileProvider(provider, Path.GetDirectoryName(fullName)), name);
            }
            else if (name.Equals("song.ini", StringComparison.OrdinalIgnoreCase))
            {
                SongIniLoader.ImportSongIni(new SubFileProvider(provider, Path.GetDirectoryName(fullName)), name);
            }
        }
        if (!context.MapsFound) // if we failed, we can run a second pass
        {
            foreach (var path in provider.List())
            {
                var ext = Path.GetExtension(path);
                if (ext == ".dtx")
                    await DtxLoader.ImportDtx(new SubFileProvider(provider, Path.GetDirectoryName(path)), Path.GetFileName(path));
            }
        }
        return context.MapsFound;
    }
}