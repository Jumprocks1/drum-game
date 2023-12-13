using System;
using System.IO;
using ManagedBass;
using ManagedBass.Mix;
using osu.Framework.Audio.Mixing;
using osu.Framework.Audio.Track;
using osu.Framework.Logging;

namespace DrumGame.Game.Utils;

public static class BassUtil
{
    public static double MixerTime(AudioMixer audioMixer = null) // ms
        => ChannelTime(MixerHandle(audioMixer));
    public static double BassMixTime(int channel) // ms
        => Bass.ChannelBytes2Seconds(channel, BassMix.ChannelGetPosition(channel)) * 1000;
    public static double ChannelTime(Track track) => ChannelTime(Util.Get<int>(track, "activeStream"));
    public static double ChannelTime(int channel)
        => Bass.ChannelBytes2Seconds(channel, Bass.ChannelGetPosition(channel)) * 1000;
    public static int MixerHandle(AudioMixer audioMixer = null) => Util.Get<int>(audioMixer ?? Util.DrumGame.Audio.TrackMixer, "Handle");
    public static long MixerPosition(AudioMixer audioMixer = null)
        => Bass.ChannelGetPosition(MixerHandle(audioMixer));
    // I think this has to go on the update thread
    public static bool LoadPlugin(string pluginName)
    {
        try
        {
            var plugin = Bass.PluginLoad(pluginName);
            if (plugin == 0)
                plugin = Bass.PluginLoad(Util.Resources.GetAbsolutePath(Path.Join("lib", pluginName)));
            if (plugin == 0)
            {
                Logger.Log($"Failed to load {pluginName}", level: LogLevel.Error);
                return false;
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Error loading {pluginName} add-on.");
            return false;
        }
        return true;
    }

    static bool? _opus;
    public static bool HasOpus => _opus ??= LoadPlugin("bassopus");
    public static bool OpusChecked => _opus.HasValue;
    public static bool LoadOpus() => HasOpus;

    static bool? _webm;
    public static bool HasWebm => _webm ??= LoadPlugin("basswebm");
    public static bool WebmChecked => _webm.HasValue;
    public static bool LoadWebm() => HasWebm;

    static bool? _midi;
    public static bool HasMidi => _midi ??= LoadPlugin("bassmidi");
}