using osu.Framework.Platform;
using osu.Framework;
using osu.Framework.Logging;
using DrumGame.Game.Utils;
using System;
using System.Linq;
using System.Globalization;
using osu.Framework.Graphics;
using DrumGame.Game.Commands;

namespace DrumGame.Desktop;

public static class Program
{
    static LogLevel LogLevel = LogLevel.Important;
    static bool AntiAliasing = true;
    public static bool Discord { get; private set; } = true;
    static event Action<Drawable> OnLoad;
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
            game.OnLoadComplete += OnLoad;
            host.Run(game);
        }
        if (AntiAliasing)
            GLUtil.StopSDLMultisample();
    }

    static bool ParseCL(string[] args)
    {
        var exit = false;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            bool nextArg(out string o)
            {
                if (args.Length > i + 1)
                {
                    o = args[++i];
                    return true;
                }
                Console.WriteLine($"Expected another argument after {arg}");
                o = null;
                return false;
            }
            if (arg == "-v" || arg == "--version")
            {
                Console.WriteLine(Util.VersionString);
                exit = true;
            }
            else if (arg == "--verbose") LogLevel = (LogLevel)Math.Max(0, (int)LogLevel - 1);
            else if (arg == "--no-aa") AntiAliasing = false;
            else if (arg == "--no-discord") Discord = false;
            else if (arg == "--sync")
                OnLoad += _ => Util.CommandController.ActivateCommand(Command.SyncMaps, Game.Browsers.BeatmapSelector.SyncOptions.Local);
            else if (!arg.StartsWith('-'))
                OnLoad += _ => Util.CommandController.ActivateCommand(Command.OpenFile, arg);
            else if (arg == "--wait-for-debugger" || arg == "--debug" || arg == "-d") Util.WaitForDebugger();
            else if (arg == "--command" || arg == "-c")
            {
                if (nextArg(out var command))
                {
                    OnLoad += _ =>
                    {
                        var commandInfo = CommandInfo.FromString(command);
                        Util.CommandController.ActivateCommand(commandInfo);
                    };
                }
            }
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

