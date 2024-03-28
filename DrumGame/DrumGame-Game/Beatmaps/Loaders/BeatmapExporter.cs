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
    public static bool Export(CommandContext _, Beatmap beatmap)
    {
        try
        {
            var mainPath = beatmap.Source.AbsolutePath;
            var fileNameNoExt = Path.GetFileNameWithoutExtension(mainPath);
            var relativeTo = Path.GetDirectoryName(mainPath);
            var includedFiles = new List<string>();

            includedFiles.Add(mainPath);
            includedFiles.Add(beatmap.FullAudioPath());
            if (!string.IsNullOrWhiteSpace(beatmap.Image))
                includedFiles.Add(beatmap.FullAssetPath(beatmap.Image));

            var outputZipPath = Path.Join(Util.Resources.GetDirectory("exports").FullName, fileNameNoExt + ".zip");

            using (var archive = new ZipArchive(File.Open(outputZipPath, FileMode.Create), ZipArchiveMode.Create))
            {
                foreach (var file in includedFiles)
                    archive.CreateEntryFromFile(file, Path.GetRelativePath(relativeTo, file).Replace('\\', '/'));
            }

            Util.RevealInFileExplorer(outputZipPath);
        }
        catch (Exception e)
        {
            Util.Palette.UserError("Failed to export", e);
        }


        return true;
    }
}