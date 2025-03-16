using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DrumGame.Game.API;
using DrumGame.Game.Utils;
using osu.Framework.Logging;

namespace DrumGame.Game.Media;

public class AudioMerger
{
    public string OutputFile;
    public List<string> InputFiles = new();
    public List<double> InputDelaysMs;
    public List<Stream> InputStreams = new();

    // Idk how to do async/sync without copy pasting code ¯\_(ツ)_/¯
    public void AmixSync()
    {
        // this uses amix instead of amerge, amerge seems to have issues with mixing stereo/mono
        var tempFiles = new List<string>();
        try
        {
            foreach (var stream in InputStreams)
            {
                using var s = stream;
                var tempFile = Path.Join(Util.Resources.Temp.FullName, Guid.NewGuid().ToString());
                tempFiles.Add(tempFile);
                using var os = File.OpenWrite(tempFile);
                s.CopyTo(os);
                InputFiles.Add(tempFile);
            }

            var process = new FFmpegProcess("merging audio");
            foreach (var file in InputFiles) process.AddInput(file);
            process.AddArguments("-filter_complex", $"amix=inputs={InputFiles.Count}:normalize=0");
            process.Vorbis(8);
            process.AddArguments("-ac", "2");
            process.AddArgument("-vn");
            process.AddOutput(OutputFile);
            Directory.CreateDirectory(Path.GetDirectoryName(OutputFile));
            process.Run(); // can't return here since we don't want to delete our files
        }
        finally
        {
            foreach (var file in tempFiles) File.Delete(file);
        }
    }

    public string InputDelayFilter()
    {
        if (InputDelaysMs == null || InputDelaysMs.Count == 0 || InputDelaysMs.Count != InputFiles.Count) return null;
        // https://stackoverflow.com/a/31957317/11435204
        var min = InputDelaysMs.Min();
        for (var i = 0; i < InputDelaysMs.Count; i++)
            InputDelaysMs[i] -= min;
        if (InputDelaysMs.All(e => e < 1)) return null;
        Logger.Log("Attempting input delay FFmpeg mixing", level: LogLevel.Important);
        var filterSteps = new List<string>();
        var newInputs = new List<string>();
        for (var i = 0; i < InputDelaysMs.Count; i++)
        {
            var delay = InputDelaysMs[i];
            if (delay < 1) newInputs.Add($"[{i}:a]");
            else
            {
                filterSteps.Add($"aevalsrc=0:d={delay / 1000}[s{i}]");
                filterSteps.Add($"[s{i}][{i}:a]concat=n=2:v=0:a=1[ac{i}]");
                newInputs.Add($"[ac{i}]");
            }
        }
        filterSteps.Add($"{string.Join("", newInputs)}amerge=inputs={InputFiles.Count}[aout]");
        return string.Join(';', filterSteps);
    }

    // can throw exception
    public async Task MergeAsync()
    {
        var tempFiles = new List<string>();
        // TODO in the future we should hook this up with FFmpeg.AutoGen
        // https://github.com/Ruslan-B/FFmpeg.AutoGen/blob/master/FFmpeg.AutoGen.Examples.Encode/Program.cs


        // this is what we want to do here:
        //  ffmpeg -i InputFiles[0] -i InputFiles[1] -filter_complex amerge=inputs=2 -ac 2 -ab 320k o.mp3

        try
        {
            foreach (var stream in InputStreams)
            {
                using var s = stream;
                var tempFile = Path.Join(Util.Resources.Temp.FullName, Guid.NewGuid().ToString());
                tempFiles.Add(tempFile);
                using var os = File.OpenWrite(tempFile);
                s.CopyTo(os);
                InputFiles.Add(tempFile);
            }

            var process = new FFmpegProcess("merging audio");
            foreach (var file in InputFiles) process.AddInput(file);
            var inputDelay = InputDelayFilter();
            if (inputDelay != null)
            {
                process.AddArguments("-filter_complex", inputDelay);
                process.AddArguments("-map", "[aout]");
            }
            else
                process.AddArguments("-filter_complex", $"amerge=inputs={InputFiles.Count}");
            process.Vorbis(8);
            process.AddArguments("-ac", "2");
            process.AddArgument("-vn"); // remove video, I had this cause a crash with BASS for End to end (https://approvedtx.blogspot.com/2017/04/251-end-to-end.html)
            process.AddOutput(OutputFile);
            Directory.CreateDirectory(Path.GetDirectoryName(OutputFile));
            await process.RunAsync(); // can't return here since we don't want to delete our files
        }
        finally
        {
            foreach (var file in tempFiles) File.Delete(file);
        }
    }
}