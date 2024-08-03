using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using DrumGame.Game.API;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Browsers;
using DrumGame.Game.Channels;
using DrumGame.Game.Commands;
using DrumGame.Game.Interfaces;
using DrumGame.Game.IO;
using DrumGame.Game.Media;
using DrumGame.Game.Modals;
using DrumGame.Game.Stores;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace DrumGame.Game.Beatmaps.Loaders;

// 1 instance for set of maps exported
// Typically this is just 1 map, but if export all difficulties is checked, the instance is shared
public class DtxExporter
{
    public static double ExportOffset => -DtxLoader.ImportOffset;
    static string Base36Key = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    MapStorage MapStorage => Util.DrumGame.MapStorage;
    static string base36(int i) => new(new char[] { Base36Key[i / 36], Base36Key[i % 36] });
    record WavChipKey(DrumChannel Channel, NoteModifiers VelocityModifier, NoteModifiers Sticking, double VolumeMultiplier)
    {
        public byte ComputedVelocity
        {
            get
            {
                var v = HitObjectData.ComputeVelocity(VelocityModifier);
                if (VolumeMultiplier != 1)
                    v = (byte)Math.Clamp(Math.Floor(v * VolumeMultiplier), 0, 127);
                return v;
            }
        }
    }
    record WavChip
    {
        public string Id;
        public byte Note;
        public double VelocityMultiplier => Key.VolumeMultiplier;
        public string Filename;
        public double Volume = 100;
        public double Pan = 0;
        public bool BGM;
        public NoteModifiers Sticking => Key.Sticking;
        public NoteModifiers Velocity => Key.VelocityModifier;
        public WavChipKey Key;
        public void WriteTo(DtxWriter writer)
        {
            writer.WriteLine($"#WAV{Id}: {Filename}");
            // if velocity goes over 127, we could apply the overflow  to the volume
            var outputVolume = Math.Round(Math.Clamp(Volume, 0, 100));
            // var outputVolume = Math.Round(Math.Clamp(Volume * VelocityMultiplier, 0, 100));

            if (outputVolume != 100)
                writer.WriteLine($"#VOLUME{Id}: {outputVolume}");

            var width = 100;
            if (Velocity.HasFlag(NoteModifiers.Ghost) || Velocity.HasFlag(NoteModifiers.Roll))
                width = writer.Exporter.Config.GhostNoteWidth;
            if (width != 100)
                writer.WriteLine($"#SIZE{Id}: {width}");

            var correctedPan = Pan;
            if (Sticking == NoteModifiers.Right)
                correctedPan = Math.Abs(Pan);
            if (Sticking == NoteModifiers.Left)
                correctedPan = -Math.Abs(Pan);
            var outputPan = Math.Round(correctedPan);
            if (outputPan != 0)
                writer.WriteLine($"#PAN{Id}: {outputPan}");
            if (BGM)
                writer.WriteLine($"#BGMWAV: {Id}");
        }
    }
    List<WavChip> Samples = new();
    Dictionary<WavChipKey, WavChip> SampleMap = new();
    BookmarkVolumeEvent[] VolumeEvents;
    Dictionary<int, DrumChannel> HiHatHits;
    double BgmEncodingOffset; // if 0, no need to do anything. Should always be positive
    public enum OutputFormat
    {
        Folder, // default
        Zip, // this also updates the folder before zipping
        SevenZip
    }
    public enum SampleMethod
    {
        // for now we will use ffmpeg command line pipes
        // if ffmpeg is not found, change method to empty files
        SoundFontEncodingMissing,
        EmptyFiles,
        Ignore,
    }
    public class ExportConfig
    {
        public OutputFormat OutputFormat;
        public SampleMethod SampleMethod;
        public bool OpenAfterExport = true;
        public bool ExportMapSet;
        public string ExportName;
        public double RollDivisor;
        public int GhostNoteWidth = 100;
        public bool EncodeOffset;
        public bool IncludeVideo;
        public int OffbeatHiHatVolume = 100;
        public bool ExcludeMeasureChanges = true; // TODO add config
    }

    class DtxWriter : StreamWriter
    {
        public readonly DtxExporter Exporter;
        public DtxWriter(Stream stream, DtxExporter exporter) : base(stream, encoding: Encoding.GetEncoding(932))
        {
            Exporter = exporter;
        }

        public void WriteMetadata(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            WriteLine($"#{key}: {value}");
        }
    }

    static NoteModifiers StereoSticking(HitObject ho)
    {
        // only these cymbals have stereo sticking. A right stick on the hi-hat will still be panned left
        if (ho.Channel == DrumChannel.Crash || ho.Channel == DrumChannel.China || ho.Channel == DrumChannel.Splash)
        {
            var stick = ho.Modifiers & NoteModifiers.LeftRight;
            if (stick != NoteModifiers.None) return stick;
            if (ho.Channel == DrumChannel.Crash) return NoteModifiers.Right;
            return NoteModifiers.Left;
        }
        return NoteModifiers.None;
    }
    double GetVolume(HitObject ho)
    {
        var baseVolume = CurrentBeatmap.GetVolumeMultiplier(VolumeEvents, ho);
        var channel = ho.Channel;
        if (channel == DrumChannel.OpenHiHat || channel == DrumChannel.ClosedHiHat || channel == DrumChannel.Ride)
        {
            var beatStartTick = CurrentBeatmap.BeatStartTickFromTick(ho.Time);
            if (beatStartTick != ho.Time)
            {
                if (HiHatHits.TryGetValue(beatStartTick, out var o) && o == channel)
                    baseVolume *= Config.OffbeatHiHatVolume / 100d;
                else
                {
                    var half = beatStartTick + CurrentBeatmap.TickRate / 2;
                    if (half < ho.Time && HiHatHits.TryGetValue(half, out o) && o == channel)
                        baseVolume *= Config.OffbeatHiHatVolume / 100d;
                }
            }
        }
        return baseVolume;
    }

    WavChipKey SampleKey(HitObject ho) =>
        new(ho.Channel, ho.Data.VelocityModifiers, StereoSticking(ho), GetVolume(ho));

    class BgmObject : ITickTime
    {
        public int Time { get; set; }
    }
    class BgObject : ITickTime
    {
        public int Time { get; set; }
    }
    class EndEventObject : ITickTime { public int Time { get; set; } }

    string DtxChannel(ITickTime ev)
    {
        if (ev is HitObject ho)
        {
            return ho.Channel switch
            {
                DrumChannel.OpenHiHat => "18",
                DrumChannel.ClosedHiHat => "11",
                DrumChannel.Ride => "19",
                DrumChannel.RideBell => "19",
                DrumChannel.Snare => "12",
                DrumChannel.SideStick => "12",
                DrumChannel.SmallTom => "14",
                DrumChannel.MediumTom => "15",
                DrumChannel.LargeTom => "17",
                DrumChannel.BassDrum => ho.Modifiers.HasFlag(NoteModifiers.Left) ? "1C" : "13",
                DrumChannel.HiHatPedal => "1B",
                // This lets us use sticking modifiers in Drum Game to set the cymbal channel
                DrumChannel.Crash or DrumChannel.Splash or DrumChannel.China => StereoSticking(ho) == NoteModifiers.Left ? "1A" : "16",
                DrumChannel.Rim => "12", // snare for now, should probably be able to set this
                _ => throw new NotSupportedException()
            };
        }
        else if (ev is TempoChange)
            return "08";
        else if (ev is BgmObject)
            return "01";
        else if (ev is BgObject)
            return "54";
        else if (ev is EndEventObject) return "61"; // SE1
        else throw new NotImplementedException();
    }

    string ImageOutput;
    void WriteDtx(Beatmap beatmap, Stream stream)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var writer = new DtxWriter(stream, this);

        var tempoChanges = new List<TempoChange>(beatmap.TempoChanges);
        Beatmap.AddExtraDefault(tempoChanges);
        var startingTempo = tempoChanges[0];
        tempoChanges.RemoveAt(0); // don't need to write first tempo change
        var measureChangesBackup = beatmap.MeasureChanges;
        if (Config.ExcludeMeasureChanges)
            beatmap.MeasureChanges = [];

        var measureChanges = new List<MeasureChange>(beatmap.MeasureChanges);
        Beatmap.AddExtraDefault(measureChanges);
        var startingMeasureChange = measureChanges[0];


        writer.WriteLine($"; Created by Drum Game [{Util.VersionString}]");
        writer.WriteLine($"; https://github.com/Jumprocks1/drum-game");
        writer.WriteLine();
        writer.WriteMetadata("TITLE", beatmap.Title);
        writer.WriteMetadata("ARTIST", beatmap.Artist);
        if (!string.IsNullOrWhiteSpace(beatmap.Mapper))
            writer.WriteMetadata("COMMENT", $"Chart by {beatmap.Mapper}");
        writer.WriteMetadata("PREVIEW", "pre.ogg");
        if (!string.IsNullOrWhiteSpace(beatmap.Image))
        {
            var fullPath = beatmap.FullAssetPath(beatmap.Image);
            var outputExtension = Path.GetExtension(beatmap.Image);
            if (string.IsNullOrWhiteSpace(outputExtension) || outputExtension == ".jfif")
                outputExtension = ".jpg";
            ImageOutput = "pre" + outputExtension;
            if (File.Exists(fullPath))
            {
                if (!Output.Exists(ImageOutput))
                {
                    using var os = Output.OpenCreate(ImageOutput);

                    var squareImage = true;

                    using (var file = File.OpenRead(fullPath))
                    using (var image = Image.Load(file))
                    {
                        var format = image.Metadata.DecodedImageFormat;
                        var size = image.Size;
                        squareImage = size.Width == size.Height;
                        if (!squareImage)
                        {
                            Logger.Log($"Squaring image, old size: {size}");
                            var side = Math.Min(size.Width, size.Height);
                            image.Mutate(e => e.Crop(new Rectangle((image.Width - side) / 2, (image.Height - side) / 2, side, side)));
                            if (format.Name == "Webp")
                                format = JpegFormat.Instance;
                            image.Save(os, format);
                        }
                    }
                    if (squareImage)
                        os.Write(File.ReadAllBytes(fullPath));
                }
            }
            writer.WriteMetadata("PREIMAGE", ImageOutput);
        }
        writer.WriteMetadata("BPM", startingTempo.HumanBPM.ToString(CultureInfo.InvariantCulture));
        var level = beatmap.GetDtxLevel();
        if (level == null)
        {
            const string msg = "Please add a dtx-level-xx tag to the map";
            if (osu.Framework.Development.DebugUtils.IsDebugBuild) throw new UserException(msg);
            else Util.Palette.UserError(msg);
        }
        else writer.WriteMetadata("DLEVEL", level);
        writer.WriteLine();

        var bpmChips = new Dictionary<int, string>();
        string DtxChip(ITickTime ev)
        {
            if (ev is TempoChange tc)
                return bpmChips[tc.MicrosecondsPerQuarterNote];
            if (ev is HitObject ho)
                return GetChip(SampleKey(ho)).Id;
            if (ev is BgmObject) return BgmChip.Id;
            if (ev is BgObject) return "01";
            else if (ev is EndEventObject)
                // not sure if there's a better way to do this. Ideally we would just reserve the next sample id
                return "99";
            throw new NotImplementedException();
        }

        var bodyBuilder = new StringBuilder();

        foreach (var tc in tempoChanges)
        {
            if (!bpmChips.ContainsKey(tc.MicrosecondsPerQuarterNote))
            {
                var newChip = base36(bpmChips.Count + 1);
                bpmChips[tc.MicrosecondsPerQuarterNote] = newChip;
                bodyBuilder.AppendLine($"#BPM{newChip}: {tc.HumanBPM}");
            }
        }
        bodyBuilder.AppendLine();

        var startingTicksPerMeasure = (int)Math.Round(startingMeasureChange.Beats * beatmap.TickRate);

        var bgmStartTick = beatmap.TickFromBeatSlow(-(beatmap.StartOffset + ExportOffset) * 1_000 / startingTempo.MicrosecondsPerQuarterNote);
        var bufferMeasures = 0;
        var minimumBufferTicks = beatmap.TickRate / 2; // there must be at least 1 half note before BGM start
        if (bgmStartTick <= minimumBufferTicks) // starting near 0 is bad, Idk why
            bufferMeasures = (startingTicksPerMeasure - bgmStartTick + minimumBufferTicks) / startingTicksPerMeasure;
        var videoStartTick = 0;
        if (Config.IncludeVideo)
        {
            videoStartTick = CurrentBeatmap.TickFromBeatSlow(CurrentBeatmap.BeatFromMilliseconds(-beatmap.YouTubeOffset));
            if (videoStartTick < -bufferMeasures * startingTicksPerMeasure)
                bufferMeasures = (startingTicksPerMeasure - videoStartTick) / startingTicksPerMeasure;
        }

        if (Config.EncodeOffset)
        {
            bgmStartTick = -bufferMeasures * startingTicksPerMeasure;
            // note, MillisecondsFromTick already adds StartOffset
            BgmEncodingOffset = -(beatmap.MillisecondsFromTick(bgmStartTick) + ExportOffset);
        }


        var hitObjects = new List<HitObject>();

        foreach (var ho in beatmap.HitObjects)
        {
            // this upgrades accented crashes to turn into L+R crash and china
            // we don't normally do this in drum game because the notes would overlap
            // TODO this probably has a bug where if we don't use the China at all,
            //    there will be no wav sample for it later
            if (ho.Channel == DrumChannel.Crash && ho.Modifiers == NoteModifiers.Accented)
            {
                hitObjects.Add(ho.With(NoteModifiers.None));
                hitObjects.Add(new HitObject(ho.Time, new HitObjectData(DrumChannel.China)));
            }
            else if (ho is RollHitObject roll)
            {
                var forceStick = ho.Sticking;
                var ticksPerHit = (int)(beatmap.TickRate / Config.RollDivisor);
                var count = roll.Duration / ticksPerHit;
                var startTime = beatmap.MillisecondsFromTick(ho.Time);
                for (var i = 0; i < count; i++)
                {
                    var tickTime = ho.Time + i * ticksPerHit;
                    var sticking = forceStick == NoteModifiers.None ?
                        (i & 1) == 0 ? NoteModifiers.Right : NoteModifiers.Left
                        : forceStick;
                    hitObjects.Add(new HitObject(tickTime, new HitObjectData(ho.Channel, ho.Data.Modifiers | sticking | NoteModifiers.Roll, ho.Data.Preset)));
                }
            }
            else
            {
                hitObjects.Add(ho);
            }
        }

        IEnumerable<ITickTime> events = hitObjects;
        events = events.Append(new BgmObject { Time = bgmStartTick });
        if (Config.IncludeVideo) events = events.Append(new BgObject { Time = videoStartTick });
        events = events.Concat(tempoChanges);
        events = events.Concat(beatmap.MeasureChanges);
        var endEvent = BookmarkEvents.OfType<BookmarkEndEvent>().FirstOrDefault();
        if (endEvent != null)
            events = events.Append(new EndEventObject { Time = beatmap.TickFromBeat(endEvent.Beat) });
        // events are ordered in reverse, so MeasureChanges are inserted first in the DTX file (based on measure)
        var eventQueue = events.OrderByDescending(e => e.Time).ToList();



        while (eventQueue.TryPop(out var ev))
        {
            var measure = beatmap.MeasureFromTickNegative(ev.Time);
            var outputMeasure = measure + bufferMeasures;
            if (outputMeasure < 0) throw new Exception("DTX measure before 0");
            if (ev is MeasureChange mc)
            {
                if (mc.Time == 0) outputMeasure = 0;
                // note this is a special case where we write this early
                writer.WriteLine($"#{outputMeasure:000}02: {mc.Beats / 4}");
                continue;
            }
            var measureStart = beatmap.TickFromMeasureNegative(measure);
            var measureEnd = beatmap.TickFromMeasureNegative(measure + 1);
            var channel = DtxChannel(ev);
            bodyBuilder.Append($"#{outputMeasure:000}{channel}: ");
            if (ev is BgmObject || ev is BgObject) // this only works for 01 as a single item
            {
                // get the offset within ~1ms
                var accuracy = 1000 / (startingMeasureChange.Beats * startingTempo.MicrosecondsPerQuarterNote);
                var fraction = Util.ToFraction((double)(ev.Time - measureStart) / startingTicksPerMeasure, accuracy);
                for (var i = 0; i < fraction.Item2; i++)
                    bodyBuilder.Append(i == fraction.Item1 ? "01" : "00");
            }
            else
            {
                var channelHits = new List<(int, string)> { (ev.Time - measureStart, DtxChip(ev)) };
                var gcd = Util.GCD(measureEnd - measureStart, ev.Time - measureStart);
                for (var i = eventQueue.Count - 1; i >= 0; i--)
                {
                    var e = eventQueue[i];
                    if (e.Time >= measureEnd) break;
                    if (DtxChannel(e) == channel)
                    {
                        eventQueue.RemoveAt(i);
                        channelHits.Add((e.Time - measureStart, DtxChip(e)));
                        gcd = Util.GCD(e.Time - measureStart, gcd);
                    }
                }

                var t = 0;
                for (var i = 0; i < channelHits.Count; i++)
                {
                    var targetT = channelHits[i].Item1 / gcd;
                    if (t > targetT) throw new Exception($"2 notes on the same channel at measure {measure}");
                    while (targetT != t)
                    {
                        bodyBuilder.Append("00");
                        t += 1;
                    }
                    bodyBuilder.Append(channelHits[i].Item2);
                    t += 1;
                }
                var length = (measureEnd - measureStart) / gcd;
                while (t < length)
                {
                    bodyBuilder.Append("00");
                    t += 1;
                }
            }
            bodyBuilder.AppendLine();
        }

        MakeSamples(); // this will load the pan/volume for our samples :)
        foreach (var sample in Samples)
            sample.WriteTo(writer);
        writer.WriteLine();

        if (Config.IncludeVideo)
        {
            writer.WriteLine("#AVI01: bg.mp4");
            writer.WriteLine();
        }

        writer.Write(bodyBuilder);
        beatmap.MeasureChanges = measureChangesBackup;
    }
    class SampleInfo
    {
        public double Volume;
        public double Pan;
        public double EncodingOffset;
    }

    HashSet<string> ArchivedFiles = new();

    void Archive(string file) // adds to (and opens if needed) an output zip file
    {
        if (Config.OutputFormat == OutputFormat.Zip)
        {
            if (ArchivedFiles.Add(file) && Output.Exists(file))
            {
                using var zipStream = OutputArchive.CreateEntry(Config.ExportName + "/" + file).Open();
                using var fileStream = Output.Open(file);
                fileStream.CopyTo(zipStream);
            }
        }
    }


    ZipArchive _outputArchive;
    ZipArchive OutputArchive
    {
        get
        {
            if (_outputArchive == null)
            {
                var zipFolderName = Config.ExportName ?? $"{TargetBeatmap.Artist} - {TargetBeatmap.Title}";
                var zipName = zipFolderName + ".zip";
                var file = Output.OpenCreate(zipName);
                _outputArchive = new ZipArchive(file, ZipArchiveMode.Create);
            }
            return _outputArchive;
        }
    }

    public void ExportSingleMap(Beatmap beatmap)
    {
        this.CurrentBeatmap = beatmap;
        BookmarkEvents = BookmarkEvent.CreateList(CurrentBeatmap);
        VolumeEvents = BookmarkEvents.OfType<BookmarkVolumeEvent>().AsArray();
        HiHatHits = new();
        foreach (var ho in beatmap.HitObjects.Where(e =>
            e.Channel == DrumChannel.OpenHiHat || e.Channel == DrumChannel.ClosedHiHat ||
            e.Channel == DrumChannel.HalfOpenHiHat || e.Channel == DrumChannel.HiHatPedal || e.Channel == DrumChannel.Ride))
        {
            HiHatHits.Add(ho.Time, ho.Channel);
        }
        var dtxOutputName = GetDtxFileName(CurrentBeatmap) + ".dtx";
        MakeBgmChip();
        WriteDtx(CurrentBeatmap, Output.OpenCreate(dtxOutputName));
        EncodeBgmAndPreview();
        SaveChipInfo();

        if (Config.OutputFormat == OutputFormat.Zip)
        {
            try
            {
                Archive(dtxOutputName);
                // we don't archive bgm.ogg since it's in Samples
                Archive("pre.ogg");
                Archive(ImageOutput);
                if (Config.IncludeVideo) Archive("bg.mp4");
                foreach (var filename in Samples.Select(e => e.Filename))
                    Archive(filename); // this removes duplicates, so don't have to worry about L/R crashes being written twice
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to write to zip");
                Util.Palette.ShowMessage("Failed to write to zip file");
            }
        }
    }

    WavChip GetChip(WavChipKey key)
    {
        if (SampleMap.TryGetValue(key, out var k)) return k;

        var midi = key.Channel.MidiNote();
        var computedVelocity = key.ComputedVelocity;
        var chip = new WavChip
        {
            Id = base36(Samples.Count + 1),
            Note = midi,
            Filename = $"{key.Channel}_{midi}_{computedVelocity}.ogg",
            Key = key
        };
        Samples.Add(chip);
        SampleMap[key] = chip;
        return chip;
    }

    WavChip BgmChip;

    void MakeBgmChip()
    {
        if (BgmChip != null) return;
        Samples.Add(BgmChip = new WavChip
        {
            Id = base36(Samples.Count + 1),
            Filename = "bgm.ogg",
            Volume = CurrentBeatmap.CurrentRelativeVolume / 0.3 * 45,
            Key = new(DrumChannel.None, NoteModifiers.None, NoteModifiers.None, 1.0),
            BGM = true
        });
    }

    void EncodeBgmAndPreview()
    {
        var bgmName = "bgm.ogg";
        var bgmPath = Output.BuildPath(bgmName);
        if (File.Exists(bgmPath))
        {
            if (ChipInfo.TryGetValue(bgmName, out var o))
                if (o.EncodingOffset != BgmEncodingOffset)
                    File.Delete(bgmPath);
        }
        if (!File.Exists(bgmPath))
        {
            ChipInfo[bgmName] = new() { EncodingOffset = BgmEncodingOffset };
            // TODO this will allow opus ogg, which I think is okay
            if (Path.GetExtension(CurrentBeatmap.Audio) == ".ogg" && BgmEncodingOffset == 0)
                File.Copy(CurrentBeatmap.FullAudioPath(), bgmPath);
            else
            {
                var process = new FFmpegProcess("converting bgm");
                process.AddInput(CurrentBeatmap.FullAudioPath());
                process.OffsetMs(BgmEncodingOffset);
                process.Vorbis(q: 6);
                process.SimpleAudio();
                process.AddOutput(bgmPath);
                process.Run();
            }
        }

        if (Config.IncludeVideo && CurrentBeatmap.YouTubeID != null)
        {
            try
            {
                var videoPath = Output.BuildPath("bg.mp4");
                if (!File.Exists(videoPath))
                {

                    var url = CurrentBeatmap.YouTubeID;
                    var processInfo = new ProcessStartInfo(YouTubeDL.Executable);
                    processInfo.ArgumentList.Add("-f");
                    processInfo.ArgumentList.Add("136");
                    processInfo.ArgumentList.Add("-o");
                    processInfo.ArgumentList.Add(videoPath);
                    processInfo.RedirectStandardError = true;
                    if (url.Length == 11)
                        url = "youtu.be/" + url; // without this, IDs starting with `-` will fail
                    processInfo.ArgumentList.Add(url);

                    var proc = Process.Start(processInfo);
                    proc.WaitForExit();
                    if (proc.ExitCode != 0)
                    {
                        string error;
                        if (processInfo.RedirectStandardOutput)
                            error = proc.StandardOutput.ReadToEnd();
                        else error = "See console output for error";
                        throw new Exception($"Failed to run {processInfo.FileName} with: {string.Join(", ", proc.StartInfo.ArgumentList)}\n\n\n{error}");
                    }
                }
            }
            catch (Exception e) { Logger.Error(e, "Error while downloading YouTube video"); }
        }

        var previewPath = Output.BuildPath("pre.ogg");
        if (!File.Exists(previewPath))
        {
            var previewTime = CurrentBeatmap.PreviewTime ?? -1;
            var targetTime = previewTime;
            if (previewTime < 0)
            {
                using var track = Util.Resources.GetTrack(CurrentBeatmap.FullAudioPath()) as TrackBass;
                if (track != null)
                {
                    if (track.Length == 0) track.Seek(track.CurrentTime);
                    targetTime = PreviewLoader.DefaultPreviewTime * track.Length;
                }
            }
            var process = new FFmpegProcess("creating preview");
            process.AddArguments("-ss", (Math.Max(0, targetTime) / 1000).ToString(CultureInfo.InvariantCulture), "-t", "15");
            process.AddInput(CurrentBeatmap.FullAudioPath());
            var mult = Math.Clamp(BgmChip.Volume / 100, 0, 1);
            if (mult != 1)
                process.AddArguments("-af", $"volume={mult}, afade=t=out:st=12:d=3");
            else
                process.AddArguments("-af", "afade=t=out:st=12:d=3");
            process.Vorbis();
            process.SimpleAudio();
            process.AddOutput(previewPath);
            process.Run();
        }
    }

    Dictionary<string, SampleInfo> _chipInfo;
    Dictionary<string, SampleInfo> ChipInfo
    {
        get
        {
            if (_chipInfo != null) return _chipInfo;

            Dictionary<string, SampleInfo> res;
            var chipInfoFile = ChipInfoPath;
            if (File.Exists(chipInfoFile))
                res = JsonConvert.DeserializeObject<Dictionary<string, SampleInfo>>(File.ReadAllText(chipInfoFile));
            else
                res = new();
            return _chipInfo = res;
        }
    }
    void SaveChipInfo() => File.WriteAllText(ChipInfoPath, JsonConvert.SerializeObject(ChipInfo));
    string ChipInfoPath => Output.BuildPath("samples.json");
    void MakeSamples()
    {
        using var renderer = BassUtil.HasMidi ? new SoundFontRenderer("soundfonts/main.sf2") : null;
        var hitSampleChips = new List<WavChip>();
        foreach (var sample in Samples)
        {
            var key = sample.Key;
            if (key.Channel == DrumChannel.None) continue; // skip bgm
            hitSampleChips.Add(sample);
            if (ChipInfo.TryGetValue(sample.Filename, out var v))
            {
                sample.Pan = v.Pan;
                sample.Volume = v.Volume;
            }
            var midi = key.Channel.MidiNote();
            var computedVelocity = key.ComputedVelocity;
            var outputPath = Output.BuildPath(sample.Filename);
            renderer?.Render(outputPath, midi, computedVelocity);
        }
        var result = renderer.WaitForResult();
        for (var i = 0; i < result.Count; i++)
        {
            var r = result[i];
            if (r.Rendered)
            {
                var chip = hitSampleChips[i];
                chip.Volume = 100 / r.Boost;
                chip.Pan = r.Pan;
                ChipInfo[chip.Filename] = new SampleInfo { Volume = chip.Volume, Pan = chip.Pan };
            }
            else
            {
                var chip = hitSampleChips[i];
                chip.Volume = ChipInfo[chip.Filename].Volume;
                chip.Pan = ChipInfo[chip.Filename].Pan;
            }
        }

        var boost = 3.8d; // this is just what I decided on
        foreach (var sample in hitSampleChips) sample.Volume *= boost;
    }

    Dictionary<string, string> Filenames = new();

    void MakeSetDef(IReadOnlyList<MapSetEntry> set)
    {
        const string setFile = "SET.def";
        {
            var orderedSet = set.OrderBy(e => Beatmap.GetDtxLevel(e.Metadata.SplitTags()) ?? "50").ToList();
            using var stream = Output.OpenCreate(setFile);
            using var writer = new StreamWriter(stream, Encoding.Unicode);
            writer.WriteLine("#TITLE " + set[0].Metadata.Title);
            writer.WriteLine();
            var names = new HashSet<string>();
            for (var j = orderedSet.Count - 1; j >= 0; j--)
            {
                var filename = GetDtxFileNameOrNull(orderedSet[j].Metadata.SplitTags());
                filename ??= Levels.Last(e => !names.Contains(e));
                if (!names.Add(filename))
                    throw new UserException("Duplicate level name found, please add `dtx-name-ext` (or similar) tag to one of the maps.");
                Filenames[orderedSet[j].Metadata.Id] = filename;
            }

            var i = 1;
            foreach (var diff in orderedSet)
            {
                var filename = Filenames[diff.Metadata.Id];
                writer.WriteLine($"#L{i}LABEL {GetDtxDiffName(filename)}");
                writer.WriteLine($"#L{i}FILE {filename}.dtx");
                writer.WriteLine();
                i += 1;
            }
        }
        Archive(setFile);
    }

    void Export()
    {
        var set = Config.ExportMapSet ? MapStorage.GetMapSet(TargetBeatmap) : [];

        string commonString;
        if (set.Count > 1)
            commonString = GetCommonString(set.Select(e => Path.GetFileNameWithoutExtension(e.MapStoragePath)).ToList());
        else
            commonString = Path.GetFileNameWithoutExtension(TargetBeatmap.MapStoragePath);

        var basename = Util.Resources.GetAbsolutePath(Path.Join("dtx-exports", commonString + "-dtx"));
        Output = new FileProvider(Directory.CreateDirectory(basename));

        if (set.Count > 1)
            MakeSetDef(set);

        var exports = new List<(BeatmapMetadata, Beatmap)>();
        exports.Add((null, TargetBeatmap));
        foreach (var (_, map) in set)
        {
            if (map.Id != TargetBeatmap.Id)
                exports.Add((map, null));
        }

        foreach (var (metadata, map) in exports)
        {
            var beatmap = map ?? Util.MapStorage.LoadMap(metadata);
            ExportSingleMap(beatmap);
        }

        if (_outputArchive != null)
        {
            _outputArchive.Dispose();
            _outputArchive = null;
        }


        if (Config.OpenAfterExport)
            Util.Host.OpenFileExternally(Output.Folder);
    }

    readonly Beatmap TargetBeatmap;
    Beatmap CurrentBeatmap; // current beatmap we are trying to export, useful so we don't have to pass the parameter around
    List<BookmarkEvent> BookmarkEvents;
    readonly ExportConfig Config;
    FileProvider Output;
    DtxExporter(Beatmap beatmap, ExportConfig config)
    {
        TargetBeatmap = beatmap;
        Config = config;
    }
    public static void Export(Beatmap beatmap, ExportConfig config)
    {
        try
        {
            var exporter = new DtxExporter(beatmap, config);
            exporter.Export();
        }
        catch (Exception e)
        {
            Util.Palette.UserError("Failed to export. See log for details.");
            Logger.Error(e, "Failed to export as DTX");
        }
    }
    public static bool Export(CommandContext context, Beatmap beatmap)
    {
        if (beatmap.UseYouTubeOffset)
        {
            Util.Palette.UserError("Cannot export while YouTube audio is loaded");
            return true;
        }
        var badChar = Path.GetInvalidFileNameChars();
        bool isBad(char c) => char.IsWhiteSpace(c) || c == '.' || badChar.Contains(c);
        var z = $"{beatmap.GetRomanArtist()} - {beatmap.GetRomanTitle()}";
        var zipName = new StringBuilder();
        for (var i = 0; i < z.Length; i++)
        {
            if (isBad(z[i]))
            {
                if (i > 0 && isBad(z[i - 1])) continue;
                zipName.Append(' ');
            }
            else zipName.Append(z[i]);
        }
        var hasMapSet = (Util.MapStorage.MapSets[beatmap]?.Count ?? 0) > 1;
        var fields = new FieldBuilder()
            .Add(new BoolFieldConfig { Label = "Build Zip" });

        if (hasMapSet)
            fields.Add(new BoolFieldConfig { Label = "Export Map Set", Key = nameof(ExportConfig.ExportMapSet), DefaultValue = hasMapSet });

        fields
            .Add(new StringFieldConfig { Label = "Ghost Note Width", DefaultValue = "80", Key = nameof(ExportConfig.GhostNoteWidth) })
            .Add(new StringFieldConfig { Label = "Zip Name", DefaultValue = zipName.ToString(), Key = nameof(ExportConfig.ExportName) })
            .Add(new BoolFieldConfig { Label = "Encode Offset", DefaultValue = true, Key = nameof(ExportConfig.EncodeOffset) })
            .Add(new StringFieldConfig { Label = "Offbeat hihat/ride volume", DefaultValue = "92", Key = nameof(ExportConfig.OffbeatHiHatVolume) })
            .Add(new BoolFieldConfig { Label = "Include Video", DefaultValue = beatmap.YouTubeID != null, Key = nameof(ExportConfig.IncludeVideo) });

        var hasRolls = beatmap.HitObjects.Any(e => e.Roll);
        if (hasRolls)
            fields.Add(new StringFieldConfig { Label = "Roll Divisor", DefaultValue = "4", Key = "rollDivisor" });

        var req = context.Palette.Request(new RequestConfig
        {
            Title = "Export to DTX",
            CommitText = "Export",
            Fields = fields.Build(),
            OnCommit = e =>
            {
                var buildZip = e.GetValue<bool>(0);
                var config = new ExportConfig();
                if (buildZip)
                    config.OutputFormat = OutputFormat.Zip;
                if (hasMapSet)
                    config.ExportMapSet = e.GetValue<bool>(nameof(ExportConfig.ExportMapSet));
                config.GhostNoteWidth = int.Parse(e.GetValue<string>(nameof(ExportConfig.GhostNoteWidth)));
                config.ExportName = e.GetValue<string>(nameof(ExportConfig.ExportName));
                config.EncodeOffset = e.GetValue<bool>(nameof(ExportConfig.EncodeOffset));
                config.OffbeatHiHatVolume = int.Parse(e.GetValue<string>(nameof(ExportConfig.OffbeatHiHatVolume)));
                config.IncludeVideo = e.GetValue<bool>(nameof(ExportConfig.IncludeVideo));
                if (hasRolls)
                    config.RollDivisor = double.TryParse(e.GetValue<string>("rollDivisor"), out var o) ? o : 4;
                Export(beatmap, config);
            }
        });
        if (beatmap.PreviewTime == null)
            req.AddWarning("Warning: No preview time set");
        if (beatmap.GetDtxLevel() == null)
            req.AddWarning("Warning: No DTX level tag set");
        if (beatmap.YouTubeOffset == default && !string.IsNullOrWhiteSpace(beatmap.YouTubeID))
            req.AddWarning("Warning: YouTube offset not set");
        return true;
    }

    string GetDtxFileName(Beatmap beatmap) => Filenames.GetValueOrDefault(beatmap.Id) ?? GetDtxFileNameOrNull(beatmap.SplitTags()) ?? Levels[^1];
    static string GetDtxFileNameOrNull(string[] tags)
    {
        foreach (var tag in tags)
            if (tag.StartsWith("dtx-name-"))
                return tag[9..];
        return null;
    }
    static string GetDtxDiffName(string filename) => filename switch
    {
        "bsc" => "BASIC",
        "adv" => "ADVANCED",
        "ext" => "EXTREME",
        _ => "MASTER"
    };
    static readonly string[] Levels = ["bsc", "adv", "ext", "mstr"];
    static string GetCommonString(List<string> s)
    {
        var first = s[0];
        var best = first.Length;
        for (var i = 1; i < s.Count; i++)
        {
            var e = s[i];
            var newBest = 0;
            while (newBest < best && newBest < e.Length)
            {
                if (e[newBest] != first[newBest]) break;
                newBest += 1;
            }
            best = newBest;
        }
        return first[..best];
    }
}

