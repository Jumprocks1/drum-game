using DrumGame.Game.Beatmaps.Loaders;
using Newtonsoft.Json;

namespace DrumGame.Game.Channels;

[JsonConverter(typeof(DrumChannelConverter))]
public enum DrumChannel
{
    None,
    Crash,
    OpenHiHat,
    HalfOpenHiHat,
    ClosedHiHat,
    Ride,
    RideBell,
    Snare,
    SideStick,
    SmallTom,
    MediumTom,
    LargeTom,
    BassDrum,
    HiHatPedal,
    Splash,
    China,
    Rim,
    Metronome,
    Aux1,
    Aux2,
    Aux3,
    Aux4,
    Aux5,
}

