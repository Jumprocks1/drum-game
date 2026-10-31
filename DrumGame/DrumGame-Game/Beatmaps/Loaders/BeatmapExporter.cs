using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using DrumGame.Game.Commands;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Beatmaps.Loaders;

public static class BeatmapExporter
{
    // also reveals in explorer
    public static bool Export(CommandContext _, Beatmap beatmap) => Export(_, [beatmap]);
    public static bool Export(CommandContext _, List<Beatmap> beatmaps)
    {
        try
        {
            if (beatmaps.Count == 0) return false;
            string outputName;
            if (beatmaps.Count == 1)
                outputName = Path.GetFileNameWithoutExtension(beatmaps[0].Source.AbsolutePath);
            else
                outputName = $"{DateTime.Now:yyyyMMddTHHmmss}_{beatmaps.Count}maps";
            var outputZipPath = Path.Join(Util.Resources.GetDirectory("exports").FullName, outputName + ".zip");
            var includedFiles = new List<string>();

            var commonPath = Path.GetDirectoryName(beatmaps[0].Source.AbsolutePath);

            foreach (var beatmap in beatmaps)
            {
                var mainPath = beatmap.Source.AbsolutePath;
                commonPath = Util.PathGCD(Path.GetDirectoryName(mainPath), commonPath) ?? throw new Exception("No common path");
                includedFiles.Add(mainPath);
                includedFiles.Add(beatmap.FullAudioPath());
                if (!string.IsNullOrWhiteSpace(beatmap.Image))
                    includedFiles.Add(beatmap.FullAssetPath(beatmap.Image));
            }


            using var archive = new ZipArchive(File.Open(outputZipPath, FileMode.Create), ZipArchiveMode.Create);
            foreach (var file in includedFiles)
                archive.CreateEntryFromFile(file, Path.GetRelativePath(commonPath, file).Replace('\\', '/'));
            Util.RevealInFileExplorer(outputZipPath);
        }
        catch (Exception e)
        {
            Util.Palette.UserError("Failed to export", e);
        }


        return true;
    }
}