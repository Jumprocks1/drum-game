using System;
using System.Collections.Generic;
using System.IO;
using osu.Framework.Logging;

namespace DrumGame.Game.Stores;

public static class MapStorageExtensions
{
    public static void Clean(this MapStorage storage)
    {
        var usedFiles = new HashSet<string>();
        var audioPath = "audio";
        foreach (var map in storage.GetMaps())
        {
            var ser = storage.GetMetadata(map);
            var audio = ser.Audio;
            if (audio == null)
            {
                Logger.Log($"{map} missing audio", level: LogLevel.Important);
            }
            else
            {
                if (!storage.Exists(audio))
                    Logger.Log($"{audio} not found", level: LogLevel.Important);
                if (!audio.StartsWith(audioPath) && audio != "metronome")
                    Logger.Log($"{audio} not in `audio` directory", level: LogLevel.Important);
                usedFiles.Add(storage.GetFullPath(audio));
            }
        }

        // var path = storage.AbsolutePath;
        // var archive = storage.GetFullPath("archive");
        // foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        // {
        //     var extension = Path.GetExtension(file);
        //     var name = Path.GetFileName(file);
        //     if (extension == ".bjson" || file.StartsWith(archive) || name == ".cache.json") continue;
        //     if (!usedFiles.Contains(file))
        //     {
        //         Logger.Log($"Unused file {file}", level: LogLevel.Important);
        //     }
        // }
        var path = Path.Join(storage.AbsolutePath, "audio");
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            if (!usedFiles.Contains(file))
                Logger.Log($"Unused in /audio {file}", level: LogLevel.Important);
        }
    }
}