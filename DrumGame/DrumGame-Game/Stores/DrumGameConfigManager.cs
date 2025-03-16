using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Browsers;
using DrumGame.Game.Input;
using DrumGame.Game.Utils;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace DrumGame.Game.Stores;

// seems like this saves unecessarily unforunately
// bindables change when they're parsed and it saves those changes right away, which isn't ideal
// I think it also saves on exit
public class DrumGameConfigManager : IniConfigManager<DrumGameSetting>
{
    internal const string FILENAME = @"drumgame.ini";

    protected override string Filename => FILENAME;

    public readonly Storage Storage;

    public void RevealInFileExplorer() => Storage.PresentFileExternally(FILENAME);
    public void OpenExternally() => Storage.OpenFileExternally(FILENAME);

    public Bindable<double> MidiInputOffset;
    public Bindable<int> MidiThreshold;
    public Bindable<double> KeyboardInputOffset;
    public Bindable<double> MidiOutputOffset;
    public BindableNumber<double> SampleVolume;
    public BindableNumber<double> SoundfontVolume;
    public BindableChannelEquivalents ChannelEquivalents;
    public BindableMidiMapping MidiMapping;
    public Bindable<SortMethod> ReplaySort;
    public Bindable<string> FileSystemResources;
    public BindableJson<MapLibraries> MapLibraries;
    public BindableJson<Beatmaps.Practice.PracticeMode.PracticeConfig> PracticeConfig;
    public Bindable<string> FFmpegLocation;
    public Bindable<double> MinimumLeadIn; // in seconds
    public Bindable<bool> PlaySamplesFromMidi;
    public Bindable<bool> DiscordRichPresence;
    public Bindable<bool> PreservePitch;
    public Bindable<(byte, byte)> HiHatRange;
    public ParsableBindable<KeyboardMapping> KeyboardMapping;
    public Bindable<DisplayPreference> DisplayMode;

    protected override void InitialiseDefaults()
    {
        SetDefault(DrumGameSetting.MasterVolume, 1.0, 0.0, 1.0, 0.01);
        SetDefault(DrumGameSetting.MasterMuted, false);
        SetDefault(DrumGameSetting.TrackVolume, 1.0, 0.0, 1.0, 0.01);
        SetDefault(DrumGameSetting.TrackMuted, false);
        SampleVolume = SetDefault(DrumGameSetting.SampleVolume, 1.0, 0.0, 1.0);
        SetDefault(DrumGameSetting.SampleMuted, false);
        SetDefault(DrumGameSetting.MetronomeVolume, 1.0, 0.0, 1.0, 0.01);
        SetDefault(DrumGameSetting.MetronomeMuted, true);
        SetDefault(DrumGameSetting.HitVolume, 1.0, 0.0, 1.0, 0.01);
        SetDefault(DrumGameSetting.HitMuted, false);
        SoundfontVolume = SetDefault(DrumGameSetting.SoundfontVolume, 1.5, 0.0, 3);
        SetDefault(DrumGameSetting.SoundfontUseMidiVelocity, false);
        SetDefault(DrumGameSetting.PlaySoundfontOutsideMaps, true);
        SetDefault(DrumGameSetting.SaveFullReplayData, false);
        SetDefault(DrumGameSetting.BeatmapSearch, "");
        AddBindable(DrumGameSetting.CurrentCollection, new BindableNullString());
        DisplayMode = SetDefault(DrumGameSetting.BeatmapDisplayMode, DisplayPreference.Notation);
        MidiOutputOffset = SetDefault(DrumGameSetting.MidiOutputOffset, 5.0);
        // negative offset since the hit samples are played after we recieve the press
        // should be pretty similar to DrumsetAudioPlayer.WavOffset
        KeyboardInputOffset = SetDefault(DrumGameSetting.KeyboardInputOffset, -20.0);
        MidiInputOffset = SetDefault(DrumGameSetting.MidiInputOffset, 15.0);
        AddBindable(DrumGameSetting.ChannelEquivalents, ChannelEquivalents = new BindableChannelEquivalents(Beatmaps.Scoring.ChannelEquivalents.Default));
        AddBindable(DrumGameSetting.MidiMapping, MidiMapping = new BindableMidiMapping());
        AddBindable(DrumGameSetting.SyncTarget, new Bindable<string>(""));
        ReplaySort = SetDefault(DrumGameSetting.ReplaySort, SortMethod.Score);
        FileSystemResources = SetDefault<string>(DrumGameSetting.FileSystemResources, null);
        FFmpegLocation = SetDefault<string>(DrumGameSetting.FFmpegLocation, null);
        SetDefault(DrumGameSetting.MinimumLeadIn, 1d, 0d);
        AddBindable(DrumGameSetting.HiHatRange, HiHatRange = new BindableRange((255, 255)));
        AddBindable(DrumGameSetting.KeyboardMapping, KeyboardMapping = new ParsableBindable<KeyboardMapping>());
        KeyboardMapping.Value ??= new(); // doesn't get parsed on a fresh config load
        SetDefault(DrumGameSetting.WatchImportFolder, false);
        SetDefault(DrumGameSetting.PreferVorbisAudio, false);
        SetDefault<string>(DrumGameSetting.Skin, null);
        PlaySamplesFromMidi = SetDefault(DrumGameSetting.PlaySamplesFromMidi, false);
        SetDefault<string>(DrumGameSetting.MinimumDtxLevel, null);
        AddBindable(DrumGameSetting.MapLibraries, MapLibraries = new BindableJson<MapLibraries>());
        AddBindable(DrumGameSetting.PracticeConfig, PracticeConfig = new BindableJson<Beatmaps.Practice.PracticeMode.PracticeConfig>());
        DiscordRichPresence = SetDefault<bool>(DrumGameSetting.DiscordRichPresence, false);
        MidiThreshold = SetDefault(DrumGameSetting.MidiThreshold, 0, -1, 127);
        PreservePitch = SetDefault(DrumGameSetting.PreservePitch, true);
        SetDefault(DrumGameSetting.AutoLoadVideo, true);
        SetDefault(DrumGameSetting.PreferredMidiInput, "");
        SetDefault(DrumGameSetting.PreferredMidiOutput, "");
        SetDefault(DrumGameSetting.Modifiers, "");
        SetDefault(DrumGameSetting.HitWindowPreference, HitWindowPreference.Standard);
        SetDefault(DrumGameSetting.CustomHitWindows, HitWindows.DefaultCustomHitWindowString);
        SetDefault(DrumGameSetting.Version, "0.0.0");
    }

    public static int[] ParseVersion(string version) => version.Split('.').Select(e => int.TryParse(e, out var o) ? o : -1).ToArray();
    public static int CompareVersions(int[] a, int[] b)
    {
        var len = Math.Max(a.Length, b.Length);
        for (var i = 0; i < len; i++)
        {
            if (a.Length <= i) return -1;
            if (b.Length <= i) return 1;
            if (a[i] < b[i]) return -1;
            if (a[i] > b[i]) return 1;
        }
        return 0;
    }

    public void Migrate()
    {
        // doesn't really have to be current version, should just be the first version after the last migration
        const string latest = "0.9.0";
        // if the .ini file doesn't exist, we don't do any migrations
        if (!fileFound)
        {
            SetValue(DrumGameSetting.Version, latest);
            return;
        }
        var version = Get<string>(DrumGameSetting.Version);
        if (version == latest) return;
        var currentVersionParsed = ParseVersion(version);
        bool check(string migration)
        {
            var res = CompareVersions(currentVersionParsed, ParseVersion(migration)) == -1;
            if (res) Logger.Log($"Performing {migration} migration", level: LogLevel.Important);
            return res;
        }

        if (check("0.9.0"))
        {
            // perform 0.9.0 migration
        }

        SetValue(DrumGameSetting.Version, latest);
        Logger.Log($"Migrated config to {latest}", level: LogLevel.Important);
    }

    bool fileFound; // used for checking if migration is needed

    void innerLoad()
    {
        // base.PerformLoad();

        // Mostly copy pasted from base.PerformLoad()
        // I just wanted to print out the lines that fail to parse
        if (string.IsNullOrEmpty(Filename)) return;

        // we can't use Storage yet since it's not initialized.....super sad
        using (var stream = Util.Host.Storage.GetStream(Filename))
        {
            if (stream == null)
                return;
            fileFound = true;

            using (var reader = new StreamReader(stream))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    var equalsIndex = line.IndexOf('=');

                    if (line.Length == 0 || line[0] == '#' || equalsIndex < 0) continue;

                    var key = line.AsSpan(0, equalsIndex).Trim().ToString();
                    var val = line.AsSpan(equalsIndex + 1).Trim().ToString();

                    if (!Enum.TryParse(key, out DrumGameSetting lookup))
                    {
                        Logger.Log($"Failed to load config line: {line}", level: LogLevel.Important);
                        continue;
                    }

                    if (ConfigStore.TryGetValue(lookup, out var b))
                    {
                        try
                        {
                            if (!(b is IParseable parseable))
                                throw new InvalidOperationException($"Bindable type {b.GetType().ReadableName()} is not {nameof(IParseable)}.");

                            parseable.Parse(val, CultureInfo.InvariantCulture);
                        }
                        catch (Exception e)
                        {
                            Logger.Log($@"Unable to parse config key {lookup}: {e}", LoggingTarget.Runtime, LogLevel.Important);
                        }
                    }
                    else if (AddMissingEntries)
                        SetDefault(lookup, val);
                }
            }
        }
    }

    protected override void PerformLoad()
    {
        innerLoad();
        AfterLoad();
        try
        {
            Migrate();
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error occured while performing config migration");
        }
    }

    protected void AfterLoad()
    {
        // we can set some extra values if they were not loaded 100% here
        MidiMapping.Value ??= new MidiMapping(null);
        MapLibraries.Value ??= new MapLibraries();
        PracticeConfig.Value ??= new();
    }

    public DrumGameConfigManager(Storage storage, IDictionary<DrumGameSetting, object> defaultOverrides = null)
        : base(storage, defaultOverrides)
    {
        Storage = storage;
        Util.ConfigManager = this;
    }

    // Idk what create tracked settings is for
}

public enum SortMethod
{
    Score,
    Time,
    Accuracy,
    Misses,
    MaxCombo
}

public enum DrumGameSetting
{
    MasterVolume,
    MasterMuted,
    TrackVolume,
    TrackMuted,
    SampleVolume,
    SampleMuted,
    MetronomeVolume,
    MetronomeMuted,
    HitVolume,
    HitMuted,
    SaveFullReplayData,
    MidiInputOffset,
    KeyboardInputOffset,
    MidiOutputOffset,
    BeatmapSearch,
    CurrentCollection,
    ChannelEquivalents,
    MidiMapping,
    SyncTarget,
    ReplaySort,
    FileSystemResources,
    FFmpegLocation,
    HiHatRange,
    KeyboardMapping,
    PlaySamplesFromMidi,
    BeatmapDisplayMode,
    WatchImportFolder,
    PreferVorbisAudio,
    Skin,
    MinimumDtxLevel,
    MapLibraries,
    PracticeConfig,
    DiscordRichPresence,
    MidiThreshold,
    MinimumLeadIn,
    PreservePitch,
    AutoLoadVideo,
    PreferredMidiInput,
    PreferredMidiOutput,
    Modifiers,
    HitWindowPreference,
    CustomHitWindows,
    SoundfontVolume,
    PlaySoundfontOutsideMaps,
    SoundfontUseMidiVelocity,
    Version
}

