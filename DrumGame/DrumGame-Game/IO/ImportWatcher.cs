using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DrumGame.Game.Utils;
using osu.Framework.Logging;
using osu.Framework.Threading;

namespace DrumGame.Game.IO;


// meant to handle the `import` folder, but also has static method for importing generic files
public class ImportWatcher : IDisposable
{
    FileSystemWatcher Watcher;

    bool hasPendingImports;


    struct ImportItem
    {
        public string Path;
        public DateTime Touched;
    }

    List<ImportItem> l_queue = new();

    ScheduledDelegate NextUpdate;

    public void Init()
    {
        var folder = Util.Resources.GetDirectory("import");
        Watcher = new FileSystemWatcher(folder.FullName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName, // not too confident about these
            EnableRaisingEvents = true,
        };

        void HandleWatcherEvent(FileSystemEventArgs e)
        {
            var path = e.FullPath;
            if (!File.Exists(path)) return; // prevent pulling directories
            var time = DateTime.Now;
            lock (l_queue)
            {
                var item = new ImportItem { Path = path, Touched = time };
                var found = false;
                for (var i = 0; i < l_queue.Count; i++)
                {
                    if (l_queue[i].Path == path)
                    {
                        l_queue[i] = item;
                        found = true;
                        break;
                    }
                }
                if (!found) l_queue.Add(item);
                ScheduleUpdate();
            }
        }

        Watcher.Renamed += (_, e) => HandleWatcherEvent(e);
        Watcher.Created += (_, e) => HandleWatcherEvent(e);
        Watcher.Changed += (_, e) => HandleWatcherEvent(e);

        CheckFolder();
    }
    public static bool IsFileReady(string filename)
    {
        try
        {
            using var inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch { return false; }
    }
    public async void Update() // I think this is good as async void
    {
        var time = DateTime.Now;
        var ready = new List<ImportItem>();
        lock (l_queue)
        {
            if (hasPendingImports) // can't process if we are still working on an import
            {
                ScheduleUpdate();
                return;
            }
            for (var i = l_queue.Count - 1; i >= 0; i--)
            {
                var target = l_queue[i];
                if (!File.Exists(target.Path))
                {
                    l_queue.RemoveAt(i);
                    continue;
                }
                if (IsFileReady(target.Path))
                {
                    l_queue.RemoveAt(i);
                    ready.Add(target);
                }
                else
                {
                    if (l_queue.Count > 5 && (time - target.Touched).TotalSeconds > 5)
                    {
                        Logger.Log($"Force dequeuing {target.Path}");
                        l_queue.RemoveAt(i);
                    }
                }
            }
            if (l_queue.Count > 0)
                ScheduleUpdate();
            if (ready.Count > 0)
                hasPendingImports = true;
        }
        if (ready.Count > 0)
        {
            try
            {
                foreach (var item in ready)
                    await TryImportAndMove(item.Path); // any async code here should just get thrown on the update thread
            }
            finally
            {
                lock (l_queue)
                {
                    hasPendingImports = false;
                }
            }
        }
    }
    public void ScheduleUpdate()
    {
        lock (l_queue)
        {
            NextUpdate.TryCancel();
            if (l_queue.Count == 0) return;
            NextUpdate = Util.UpdateThread.Scheduler.AddDelayed(Update, 100);
        }
    }
    public void CheckFolder()
    {
        var folder = Util.Resources.GetDirectory("import");
        var files = folder.GetFiles();
        if (files.Length == 0) return;
        lock (l_queue)
        {
            foreach (var file in files)
                l_queue.Add(new ImportItem { Path = file.FullName, Touched = file.LastWriteTime });
            if (l_queue.Count > 0)
                ScheduleUpdate();
        }
    }
    public void Dispose()
    {
        Watcher?.Dispose();
        Watcher = null;
    }

    static async Task<bool> TryImportAndMove(string path) // currently, this runs on update thread
    {
        try
        {
            var res = await FileImporters.OpenFile(path);
            if (!res) return false;
            var target = Path.Join(Util.Resources.GetDirectory("import/completed").FullName, Path.GetFileName(path));
            var i = 0;
            while (File.Exists(target))
            {
                var ext = Path.GetExtension(path);
                target = Path.Join(Util.Resources.GetDirectory("import/completed").FullName,
                    Path.GetFileNameWithoutExtension(path) + i + ext);
                i += 1;
            }
            if (File.Exists(target))
                File.Delete(path);
            else
                File.Move(path, target);
            return true;
        }
        catch (Exception e) { Logger.Error(e, $"Failed to import {path}"); }
        return false;
    }
}