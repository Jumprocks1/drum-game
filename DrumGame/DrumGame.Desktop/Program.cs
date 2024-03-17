using osu.Framework.Platform;
using osu.Framework;
using osu.Framework.Logging;
using DrumGame.Game.Utils;
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;

namespace DrumGame.Desktop;

public static class Program
{
    static LogLevel LogLevel = LogLevel.Important;
    static bool AntiAliasing = true;
    public static bool Discord { get; private set; } = true;
    static bool Sync;
    static List<string> OpenFiles;
    [STAThread]
    public static void Main(string[] args)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Util.StartTime = sw;

        if (args.Length > 0)
            if (ParseCL(args)) return;

        Logger.Level = LogLevel;
        if (AntiAliasing)
            GLUtil.StartSDLMultisample();
        if (!osu.Framework.Development.DebugUtils.IsDebugBuild) // release build doesn't print, but I like it
            Logger.NewEntry += LogHook;
        using (var host = Host.GetSuitableDesktopHost(@"DrumGame"))
        {
            Util.Host = host;
            using var game = new DrumGameDesktop();
            if (Sync)
                game.OnLoadComplete += _ => Util.CommandController.ActivateCommand(Game.Commands.Command.SyncMaps, Game.Browsers.BeatmapSelector.SyncOptions.Local);
            if (OpenFiles != null)
                game.OnLoadComplete += _ => Util.CommandController.ActivateCommand(Game.Commands.Command.OpenFile, parameters: OpenFiles.ToArray());
            host.Run(game);
        }
        if (AntiAliasing)
            GLUtil.StopSDLMultisample();
    }

    static bool ParseCL(string[] args)
    {
        var exit = false;
        foreach (var arg in args)
        {
            if (arg == "-v" || arg == "--version")
            {
                Console.WriteLine(Util.VersionString);
                exit = true;
            }
            else if (arg == "--verbose") LogLevel = (LogLevel)Math.Max(0, (int)LogLevel - 1);
            else if (arg == "--no-aa") AntiAliasing = false;
            else if (arg == "--no-discord") Discord = false;
            else if (arg == "--sync") Sync = true;
            else if (!arg.StartsWith("-")) (OpenFiles ??= new()).Add(arg);
            else if (arg == "--wait-for-debugger") Util.WaitForDebugger();
            else Console.WriteLine($"Unrecognized argument: {arg}");
        }
        return exit;
    }

    static void LogHook(LogEntry e)
    {
        // mostly copied from Logger.cs
        var logOutput = e.Message;

        if (e.Exception != null)
            logOutput += $"\n{e.Exception}";

        var lines = logOutput
            .Replace(@"\r\n", @"\n")
            .Split('\n')
            .Select(s => $@"{DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} [{e.Level.ToString().ToLowerInvariant()}]: {s.Trim()}");

        foreach (var line in lines)
            Console.WriteLine($"[{e.LoggerName.ToLowerInvariant()}] {line}");
    }
}

