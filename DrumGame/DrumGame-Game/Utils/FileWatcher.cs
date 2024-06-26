using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using osu.Framework.Logging;

namespace DrumGame.Game.Utils;

public class FileWatcher<T> : FileWatcher
{
    public event Action<T> JsonChanged;

    public FileWatcher(string directory, string path) : base(directory, path)
    {
        ContentsChanged += contents =>
        {
            JsonChanged?.Invoke(JsonConvert.DeserializeObject<T>(contents));
        };
    }
    public FileWatcher(string path) : this(Path.GetDirectoryName(path), path) { }

    public static T Load(string path) => JsonConvert.DeserializeObject<T>(File.ReadAllText(path));

    public override void Dispose()
    {
        JsonChanged = null;
        base.Dispose();
    }
}

public class FileWatcher : IDisposable
{
    public double DebounceMs = 50; // cause a forced delay
    FileSystemWatcher watcher;
    public event Action<string> ContentsChanged;
    // we have 2 events so we don't have to read the file if not needed
    public event Action Changed;

    public string TargetPath;
    public string Directory;

    public static FileWatcher<T> FromPath<T>(string path) => new(Path.GetDirectoryName(path), path);
    public FileWatcher(string path) : this(Path.GetDirectoryName(path), path) { }
    public FileWatcher(string directory, string path) // make sure to call Register() to start it
    {
        TargetPath = path;
        Directory = directory;
    }

    public List<string> ExtraFilters;
    void SetFilters()
    {
        watcher.Filters.Clear();
        if (TargetPath != null)
            watcher.Filters.Add(Path.GetFileName(TargetPath));
        if (ExtraFilters != null)
            foreach (var f in ExtraFilters) watcher.Filters.Add(f);
    }
    public void Register()
    {
        watcher = new(Directory)
        {
            NotifyFilter = NotifyFilters.LastWrite
        };
        SetFilters();
        var reloadId = 0;
        watcher.Changed += (_, args) =>
        {
            var myId = ++reloadId;
            void checkRead()
            {
                if (myId != reloadId) return;
                if (!Util.checkFileReady(args.FullPath))
                {
                    Util.UpdateThread.Scheduler.AddDelayed(checkRead, 10);
                    return;
                }
                handleReady(args.FullPath);
            }
            Util.UpdateThread.Scheduler.AddDelayed(checkRead, DebounceMs);
        };
        watcher.EnableRaisingEvents = true;
    }

    public void UpdatePath(string path) => UpdatePath(Path.GetDirectoryName(path), path);
    public void UpdatePath(string directory, string path)
    {
        Directory = directory;
        TargetPath = path;
        watcher.Path = Directory;
        SetFilters();
    }

    public void ForceTrigger() => handleReady(TargetPath);

    protected virtual void handleReady(string path)
    {
        try
        {
            Changed?.Invoke();
            ContentsChanged?.Invoke(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Failed to load {path}");
            var name = Path.GetFileName(path);
            Util.Palette.ShowMessage($"Failed to load {name}, see log for details");
        }
    }

    public virtual void Dispose()
    {
        ContentsChanged = null;
        watcher?.Dispose();
        watcher = null;
    }
}