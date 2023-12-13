using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DrumGame.Game.Commands;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Logging;

namespace DrumGame.Game.IO;

public class SSHSync
{
    public readonly string Host;
    public readonly string RemotePath;
    public readonly string LocalPath;
    public SSHSync(string remotePath, string localPath)
    {
        var spl = remotePath.Split(':', 2);
        Host = spl[0];
        RemotePath = spl[1];
        LocalPath = localPath;
    }
    public SSHSync(string host, string remotePath, string localPath)
    {
        Host = host;
        RemotePath = remotePath;
        LocalPath = localPath;
    }

    public static SSHSync From(CommandContext context)
    {
        var sshTarget = GetSyncTarget(context);
        if (sshTarget == null) return null;
        return new SSHSync(sshTarget, Util.Resources.AbsolutePath);
    }

    public static string GetSyncTarget(CommandContext context = null)
    {
        var target = Util.ConfigManager.GetBindable<string>(DrumGameSetting.SyncTarget).Value;
        if (string.IsNullOrWhiteSpace(target))
        {
            context?.ShowMessage("Configure a sync target to use this command");
            return null;
        }
        return target;
    }

    public record SyncFile(int Length, string Name)
    {
        public SyncFile() : this(0, null) { } // used for JSON
        public SyncFile(FileInfo fi) : this((int)fi.Length, fi.Name) { }
    }

    public record DiffResult(HashSet<SyncFile> Local, HashSet<SyncFile> Remote, List<SyncFile> LocalMissing, List<SyncFile> RemoteMissing);

    class GentleComparer : IEqualityComparer<SyncFile>
    {
        public bool Equals(SyncFile x, SyncFile y) => x.Name == y.Name;

        public int GetHashCode(SyncFile obj) => obj.Name.GetHashCode();
    }

    public DiffResult Diff(string relPath, bool gentle)
    {
        var local = Path.GetFullPath(relPath, LocalPath);
        var remote = Path.Join(RemotePath, relPath);

        // this currently does not use LastWriteTimeUtc unfortunately
        var lsCommand = $"ls -File {remote} | Select Length,Name,LastWriteTimeUtc";
        var remoteOutput = JsonSsh<SyncFile[]>(Host, lsCommand).ToHashSet();

        var di = new DirectoryInfo(local);
        var localOutput = di.GetFiles().Select(e => new SyncFile(e)).ToHashSet();

        var localMissing = remoteOutput.Except(localOutput, gentle ? new GentleComparer() : null).ToList();
        var remoteMissing = localOutput.Except(remoteOutput, gentle ? new GentleComparer() : null).ToList();

        return new DiffResult(localOutput, remoteOutput, localMissing, remoteMissing);
    }

    public void CopyToRemote(string directory, IEnumerable<string> files)
    {
        if (files.TryGetNonEnumeratedCount(out var c) && c == 0) return;
        var remote = Path.GetFullPath(directory, RemotePath);
        var startInfo = new ProcessStartInfo { FileName = "scp" };
        startInfo.ArgumentList.Add("-T");
        foreach (var file in files) startInfo.ArgumentList.Add(Path.GetFullPath(Path.Join(directory, file), LocalPath));
        startInfo.ArgumentList.Add($"{Host}:{remote}");
        Execute(startInfo);
    }
    void Execute(ProcessStartInfo startInfo)
    {
        startInfo.RedirectStandardError = true;
        startInfo.RedirectStandardOutput = true;
        var proc = Process.Start(startInfo);
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            var error = proc.StandardError.ReadToEnd();
            // pretend we were successful
            if (error.Contains("dbus_pending_call"))
                return;
            Logger.Log($"Failed to run with arguments: {string.Join(", ", proc.StartInfo.ArgumentList)}", level: LogLevel.Error);
            Logger.Log(proc.StandardOutput.ReadToEnd(), level: LogLevel.Error);
            Logger.Log(error, level: LogLevel.Error);
            throw new BackgroundTaskException("Connection failed.");
        }
    }
    public void CopyRemoteFile(string remoteFile, string localFile)
    {
        var fullRemote = Path.Join(RemotePath, remoteFile);
        var startInfo = new ProcessStartInfo { FileName = "scp" };
        startInfo.ArgumentList.Add("-T");
        startInfo.ArgumentList.Add($"{Host}:\"{fullRemote}\"");
        startInfo.ArgumentList.Add(Path.GetFullPath(localFile, LocalPath));
        Execute(startInfo);
    }
    public void CopyToLocal(string directory, string file) => CopyToLocal(directory, new string[] { file });
    public void CopyToLocal(string directory, IEnumerable<string> files)
    {
        if (files.TryGetNonEnumeratedCount(out var c) && c == 0) return;
        var local = Path.GetFullPath(directory, LocalPath);
        var startInfo = new ProcessStartInfo { FileName = "scp" };
        startInfo.ArgumentList.Add("-T");
        var fileList = string.Join(' ', files.Select(e => $"\"{Path.Join(RemotePath, Path.Join(directory, e))}\""));
        startInfo.ArgumentList.Add($"{Host}:{fileList}");
        startInfo.ArgumentList.Add(local);
        Execute(startInfo);
    }

    public static T JsonSsh<T>(string host, string command) where T : class
    {
        string Escape(string s) => s.Replace("\"", "\\\"");
        Logger.Log($"running {command} on {host}");
        command += " | ConvertTo-Json -EscapeHandling EscapeNonAscii";
        var proc = Process.Start(new ProcessStartInfo
        {
            FileName = "ssh",
            Arguments = $"{host} \"{Escape(command)}\"",
            RedirectStandardOutput = true,
        });
        var output = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(proc.StandardOutput.ReadToEnd());
        proc.Close();
        return output;
    }
}