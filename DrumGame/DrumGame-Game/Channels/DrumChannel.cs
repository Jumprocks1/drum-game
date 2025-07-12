using DrumGame.Game.Beatmaps.Loaders;
using Newtonsoft.Json;

namespace DrumGame.Game.Channels;

[JsonConverter(typeof(DrumChannelConverter))]
public enum DrumChannel
{
    None = 0,
    Crash = 1,
    OpenHiHat = 2,
    HalfOpenHiHat = 3,
    ClosedHiHat = 4,
    Ride = 5,
    RideBell = 6,
    Snare = 7,
    SideStick = 8,
    SmallTom = 9,
    MediumTom = 10,
    LargeTom = 11,
    BassDrum = 12,
    HiHatPedal = 13,
    Splash = 14,
    China = 15,
    Rim = 16,
    RideCrash = 17,
    // channels below here are not displayed in some places and are subject to index changes
    // TODO we should add L/R crash/china/splash here. Not sure of the best way yet though
    Metronome,
    PracticeMetronome,
    Aux1,
    Aux2,
    Aux3,
    Aux4,
    Aux5,
    MAX_VALUE
}

