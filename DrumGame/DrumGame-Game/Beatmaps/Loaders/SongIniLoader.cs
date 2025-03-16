using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DrumGame.Game.API;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Formats;
using DrumGame.Game.Channels;
using DrumGame.Game.Interfaces;
using DrumGame.Game.IO;
using DrumGame.Game.IO.Midi;
using DrumGame.Game.Media;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Logging;

namespace DrumGame.Game.Beatmaps.Loaders;

// https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.mid/Standard/Drums.md
// https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.mid/Core%20Infrastructure.md#phase-shift-sysex-event-specification

public class SongIniLoader
{
    IFileProvider Provider;
    MapImportContext Context;
    SongIniLoadConfig Config;

    public class SongIniLoadConfig
    {
        public bool MountOnly;
        public bool MetadataOnly;
    }

    SongIniLoader(IFileProvider provider, SongIniLoadConfig config = null)
    {
        Provider = provider;
        Context = MapImportContext.Current; // we want to capture the context since we might end up doing async operations
        Config = config ?? new();
    }


    // this supports opening a subpath in a ZipFileProvider
    public static void ImportSongIni(string path) => ImportSongIni(new FileProvider(Path.GetDirectoryName(path)), Path.GetFileName(path));
    public static void ImportSongIni(IFileProvider provider, string localFileName) => new SongIniLoader(provider).ImportSongIniInternal(localFileName);
    void CleanMap(Beatmap beatmap)
    {
        beatmap.HitObjects = beatmap.HitObjects.OrderBy(e => e.Time).ToList();

        var accentedSnares = 0;
        var regularSnares = 0;

        foreach (var ho in beatmap.HitObjects.Where(e => e.Channel == Channels.DrumChannel.Snare))
        {
            if (ho.Modifiers.HasFlag(NoteModifiers.Accented)) accentedSnares += 1;
            else regularSnares += 1;
        }
        // if over half of the snares are accented, convert them to normal and make normal ones ghost
        if (accentedSnares > regularSnares)
        {
            for (var i = 0; i < beatmap.HitObjects.Count; i++)
            {
                var h = beatmap.HitObjects[i];
                if (h.Channel == Channels.DrumChannel.Snare)
                    beatmap.HitObjects[i] = h.With(h.Modifiers.HasFlag(NoteModifiers.Accented) ? NoteModifiers.None : NoteModifiers.Ghost);
            }
        }

        beatmap.RemoveExtras<TempoChange>();
        beatmap.RemoveExtras<MeasureChange>();
        beatmap.CollapseCrashes(beatmap.TickRate / 32);
        beatmap.HashId();
    }

    public static Beatmap LoadMounted(Stream stream, string fullPath, bool metadataOnly)
    {
        var directory = Path.GetDirectoryName(fullPath);
        var provider = new FileProvider(directory);
        var loader = new SongIniLoader(provider, new SongIniLoadConfig
        {
            MountOnly = true,
            MetadataOnly = metadataOnly
        });
        return loader.ParseSongIniStream(stream);
    }

    void ImportSongIniInternal(string fileName)
    {
        Logger.Log($"Importing {fileName} from {Provider.FolderName}", level: LogLevel.Important);
        using var ini = Provider.Open(fileName);
        ParseSongIniStream(ini);
    }

    public static readonly string[] AudioFormats = [".opus", ".ogg", ".mp3"];

    string FindAudioFile(string basename)
    {
        foreach (var ext in AudioFormats)
        {
            var f = basename + ext;
            if (Provider.Exists(f))
                return f;
        }
        return null;
    }

    struct StemInfo
    {
        public string Name;
        public int MaxIndex;
        public StemInfo(string name, int maxIndex = 0)
        {
            Name = name;
            MaxIndex = maxIndex;
        }
    }

    void FindBgm(Beatmap beatmap)
    {
        // https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/Audio%20Files.md

        if (Config.MetadataOnly && !string.IsNullOrWhiteSpace(beatmap.PreviewAudio))
            return; // don't need BGM if we have a preview and we are only looking for metadata

        if (beatmap.Audio != null) return;

        var stems = new StemInfo[] {
            new("song"),
            new("guitar"),
            new("rhythm"),
            new("bass"),
            new("keys"),
            new("drums", 4),
            new("vocals", 2),
            new("vocals_explicit", 2),
            new("crowd"),
        };
        var found = new List<string>();
        foreach (var stem in stems)
        {
            string f;
            if (stem.MaxIndex > 0)
            {
                var indexFound = false;
                for (var i = 1; i <= stem.MaxIndex; i++)
                {
                    f = FindAudioFile(stem.Name + "_" + i);
                    if (f == null) break;
                    indexFound = true;
                    found.Add(f);
                }
                if (indexFound) continue; // if drums_1.ogg exists, don't look for drums.ogg
            }
            f = FindAudioFile(stem.Name);
            if (f != null)
                found.Add(f);
        }
        // ideally we should mix all tracks in found, but if were mounting, then we just have to use the first one
        if (Config.MountOnly || Config.MetadataOnly || found.Count <= 1)
            beatmap.Audio = found.FirstOrDefault();
        else
        {
            try
            {
                var mergeTarget = "audio/bgm_" + beatmap.MetaHash() + ".ogg";
                var fullMergeTarget = beatmap.FullAssetPath(mergeTarget);
                if (!File.Exists(fullMergeTarget))
                {
                    var merger = new AudioMerger()
                    {
                        InputStreams = found.Select(Provider.Open).ToList(),
                        OutputFile = beatmap.FullAssetPath(mergeTarget)
                    };
                    merger.AmixSync();
                }
                beatmap.Audio = mergeTarget;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to merge audio tracks");
                beatmap.Audio = found.FirstOrDefault();
            }
        }
    }

    Beatmap ParseSongIniStream(Stream stream)
    {
        var map = Beatmap.Create();
        map.HitObjects = new();
        map.TempoChanges = new();
        map.MeasureChanges = new();
        map.RelativeVolume = 0.4;
        map.Tags = Config.MountOnly ? "song-ini-mount" : "song-ini-import";
        if (Config.MountOnly)
            map.PreviewAudio = FindAudioFile("preview");
        if (Provider.Exists("notes.chart"))
            LoadDotChart(map);
        else
            LoadMidi(map);

        using var reader = new StreamReader(stream);
        string line;
        string section = null;
        int? diffDrums = null;
        int? diffDrumsReal = null;
        int? diffDrumsRealPs = null;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith('[')) section = line[1..^1];
            else
            {
                // these have priority over anything set in notes.chart
                var spl = line.Split('=', 2);
                var prop = spl[0].Trim().ToLowerInvariant();
                var v = spl[1].Trim();
                if (prop == "delay")
                    map.StartOffset = double.Parse(v, CultureInfo.InvariantCulture);
                else if (prop == "artist")
                    map.Artist = v;
                else if (prop == "name")
                    map.Title = v;
                else if (prop == "frets")
                    map.Mapper = v;
                else if (prop == "charter")
                    map.Mapper = v;
                else if (prop == "genre")
                    map.AddTags($"genre-{v.Replace(' ', '-')}");
                else if (prop == "diff_drums")
                    diffDrums = int.Parse(v);
                else if (prop == "diff_drums_real")
                    diffDrumsReal = int.Parse(v);
                else if (prop == "diff_drums_real_ps")
                    diffDrumsRealPs = int.Parse(v);
                else if (prop == "preview_start_time")
                    map.PreviewTime = int.Parse(v);
                else if (prop == "song_length")
                    map.PlayableDuration = int.Parse(v);
            }
        }

        var diff = diffDrumsRealPs ?? diffDrumsReal ?? diffDrums;
        if (diff is int d && d >= 0)
        {
            if (d <= 0) d = 1;
            if (d >= 6) d = 6;
            // 1 = Easy, 6 = Expert+
            map.Difficulty = (BeatmapDifficulty)d;
        }

        var folderName = Provider.FolderName;
        if (folderName.StartsWith("7z-"))
            folderName = folderName[3..];
        if (folderName.StartsWith("temp"))
            folderName = folderName[4..];
        if (folderName.StartsWith("-download"))
            folderName = folderName[9..];

        var outputFilename = (folderName + "-" + map.MetaHash()[0..8].ToLowerInvariant()).ToFilename(".bjson");
        // if we aren't mounting, we need a location to save to
        if (!Config.MountOnly)
            map.Source = new BJsonSource(Path.Join(Util.MapStorage.AbsolutePath, outputFilename), BJsonFormat.Instance);
        FindBgm(map);

        if (!Config.MountOnly && !Config.MetadataOnly)
        {
            if (!Util.MapStorage.Exists(map.FullAudioPath()))
            {
                var providerAudio = map.Audio;
                if (Provider.Exists(providerAudio))
                {
                    var audioExt = Path.GetExtension(providerAudio);
                    var audioHash = "_" + Util.MD5(Provider.Open(providerAudio))[0..8].ToLowerInvariant();
                    var audioName = $"{folderName}-{Path.GetFileNameWithoutExtension(providerAudio)}".ToFilename();
                    map.Audio = $"audio/{audioName}{audioHash}{audioExt}";
                    Provider.Copy(providerAudio, map.FullAudioPath());
                }
            }
        }

        var image = "album.png";
        if (!Provider.Exists(image))
            image = "album.jpg";
        if (Config.MountOnly)
        {
            map.Image = image;
        }
        else
        {
            try
            {
                var ext = Path.GetExtension(image);
                var hash = Util.MD5(Provider.Open(image)).ToLowerInvariant();
                var newImagePath = "images/" + hash + ext;
                var fullNewPath = Util.MapStorage.GetFullPath(newImagePath);
                if (!File.Exists(fullNewPath))
                    Provider.Copy(image, fullNewPath);
                map.Image = newImagePath;
            }
            catch (Exception e) { Logger.Error(e, "Error loading image"); }
        }

        CleanMap(map);
        if (!Config.MountOnly)
        {
            map.Export();
            map.SaveToDisk(Util.MapStorage, Context);
        }
        return map;
    }

    void LoadDotChart(Beatmap o)
    {
        using var chartReader = new DotChartReader(Provider.Open("notes.chart"));
        foreach (var section in chartReader)
        {
            if (section.Name == "Song")
            {
                foreach (var (Key, Value) in section.Values)
                {
                    if (Key == "Name") o.Title = Value;
                    else if (Key == "Artist") o.Artist = Value;
                    else if (Key == "Charter") o.Mapper = Value;
                    else if (Key == "Offset") o.StartOffset = double.Parse(Value, CultureInfo.InvariantCulture);
                    else if (Key == "Resolution") o.TickRate = int.Parse(Value);
                    else if (Key == "Difficulty") o.AddTags($"difficulty-{Value}");
                    else if (Key == "PreviewStart") o.PreviewTime = double.Parse(Value, CultureInfo.InvariantCulture);
                    else if (Key == "Genre") o.AddTags($"genre-{Value.Replace(' ', '-')}");
                    else if (Key == "MusicStream") o.Audio = Value;
                }
            }
            else if (section.Name == "SyncTrack")
            {
                foreach (var (Key, Value) in section.Values)
                {
                    var spl = Value.Split(' ');
                    var t = int.Parse(Key);
                    if (spl[0] == "TS")
                    {
                        var num = int.Parse(spl[1]);
                        var denom = 1 << (spl.Length > 2 ? int.Parse(spl[2]) : 2);
                        var bpMeasure = (double)(num * 4) / denom;
                        o.UpdateChangePoint<MeasureChange>(t, t => t.WithBeats(bpMeasure));
                    }
                    else if (spl[0] == "B")
                    {
                        var bpm = (double)int.Parse(spl[1]) / 1000;
                        o.UpdateChangePoint<TempoChange>(t, t => t.WithTempo(Tempo.MicrosecondsFromBPM(bpm)));
                    }
                    else if (spl[0] == "S") { } // ignore
                    else Logger.Log($"Unrecognized SyncTrack event: {Key} = {Value}", level: LogLevel.Error);
                }
            }
            else if (section.Name == "Events") { }// ignore
            else if (section.Name == "ExpertDrums")
            {
                // https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.chart/Drums.md#note-and-modifier-types
                var modifiers = new bool[35];
                var grouped = section.Values.GroupBy(e => e.Key);
                // we have to do this since the accents/modifiers are not always in order...
                foreach (var group in grouped)
                {
                    Array.Fill(modifiers, false);
                    foreach (var (Key, Value) in group.OrderByDescending(e => e.Value))
                    {
                        var t = int.Parse(Key);
                        var spl = Value.Split(' ');
                        if (spl[0] == "N")
                        {
                            var note = int.Parse(spl[1]);
                            if ((note >= 34 && note <= 38) || (note >= 40 && note <= 44) || (note >= 66 && note <= 68)) modifiers[note - 34] = true;
                            else
                            {
                                var data = SongIniMapping.DotChartMapping(note, modifiers);
                                if (data.Channel == DrumChannel.None)
                                    Logger.Log($"Unrecognized event: {Key} = {Value}", level: LogLevel.Error);
                                else
                                {
                                    o.HitObjects.Add(new HitObject(t, data));
                                }
                            }
                        }
                        else Logger.Log($"Unrecognized {section.Name} event: {Key} = {Value}", level: LogLevel.Error);
                    }
                }
            }
            else Logger.Log($"Unrecognized section: {section.Name}", level: LogLevel.Error);
        }
    }

    public enum TrackType
    {
        None,
        RealDrumsPS,
        Drums
    }

    // https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.mid/Core%20Infrastructure.md
    void LoadMidi(Beatmap o)
    {
        using var midiStream = Provider.Open("notes.mid");

        using var reader = new MidiReader(midiStream);

        var midi = reader.ReadFile();

        var tickRate = midi.Header.quarterNote;
        o.TickRate = tickRate;
        var drumTracks = new string[] { "PART REAL_DRUMS_PS", "PART DRUMS" };
        var targetTrackName = drumTracks.FirstOrDefault(name => midi.Tracks.Any(e => e.Name == name));
        var trackType = TrackType.None;
        if (targetTrackName == drumTracks[0])
            trackType = TrackType.RealDrumsPS;
        else if (targetTrackName == drumTracks[1])
            trackType = TrackType.Drums;

        if (targetTrackName == null)
            Logger.Log($"Missing drum track track, found: {string.Join(", ", midi.Tracks.Select(e => e.Name))}", level: LogLevel.Error);
        var knownDiffs = new HashSet<int> {
            -2 // ignore
            // -1 is for all difficulties
            // 3 is for Expert+
        };
        var noteQueue = new List<byte>();
        foreach (var track in midi.Tracks)
        {
            // Console.WriteLine($"track: {track.Name}");
            var t = 0;
            const int difficultyCount = 4;
            var phrases = new bool[difficultyCount, 0x13 + 1];
            // https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.mid/Standard/Drums.md#track-notes
            var markers = new bool[128];

            // whenever the time changes, we clear queue
            void handleQueue() // call before changing t
            {
                foreach (var note in noteQueue)
                {
                    var data = SongIniMapping.ToHitObjectData(note, default, phrases, markers, trackType);
                    if (data.Channel == DrumChannel.None)
                    {
                        var phr = new StringBuilder();
                        for (var i = 0; i <= 0x13; i++)
                        {
                            if (phrases[3, i])
                                phr.Append($"{i:X2}");
                        }
                        Console.WriteLine($"No channel: t {(double)t / o.TickRate} n {note} {phr}");
                    }
                    else o.HitObjects.Add(new HitObject(t, data));
                }
                noteQueue.Clear();
            }
            foreach (var ev in track.events)
            {
                if (ev.time != t)
                {
                    handleQueue();
                    t = ev.time;
                }
                if (ev is MidiTrack.MidiEvent me)
                {
                    var midiNote = me.parameter1;
                    if (track.Name != targetTrackName) continue;
                    if (me.type == 9 && me.parameter2 > 0) // note on event
                    {
                        // https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.mid/Standard/Drums.md#track-notes
                        // https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.mid/Miscellaneous/Rock%20Band/Drums.md
                        if ((midiNote >= 24 && midiNote <= 51) || (midiNote >= 103 && midiNote <= 127))
                        {
                            markers[midiNote] = true;
                            continue;
                        }
                        // not sure why I skip these 2
                        if (midiNote == 12 || midiNote == 13) continue;
                        var diff = SongIniMapping.Difficulty(midiNote);
                        if (diff != 3 && diff != -1)
                        {
                            if (knownDiffs.Add(diff))
                                Logger.Log($"Unrecognized difficulty: {diff}, midi: {midiNote}", level: LogLevel.Error);
                            continue;
                        }
                        noteQueue.Add(midiNote); // velocity not used
                    }
                    // velocity 0 is equivalent to note off
                    else if (me.type == 8 || (me.type == 9 && me.parameter2 == 0)) // note off
                    {
                        if ((midiNote >= 24 && midiNote <= 51) || (midiNote >= 103 && midiNote <= 127))
                            markers[midiNote] = false;
                    }
                }
                else if (ev is MidiTrack.SysExEvent mes)
                {
                    if (mes.bytes[0] == 'P' && mes.bytes[1] == 'S' && mes.bytes[2] == 0 && mes.bytes[3] == 0)
                    {
                        if (mes.bytes[4] == 0xFF) // all difficulties
                            for (var i = 0; i < difficultyCount; i++)
                                phrases[i, mes.bytes[5]] = mes.bytes[6] == 1;
                        else
                            phrases[mes.bytes[4], mes.bytes[5]] = mes.bytes[6] == 1;
                    }
                    else Console.WriteLine($"SysEx event: {t} {BitConverter.ToString(mes.bytes)}");
                }
                else if (ev is MidiTrack.TempoEvent te)
                {
                    o.UpdateChangePoint<TempoChange>(t, t => t.WithTempo(te.MicrosecondsPerQuarterNote));
                }
                else if (ev is MidiTrack.TimingEvent ts)
                {
                    var bpMeasure = (double)(ts.Numerator * 4) / ts.Denominator;
                    o.UpdateChangePoint<MeasureChange>(t, t => t.WithBeats(bpMeasure));
                }
            }
            handleQueue();
        }
    }
}