using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DrumGame.Game.API;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Formats;
using DrumGame.Game.Channels;
using DrumGame.Game.Interfaces;
using DrumGame.Game.IO;
using DrumGame.Game.Media;
using DrumGame.Game.Utils;
using osu.Framework.Logging;

namespace DrumGame.Game.Beatmaps.Loaders;

public class DtxLoadConfig
{
    // this makes sure we don't save any changes
    // we won't copy images or audio files and we assume the final file stays in the same directory
    public bool MountOnly;
    // this skips loading all chips
    public bool MetadataOnly;
}

// based on https://osdn.net/projects/dtxmania/wiki/DTX+data+format
public partial class DtxLoader
{
    // this gets added to the offset after/during import
    // The DTXMania folks seem to prefer around +10-20ms (since that's what I most often have to add)
    // positive values will cause the BGM to start earlier
    // If this is set to 0, offset 0 will imply starting the track on beat 0 in DTX mania
    // The timing in DTX creator AL is all over the place, but I measured around 7ms on average (by putting BGM in left channel and snare in right)
    // Same for regular DTX Creator, but I measured a bit higher, around 12ms
    // This was set to 8, but I chagned it back to 0.
    public const double ImportOffset = 0;
    // if the level is below this and not the highest difficulty, print to console and skip import
    // only do this for difficulty names that are well known
    static string MinimumLevel => Util.ConfigManager.Get<string>(Stores.DrumGameSetting.MinimumDtxLevel);

    string PeekLevel = "00";

    IFileProvider Provider;
    MapImportContext Context;
    DtxLoadConfig Config;

    // only needed when importing a set.def
    List<Def> DefInfo;
    // essentially just caches image hashes so we only have to hash once. Only useful when there's multiple files with the same image reference
    public Dictionary<string, string> Images = new();

    public DtxLoader(IFileProvider provider, DtxLoadConfig config = null)
    {
        Provider = provider;
        Context = MapImportContext.Current; // we want to capture the context since we might end up doing async operations
        Config = config ?? new();
    }

    // we keep the latest set.def cached
    // as long as we load files sequentially, this will reduce the reads for that file
    // ex: folder has 5 `.dtx` files, first one causes lookup for set.def. Other files will see cached version.
    static (List<Def> defs, string directory) CachedDef;
    public static Beatmap LoadMounted(Stream stream, string fullPath, bool skipNotes)
    {
        // With skipNotes, 60% of the time spent is in ReadDtxLines, takes less than 1ms total
        // Without skipNotes it takes like 3ms, which is plenty fast enough
        var directory = Path.GetDirectoryName(fullPath);
        var provider = new FileProvider(directory);
        var loader = new DtxLoader(provider, new DtxLoadConfig
        {
            MetadataOnly = skipNotes,
            MountOnly = true
        });
        var res = loader.ImportDtxStream(stream);
        if (CachedDef.directory != directory)
            CachedDef = (ReadDef(((IFileProvider)provider).TryOpen("set.def", "SET.def"), provider.FolderName), directory);
        var defs = CachedDef.defs;
        ApplyDefInfo(defs, Path.GetFileName(fullPath), res.Item1);
        return res.Item1;
    }
    static string DifficultyMap(int dtxDifficulty) => (dtxDifficulty >= 100 ? dtxDifficulty / 10 : dtxDifficulty) switch
    {
        < 55 => "Easy",
        < 70 => "Normal",
        < 80 => "Hard",
        < 87 => "Insane",
        < 93 => "Expert",
        < 100 => "Expert+",
        _ => null
    };

    // start time is in ticks
    record DtxSample
    {
        public string Path;
        public int? StartTime;
        public double Volume = 1; // 0 to 1
        public (char, char) Id;
        public bool Bass;
        public bool SideStick;
        public bool Bell;
        public bool Ride;
        public bool Splash;
        public bool Open;
        public DtxSample(string path, int? startTime, (char, char) id)
        {
            Path = path; StartTime = startTime; Id = id;
            if (path.Contains("rim", StringComparison.InvariantCultureIgnoreCase) || path.Contains("SideStick", StringComparison.InvariantCultureIgnoreCase)
                 || path.Contains("Xstick", StringComparison.InvariantCultureIgnoreCase))
                if (!path.Contains("RimShot") && !path.Contains("Rim_Shot")) // usually this doesn't sound like a side stick
                    SideStick = true;
            if (path.Contains("Ride", StringComparison.CurrentCultureIgnoreCase)) Ride = true;
            if (path.Contains("Bell", StringComparison.CurrentCultureIgnoreCase)) Bell = true;
            if (path.Contains("Splash", StringComparison.CurrentCultureIgnoreCase) ||
                path.Contains("Spl", StringComparison.CurrentCultureIgnoreCase)) Splash = true;
            if (path.Contains("Open", StringComparison.InvariantCultureIgnoreCase)
                && !path.Contains("Half", StringComparison.InvariantCultureIgnoreCase)) Open = true;
            if (path.Contains("OHH", StringComparison.InvariantCultureIgnoreCase)) Open = true;
            if (path.Contains("HHO", StringComparison.InvariantCultureIgnoreCase)) Open = true;
            if (path.Contains("106")) Open = true;
            if (path == "69.xa" || path == "6A.xa") Open = true; // for GITADORA imports
        }
    }
    // extra info we can't fit in the Beatmap
    class DtxInfo
    {
        public List<DtxSample> BGMs = new();
        public Dictionary<(char, char), DtxSample> Samples = new();
        public DtxSample GetMainBgm()
        {
            DtxSample mainBgm = null;
            if (BGMs.All(e => !e.StartTime.HasValue))
                foreach (var bgm in BGMs) bgm.StartTime = 0;

            // try to not use file that's named "drums.ogg"
            mainBgm ??= BGMs.FirstOrDefault(e => e.StartTime.HasValue && e.Path != "drums.ogg");
            mainBgm ??= BGMs.FirstOrDefault(e => e.StartTime.HasValue);
            return mainBgm;
        }
    };

    record DtxDef { public string Label; public int DifficultyInt; public string File; }
    record Def
    {
        public string Title;
        public string MapSet;
        public Dictionary<char, DtxDef> DtxDefs = new();
    }

    (Beatmap, DtxInfo) ImportDtxStream(Stream stream)
    {
        var info = new DtxInfo();

        var o = Beatmap.Create();
        o.TickRate = BJson.DefaultTickRate * 2; // DTX supports up to 384 divisors
        o.Audio = Path.Join("audio", "na");
        o.HitObjects = new();
        o.TempoChanges = new();
        o.MeasureChanges = new();
        o.RelativeVolume = 0.4;
        // o.DifficultyName // this comes from the SET.def
        o.Tags = Config.MountOnly ? "dtx-mount" : "dtx-import";

        if (Context != null)
            o.MapSourceUrl = Context.Url;

        var bpmLookups = new Dictionary<int, Tempo>();
        var finalMeasure = 0; // used to calculate duration metadata

        // we have to do 2 passes in case the measure length changes out of order
        var secondPass = new List<(int measure, string channel, string value)>();
        foreach (var (code, value) in ReadDtxLines(stream))
        {
            if (code == "TITLE") o.Title = value;
            else if (code == "PREIMAGE")
            {
                if (Config.MountOnly) o.Image = value;
                else
                {
                    if (Images.TryGetValue(value, out var f))
                    {
                        o.Image = f;
                    }
                    else
                    {
                        try
                        {
                            var ext = Path.GetExtension(value) ?? "";
                            var hash = Util.MD5(Provider.Open(value)).ToLowerInvariant();
                            var newImagePath = "images/" + hash + ext;
                            var fullNewPath = Util.MapStorage.GetFullPath(newImagePath);
                            if (!File.Exists(fullNewPath))
                                Provider.Copy(value, fullNewPath);
                            o.Image = Images[value] = newImagePath;
                        }
                        catch (Exception e) { Logger.Error(e, "Error loading image"); }
                    }
                }
            }
            else if (code == "PREVIEW") { if (Config.MountOnly) o.PreviewAudio = value; }
            else if (code == "GENRE" && !string.IsNullOrWhiteSpace(value)) o.AddTags($"genre-{value}");
            else if (code == "ARTIST") o.Artist = value;
            else if (code == "DLEVEL")
            {
                o.Tags += $" dtx-level-{value}";
                if (int.TryParse(value, out var v))
                    o.Difficulty = DifficultyMap(v);
            }
            else if (code == "BGMWAV") info.BGMs.Add(info.Samples[(value[0], value[1])]);
            else if (code == "COMMENT") o.Description = value;
            else if (code.StartsWith("WAV"))
            {
                var id = (code[3], code[4]);
                info.Samples[id] = new DtxSample(value, null, id);
            }
            else if (code.StartsWith("VOLUME"))
            {
                var id = (code[6], code[7]);
                info.Samples[id].Volume = ParseDouble(value) / 100;
            }
            else if (code.StartsWith("BPM"))
            {
                var rem = code[3..];
                var parsed = ParseDouble(value);
                var tempo = new Tempo { BPM = parsed };
                if (rem.Length == 0)
                {
                    o.TempoChanges.Add(new TempoChange(0, tempo));
                    o.MedianBPM = parsed;
                }
                else bpmLookups[base36(rem)] = tempo;
            }
            else if (char.IsDigit(code[0]) && code.Length == 5)
            {
                var measure = int.Parse(code[0..3]);
                if (measure > finalMeasure) finalMeasure = measure;
                var channel = code[3..];
                // we only care about 01 for BGM, 02 for measure length, and 08 for tempo changes
                // we want the tempo/length changes so we can compute the map duration metadata
                if (Config.MetadataOnly && channel != "01" && channel != "02" && channel != "08")
                    continue;
                var channelInt = hex(channel);
                // https://github.com/limyz/DTXmaniaNX/blob/master/DTXMania/Code/Score%2CSong/EChannel.cs
                if (channel == "53") { } // cheer section
                else if (channel == "54") { } // movie timing, not needed
                else if (channel == "02") // measure length
                {
                    // if these come out of order, we are screwed
                    // we have to round them because if they are super weird, then it breaks everything else
                    var roundTo = o.TickRate / 12;
                    var targetLength = Math.Round(ParseDouble(value) * 4 * roundTo) / roundTo;
                    o.MeasureChanges.Add(new MeasureChange(o.TickFromMeasure(measure), targetLength));
                }
                else if (channelInt >= 49 && channelInt <= 60) { } // hidden chips (not played in DTX)
                else if (channelInt >= 97 && channelInt <= 146) { } // background sound triggers
                else if (channelInt >= 177 && channelInt <= 190) { } // default sound (when a hit is not matched to a chip)
                else if (channel == "C2") { } // hide bar lines
                else if (channel == "4F") { } // bonus effect?
                else secondPass.Add((measure, channel, value.Replace("_", "")));
            }
            else Logger.Log($"Unknown DTX code: {code} {value}", level: LogLevel.Important);
        }

        foreach (var (measure, channel, value) in secondPass)
        {
            var dc = ChannelMap(channel);
            var width = value.Length;
            var measureTick = o.TickFromMeasure(measure);
            var nextMeasureTick = o.TickFromMeasure(measure + 1);
            var ticksPerI = (nextMeasureTick - measureTick) / value.Length;

            void f(Action<int, (char, char)> handler)
            {
                for (var i = 0; i < value.Length; i += 2)
                {
                    if (value[i] != '0' || value[i + 1] != '0')
                        handler(measureTick + ticksPerI * i, (value[i], value[i + 1]));
                }
            }
            void s(Action<int, (char, char), DtxSample> handler)
            {
                for (var i = 0; i < value.Length; i += 2)
                {
                    if (value[i] != '0' || value[i + 1] != '0')
                    {
                        var id = (value[i], value[i + 1]);
                        var sample = info.Samples.GetValueOrDefault(id);
                        if (sample == null) Logger.Log($"Failed to locate sample for {id.Item1}{id.Item2}");
                        handler(measureTick + ticksPerI * i, id, sample);
                    }
                }
            }

            if (dc != DrumChannel.None)
            {
                var mod = NoteModifiers.None;
                if (channel == "1C" || channel == "1A") // 1C is left bass drum in DTX, 1A is left crash
                    mod = NoteModifiers.Left;
                else if (channel == "16") // 16 is right/main crash
                    mod = NoteModifiers.Right;
                var data = new HitObjectData(dc, mod);
                if (dc == DrumChannel.BassDrum) // for the bass channel, we have to mark the samples as bass drum samples
                    s((tick, id, sample) =>
                    {
                        if (sample != null) sample.Bass = true;
                        o.HitObjects.Add(new HitObject(tick, data));
                    });
                else if (dc == DrumChannel.HiHatPedal) // older maps use the HiHat channel for double bass, so we have to check for bass samples
                    s((tick, id, sample) =>
                    {
                        if (sample != null && sample.Bass)
                            o.HitObjects.Add(new HitObject(tick, new HitObjectData(DrumChannel.BassDrum, NoteModifiers.Left)));
                        else
                            o.HitObjects.Add(new HitObject(tick, data));
                    });
                else if (dc == DrumChannel.Snare)
                    s((tick, id, sample) =>
                    {
                        if (sample != null && sample.SideStick)
                            o.HitObjects.Add(new HitObject(tick, DrumChannel.SideStick));
                        else
                            o.HitObjects.Add(new HitObject(tick, data));
                    });
                else if (dc == DrumChannel.Crash || dc == DrumChannel.China || dc == DrumChannel.Ride)
                    s((tick, id, sample) =>
                    {
                        if (sample != null && sample.Bell)
                            o.HitObjects.Add(new HitObject(tick, DrumChannel.RideBell));
                        else if (sample != null && sample.Ride)
                            o.HitObjects.Add(new HitObject(tick, DrumChannel.Ride));
                        else if (sample != null && sample.Splash)
                            // splash can be L/R, so we include mod
                            o.HitObjects.Add(new HitObject(tick, new HitObjectData(DrumChannel.Splash, mod)));
                        else
                            // this includes the modifier for L/R cymbals
                            o.HitObjects.Add(new HitObject(tick, data));
                    });
                else if (dc == DrumChannel.OpenHiHat || dc == DrumChannel.ClosedHiHat)
                    s((tick, id, sample) =>
                    {
                        if (sample != null && sample.Open)
                            o.HitObjects.Add(new HitObject(tick, DrumChannel.OpenHiHat));
                        else
                            o.HitObjects.Add(new HitObject(tick, data));
                    });
                else
                    f((tick, _) => o.HitObjects.Add(new HitObject(tick, data)));
            }
            else if (channel == "01")
                f((tick, id) =>
                {
                    var found = info.BGMs.Find(e => e.Id == id);
                    if (found == null)
                    {
                        if (info.Samples.TryGetValue(id, out var sample)) // found a chart with an invalid chip on the BGM lane
                            info.BGMs.Add(found = sample);
                        else Logger.Log($"Failed to locate sample for {id.Item1}{id.Item2}", level: LogLevel.Important);
                    }
                    if (found != null)
                        found.StartTime = tick;
                });
            else if (channel == "08")
                f((tick, v) => o.TempoChanges.Add(new TempoChange(tick, bpmLookups[base36(v)])));
            else Logger.Log($"Unknown channel code: {measure} {channel} {value}", level: LogLevel.Important);
        }

        if (!Config.MetadataOnly)
        {
            o.HitObjects = o.HitObjects.OrderBy(e => e.Time).ToList();
            o.TempoChanges = o.TempoChanges.OrderBy(e => e.Time).ToList();
            o.RemoveDuplicates(); // this cleans up a bunch of stuff with the HitObjects
            o.RemoveExtras<TempoChange>();
            o.RemoveExtras<MeasureChange>();
        }
        else
        {
            // normally calculated with o.ComputeStats, but that's too intense
            o.PlayableDuration = o.MillisecondsFromTick(o.TickFromMeasure(finalMeasure));
        }

        if (Config.MountOnly) // if we are mounting, we will not be calling prepare for save, so we have to handle audio here
        {
            var mainBgm = info.GetMainBgm();
            if (mainBgm != null)
            {
                o.StartOffset = -o.MillisecondsFromTick(mainBgm.StartTime.Value) + ImportOffset;
                o.RelativeVolume *= mainBgm.Volume;
                o.Audio = mainBgm.Path;
            }
            else Logger.Log($"No BGM found for {o.Title}");
            o.HashId();
        }

        return (o, info);
    }

    async Task PrepareForSave(string localFileName, Beatmap beatmap, DtxInfo info)
    {
        // copies audio and sets some path related info

        var mapStorage = Util.DrumGame.MapStorage;
        var outputFolder = mapStorage.AbsolutePath;
        var folderName = Provider.FolderName;
        if (folderName.StartsWith("7z-"))
            folderName = folderName[3..];
        if (folderName.StartsWith("temp"))
            folderName = folderName[4..];
        if (folderName.StartsWith("-download"))
            folderName = folderName[9..];

        var outputFilename = (folderName + "-" + beatmap.MetaHash()[0..8].ToLowerInvariant() + "-" + localFileName).ToFilename(".bjson");
        beatmap.Source = new BJsonSource(Path.Join(outputFolder, outputFilename), BJsonFormat.Instance);

        var mainBgm = info.GetMainBgm();
        if (mainBgm != null)
        {
            beatmap.StartOffset = -beatmap.MillisecondsFromTick(mainBgm.StartTime.Value) + ImportOffset;
            beatmap.RelativeVolume *= mainBgm.Volume;

            var fullBgmNames = new List<string> { "bgm-full.ogg", "bgm_b.ogg" };
            if (mainBgm.Path == "drumless.ogg")
                fullBgmNames.Add("bgm.ogg");
            var fullFound = false; // setting this will disable any FFmpeg mixing
            foreach (var name in fullBgmNames)
            {
                if (Provider.Exists(name))
                {
                    mainBgm.Path = name;
                    fullFound = true;
                    break;
                }
            }

            var audioExt = Path.GetExtension(mainBgm.Path);
            var audioHash = Provider.Exists(mainBgm.Path) ? "_" + Util.MD5(Provider.Open(mainBgm.Path))[0..8].ToLowerInvariant() : "";
            var audioName = $"{folderName}-{Path.GetFileNameWithoutExtension(mainBgm.Path)}".ToFilename();
            beatmap.Audio = $"audio/{audioName}{audioHash}{audioExt}";

            if (Provider.Exists(mainBgm.Path))
            {
                // copy audio to mp3 file
                // for now we will actually just use the exe since Idk how to use FFmpeg API
                // this should teach me how to do it
                // https://github.com/Ruslan-B/FFmpeg.AutoGen/blob/master/FFmpeg.AutoGen.Examples.Encode/Program.cs
                var ffmpegMix = new HashSet<string>(info.BGMs.Where(e => e.StartTime.HasValue).Select(e => e.Path));

                var drumStems = new List<string> { "drums.ogg", "drums.mp3" };
                if (mainBgm.Path == "bgm.ogg")
                    drumStems.Add("bgmd.ogg");
                if (mainBgm.Path.Contains("(No Drums)"))
                    drumStems.Add(mainBgm.Path.Replace("(No Drums)", "(Drums)"));
                foreach (var file in drumStems)
                {
                    if (Provider.Exists(file))
                    {
                        ffmpegMix.Add(file);
                        break;
                    }
                }

                if (!fullFound && ffmpegMix.Count > 1) // merge with FFmpeg, could compare length before merging just to be safe
                {
                    try
                    {
                        if (!mapStorage.Exists(beatmap.FullAudioPath()))
                        {
                            Logger.Log("Merging drum audio");
                            var merger = new AudioMerger();
                            foreach (var path in ffmpegMix)
                                merger.InputStreams.Add(Provider.Open(path));
                            merger.OutputFile = beatmap.FullAudioPath();
                            await merger.MergeAsync();
                        }
                        beatmap.Audio = Path.ChangeExtension(beatmap.Audio, ".ogg");
                    }
                    catch // if no FFmpeg, do regular copy
                    {
                        if (!mapStorage.Exists(beatmap.FullAudioPath()))
                            Provider.Copy(mainBgm.Path, beatmap.FullAudioPath());
                    }
                }
                else
                {
                    if (!mapStorage.Exists(beatmap.FullAudioPath()))
                        Provider.Copy(mainBgm.Path, beatmap.FullAudioPath());
                }
            }
            else Logger.Log($"Missing {mainBgm.Path}, skipping copy");
        }
        beatmap.HashId();
    }

    // this supports opening a subpath in a ZipFileProvider
    public static Task ImportDtx(string path) => ImportDtx(new FileProvider(Path.GetDirectoryName(path)), Path.GetFileName(path));
    public static Task ImportDtx(IFileProvider provider, string localFileName) => new DtxLoader(provider).ImportDtxInternal(localFileName);
    Task ImportDtxInternal(DtxDef dtxDef) => ImportDtxInternal(dtxDef.File, dtxDef);
    async Task ImportDtxInternal(string localFileName, DtxDef dtxDef = null)
    {
        Logger.Log($"Importing {localFileName} from {Provider.FolderName}", level: LogLevel.Important);

        using var file = Provider.Open(localFileName);
        var (o, info) = ImportDtxStream(file);
        o.CreationTimeUtc = Provider.CreationTimeUtc(localFileName);

        if (!string.IsNullOrWhiteSpace(MinimumLevel) && dtxDef != null && dtxDef.DifficultyInt != -1)
        {
            var dtxLevel = o.GetDtxLevel();
            if (dtxLevel != null)
            {
                if (string.CompareOrdinal(dtxLevel, PeekLevel) >= 0) // if we are greater than the peek, always import
                    PeekLevel = dtxLevel;
                else if (string.CompareOrdinal(dtxLevel, MinimumLevel) < 0)
                {
                    Logger.Log($"Skipping import of {dtxDef.Label} difficulty (level: {dtxLevel}, min: {MinimumLevel}, peek: {PeekLevel})", level: LogLevel.Important);
                    return;
                }
            }
        }

        await PrepareForSave(localFileName, o, info);

        DefInfo ??= ReadDef(Provider.TryOpen("set.def", "SET.def"), Provider.FolderName);
        ApplyDefInfo(DefInfo, localFileName, o);
        o.Export();
        o.SaveToDisk(Util.DrumGame.MapStorage, Context);
    }
    static List<Def> ReadDef(Stream stream, string folderName)
    {
        if (stream == null) return null;
        var lastPartOfPath = Path.GetFileName(folderName);
        var o = new List<Def>();
        Def def = null;
        foreach (var (code, value) in ReadDtxLines(stream))
        {
            if (code == "TITLE") o.Add(def = new Def
            {
                Title = value,
                MapSet = $"{lastPartOfPath}/{value}"
            });
            else if (code.StartsWith('L'))
            {
                if (def == null)
                    o.Add(def = new Def());
                var id = code[1];
                var type = code[2..];
                var dtx = def.DtxDefs.GetValueOrDefault(id) ?? new DtxDef();
                if (type == "LABEL")
                {
                    dtx.Label = value;
                    dtx.DifficultyInt = DifficultyInt(value);
                }
                else if (type == "FILE")
                    dtx.File = value;
                def.DtxDefs[id] = dtx;
            }
        }
        return o;
    }

    public static Task ImportDef(string path) => ImportDef(new FileProvider(Path.GetDirectoryName(path)), Path.GetFileName(path));
    public static Task ImportDef(IFileProvider provider, string localFileName) => new DtxLoader(provider).ImportDefInternal(localFileName);
    async Task ImportDefInternal(string localFileName)
    {
        Logger.Log($"Importing set.def at {Provider.FolderName}", level: LogLevel.Important);
        DefInfo = ReadDef(Provider.Open(localFileName), Path.Join(Provider.FolderName, Path.GetDirectoryName(localFileName)));
        Logger.Log($"Loading {DefInfo.FirstOrDefault()?.Title}", level: LogLevel.Important);
        foreach (var set in DefInfo)
        {
            var orderedDef = set.DtxDefs.Select(e => e.Value).OrderByDescending(e => e.DifficultyInt);
            PeekLevel = "00"; // we want to always import the highest difficulty of each title set
            foreach (var dtx in orderedDef)
            {
                if (string.IsNullOrWhiteSpace(dtx.File) || !Provider.Exists(dtx.File))
                {
                    Logger.Log($"Skipping {dtx.File} - not found");
                    continue;
                }
                // could add try catch here, we had a practice.dtx file fail because it didn't have BGM,
                //    but the other difficulties were fine
                await ImportDtxInternal(dtx);
            }
        }
    }
}

