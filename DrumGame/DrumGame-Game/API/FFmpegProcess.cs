using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DrumGame.Game.Utils;
using osu.Framework.Logging;

namespace DrumGame.Game.API;

public class FFmpegProcess
{
    public readonly ProcessStartInfo StartInfo;
    public readonly string Description;
    public FFmpegProcess(string description = null)
    {
        Description = description;
        StartInfo = new ProcessStartInfo(Executable);
    }

    public static string Executable => Util.Resources.LocateExecutable("ffmpeg");

    string outputPath;

    public void AddOutput(string path)
    {
        outputPath = path;
    }

    public void AddArgument(string argument) => StartInfo.ArgumentList.Add(argument);
    public void AddArguments(params string[] arguments)
    {
        foreach (var argument in arguments) StartInfo.ArgumentList.Add(argument);
    }
    public void AddInput(string path)
    {
        AddArgument("-i");
        AddArgument(path);
        if (path == "-")
            StartInfo.RedirectStandardInput = true;
    }
    public void OffsetMs(double offsetMs)
    {
        if (offsetMs == 0) return;
        AddArguments("-af", $"adelay={offsetMs}ms:all=true");
    }
    public void Vorbis(double q = 3)
    {
        AddArguments("-c:a", "libvorbis");
        // See https://en.wikipedia.org/wiki/Vorbis#Technical_details for bitrates
        if (q != 3)
            AddArguments("-q:a", q.ToString()); // default is 3, q6 targets 192kbps
    }
    public void MultiplyVolume(double mult)
    {
        mult = Math.Clamp(mult, 0, 1);
        if (mult == 1) return;
        AddArguments("-af", $"volume={mult}");
    }

    public void ExtractImage(string outputPath)
    {
        AddArgument("-an");
        AddArguments("-vcodec", "copy");
        AddOutput(outputPath);
    }

    public void SwapChannels() => AddArguments("-map_channel", "0.0.1", "-map_channel", "0.0.0");
    public void SimpleAudio() // removes images
    {
        AddArgument("-map_metadata");
        AddArgument("-1");
        AddArgument("-vn");
    }

    public Action<Process> AfterStart;

    public bool Success;

    // note, this throws exceptions, but the regular Run method doesn't yet
    public async Task RunAsync()
    {
        if (string.IsNullOrWhiteSpace(StartInfo.FileName))
            throw new FileNotFoundException("ffmpeg executable not found");
        if (outputPath != null) AddArgument(outputPath);
        var proc = Process.Start(StartInfo);
        AfterStart?.Invoke(proc);
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
        {
            string error;
            if (StartInfo.RedirectStandardOutput)
                error = proc.StandardOutput.ReadToEnd();
            else error = "See console output for error";
            throw new Exception($"Failed to run {StartInfo.FileName} with: {string.Join(", ", proc.StartInfo.ArgumentList)}\n\n\n{error}");
        }
    }
    // public void Run() => RunAsync().Wait(); // this dead locked, not sure why
    public void Run()
    {
        if (outputPath != null) AddArgument(outputPath);
        try
        {
            var proc = Process.Start(StartInfo);
            AfterStart?.Invoke(proc);
            proc.WaitForExit();
            if (proc.ExitCode != 0)
            {
                string error;
                if (StartInfo.RedirectStandardOutput)
                    error = proc.StandardOutput.ReadToEnd();
                else error = "See console output for error";
                throw new Exception($"Failed to run {StartInfo.FileName} with: {string.Join(", ", proc.StartInfo.ArgumentList)}\n\n\n{error}");
            }
            Success = true;
        }
        catch (Exception e) { Logger.Error(e, "Error while " + Description ?? "running FFmpeg"); }
    }
}