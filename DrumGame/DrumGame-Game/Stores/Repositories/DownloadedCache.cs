using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using osu.Framework.Logging;

namespace DrumGame.Game.Stores.Repositories;

public static class DownloadedCache
{
    public static string FilePath => Util.Resources.GetAbsolutePath(Path.Join("repositories", "downloaded.txt"));
    static HashSet<string> Downloaded;

    // not used currently, but if we make it so you can remove songs from download list, we need to be able to resave the whole list
    // static bool Dirty = false;
    static object _lock = new();
    static List<string> PendingAdd; // only use within _lock

    static bool requiresResave;

    public static void Save()
    {
        lock (_lock)
        {
            if (requiresResave)
            {
                requiresResave = false;
                // this is a little sucky since HashSet could break the ordering
                File.WriteAllText(FilePath, string.Join('\n', Downloaded) + '\n');
            }
            else
            {
                if (PendingAdd == null || PendingAdd.Count == 0) return;
                File.AppendAllLines(FilePath, PendingAdd);
            }
            PendingAdd?.Clear();
        }
    }
    static string Clean(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        var strip = new string[] { "?usp=sharing", "?usp=drive_link", "?usp=share_link" };
        foreach (var s in strip)
        {
            if (url.EndsWith(s))
                url = url[..^s.Length];
        }
        return url;
    }
    public static bool Contains(string key) => Load().Contains(Clean(key));
    public static bool Contains(JsonRepositoryBeatmap map) => Contains(map.DownloadIdentifier);
    public static void Add(JsonRepositoryBeatmap map) => Add(map.DownloadIdentifier);
    public static void Remove(JsonRepositoryBeatmap map) => Remove(map.DownloadIdentifier);
    public static void Add(string key)
    {
        if (key == null) return;
        key = Clean(key);
        lock (_lock)
        {
            var hashSet = Load();
            if (hashSet.Contains(key)) return;
            PendingAdd ??= new();
            PendingAdd.Add(key);
            hashSet.Add(key);
        }
    }
    public static void Remove(string key)
    {
        if (key == null) return;
        key = Clean(key);
        lock (_lock)
        {
            var hashSet = Load();
            if (hashSet.Remove(key))
                requiresResave = true;
        }
    }
    static HashSet<string> Load()
    {
        if (Downloaded == null)
        {
            var path = FilePath;
            if (File.Exists(path))
                Downloaded = new HashSet<string>(File.ReadAllLines(path).Select(Clean));
            else
                Downloaded = new();
        }
        return Downloaded;
    }
}
