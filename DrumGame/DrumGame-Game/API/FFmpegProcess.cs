using System;
using System.Diagnostics;
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
        var location = Util.Resources.LocateExecutable("ffmpeg");
        StartInfo = new ProcessStartInfo(location);
    }

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

    public void SimpleAudio() // removes images
    {
        AddArgument("-map_metadata");
        AddArgument("-1");
        AddArgument("-vn");
    }

    public Action<Process> AfterStart;

    public bool Success;

    public async Task RunAsync()
    {
        if (outputPath != null) AddArgument(outputPath);
        try
        {
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
            Success = true;
        }
        catch (Exception e) { Logger.Error(e, "Error while " + Description ?? "running FFmpeg"); }
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