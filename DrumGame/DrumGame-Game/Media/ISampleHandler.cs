using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using DrumGame.Game.Midi;

namespace DrumGame.Game.Media;

public interface ISampleHandler
{
    /// <summary>
    /// Estimated latency of this output method.
    /// This value will be subtracted from the delay when Prefire is available.
    /// When not available, this will be added to the track time of events triggered with this output method.
    /// Only needed when native BASS playback isn't available (MIDI output)
    /// </summary>
    public double Latency { get; }
    // if Play() doesn't call native bass methods, we have to subtract latency
    // for example, external MIDI devices will always have latency and there's nothing we can do about it
    public bool BassNative { get; }
    public void Play(DrumChannelEvent e);
}

public interface ISampleHandlerMixDelay
{
    public void Play(DrumChannelEvent e, double delaySeconds);
}