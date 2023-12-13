using System.IO;
using System.IO.Compression;
using System.Linq;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Channels;
using DrumGame.Game.Stores;
using DrumGame.Game.IO.Osz;
using osu.Framework.Logging;
using osu.Framework.Extensions.EnumExtensions;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Beatmaps.Loaders;

public static class OszBeatmapLoader
{
    static Beatmap Convert(OsuFile osu)
    {
        var o = Beatmap.Create();
        o.TickRate = BJson.DefaultTickRate;
        o.Audio = Path.Join("audio", osu.General.AudioFilename);
        o.StartOffset = osu.TimingPoints.First().Time;
        o.HitObjects = new();
        o.TempoChanges = new();
        o.Artist = osu.Metadata.Artist;
        o.Mapper = osu.Metadata.Creator;
        o.Title = osu.Metadata.Title;
        o.Difficulty = "Normal";
        o.DifficultyName = osu.Metadata.Version;
        o.Tags = "osu-import";

        var gamemode = osu.General.Mode;
        if (gamemode == OsuFile.Mode.Taiko) o.Tags += " taiko";

        // this doesn't work 100% when there's red lines (caused by multiple offsets)
        // this should only occur in poorly timed maps
        foreach (var t in osu.TimingPoints)
        {
            if (t.BeatLength > 0)
            {
                o.TempoChanges.Add(new TempoChange(
                    o.TickFromBeat(Beatmap.RoundBeat(o.BeatFromMilliseconds(t.Time), 32)),
                    new Tempo { MicrosecondsPerQuarterNote = (int)(t.BeatLength * 1000 + 0.5m) }));
            }
        }
        o.RemoveExtras<TempoChange>();

        foreach (var h in osu.HitObjects)
        {
            var channel = h.HitSound.HasFlagFast(OsuFile.HitSound.Clap) || h.HitSound.HasFlagFast(OsuFile.HitSound.Whistle)
                ? DrumChannel.Rim : DrumChannel.Snare;
            var accent = h.HitSound.HasFlagFast(OsuFile.HitSound.Finish);
            var modifier = accent ? NoteModifiers.Accented : NoteModifiers.None;
            var tick = o.TickFromBeat(Beatmap.RoundBeat(o.BeatFromMilliseconds(h.Time), 96));
            if (h.Type.HasFlagFast(OsuFile.HitType.Slider))
            {
                // TODO length is hard
                o.HitObjects.Add(new RollHitObject(tick, new HitObjectData(channel, modifier), o.TickRate / 4));
            }
            else if (h.Type.HasFlagFast(OsuFile.HitType.Spinner))
            {
                var endTime = int.Parse(h.Params.Split(',')[0]);
                var endTick = o.TickFromBeat(Beatmap.RoundBeat(o.BeatFromMilliseconds(endTime), 96));
                var length = endTick - tick;
                o.HitObjects.Add(new RollHitObject(tick, new HitObjectData(DrumChannel.Snare, modifier), length));
                o.HitObjects.Add(new RollHitObject(tick, new HitObjectData(DrumChannel.Rim, modifier), length));
            }
            else
                o.HitObjects.Add(new HitObject(tick, new HitObjectData(channel, modifier)));
        }

        return o;
    }
    public static void ImportOsu(MapStorage mapStorage, string absolutePath)
    {
        Logger.Log($"Importing osu at {absolutePath}", level: LogLevel.Important);

        var outputFolder = mapStorage.AbsolutePath;
        var osuFile = new OsuFile(absolutePath);

        var o = Convert(osuFile);
        var outputFilename = o.Title.ToFilename(".bjson");
        if (File.Exists(Path.Join(outputFolder, outputFilename)))
            outputFilename = Path.GetFileName(absolutePath) + ".bjson";

        o.Source = new BJsonSource(Path.Join(outputFolder, "audio", outputFilename));

        var osuAudio = Path.Join(Path.GetDirectoryName(absolutePath), osuFile.General.AudioFilename);


        if (File.Exists(o.FullAudioPath()))
        {
            var currentSize = new FileInfo(o.FullAudioPath()).Length;
            var newFileSize = new FileInfo(osuAudio).Length;
            if (currentSize != newFileSize) o.Audio = Path.Join("audio", o.Title.ToFilename() + Path.GetExtension(osuAudio));
        }

        if (!File.Exists(o.FullAudioPath()))
        {
            Logger.Log($"Saving audio to {o.FullAudioPath()}", level: LogLevel.Important);
            File.Copy(osuAudio, o.FullAudioPath());
        }
        o.Export();
        o.SaveToDisk(mapStorage);
        Util.CommandController.ActivateCommand(Commands.Command.Refresh);
    }
    public static void ImportOsz(MapStorage mapStorage, string absolutePath)
    {
        // this needs to be redone to better match ImportOsu
        Logger.Log($"Importing osz at {absolutePath}", level: LogLevel.Important);
        var folderName = Path.GetFileNameWithoutExtension(absolutePath);
        var output = mapStorage.GetStorageForDirectory(folderName);
        using var osz = File.OpenRead(absolutePath);
        using var oszReader = new OszReader(osz);
        foreach (var name in oszReader.ListOsuFiles())
        {
            var osu = oszReader.ReadOsu(name);
            var o = Convert(osu);
            o.Source = new BJsonSource(output.GetFullPath(name + ".bjson"));

            var audioPath = output.GetFullPath(o.Audio);
            Logger.Log($"Saving audio to {audioPath}");
            oszReader.Archive.GetEntry(o.Audio).ExtractToFile(audioPath, true);
            o.Export();
            o.SaveToDisk(mapStorage);
        }
    }
}

