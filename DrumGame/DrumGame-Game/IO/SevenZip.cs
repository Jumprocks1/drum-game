using System;
using System.Diagnostics;
using System.IO;

namespace DrumGame.Game.IO;

public static class SevenZip
{
    public static FileProvider Open(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var dirPath = Path.Join(Path.GetDirectoryName(path), "7z-" + fileName);
        if (Directory.Exists(dirPath)) Directory.Delete(dirPath);
        var outputFolder = Directory.CreateDirectory(dirPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = "7z",
            WorkingDirectory = outputFolder.FullName
        };

        startInfo.ArgumentList.Add("x");
        startInfo.ArgumentList.Add(path);
        startInfo.ArgumentList.Add("-aos"); // skip existing files

        var proc = Process.Start(startInfo);
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            var error = proc.StandardOutput.ReadToEnd();
            throw new Exception($"Failed to run 7z with: {string.Join(", ", proc.StartInfo.ArgumentList)}\n\n\n{error}");
        }

        return new FileProvider(outputFolder)
        {
            EnumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                MaxRecursionDepth = 3
            }
        };
    }
}