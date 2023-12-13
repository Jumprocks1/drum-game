using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DrumGame.Game.API;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Media;

public class AudioMerger
{
    public string OutputFile;
    public List<string> InputFiles = new();
    public List<Stream> InputStreams = new();
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
            process.AddArguments("-filter_complex", $"amerge=inputs={InputFiles.Count}");
            process.Vorbis(8);
            process.AddArguments("-ac", "2");
            process.AddOutput(OutputFile);
            await process.RunAsync(); // can't return here since we don't want to delete our files
        }
        finally
        {
            foreach (var file in tempFiles) File.Delete(file);
        }
    }
}