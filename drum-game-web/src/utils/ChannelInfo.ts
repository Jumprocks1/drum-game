import { DrumChannel } from "../interfaces/BJson";

const o: {
    ChannelNoteMapping: { [K in DrumChannel]: [number, string] }
} = {
    // could improve performance by using an array with numeric indexes
    ChannelNoteMapping: {
        "hihat-pedal": [4.5, "\uE0A9"],
        "crash": [-1, "\uE0AA"],
        "splash": [-1, "\uE0DD"],
        "china": [-1, "\uE0AB"],
        "open-hihat": [-0.5, "\uE0B3"],
        "half-hihat": [-0.5, "\uE0B3"],
        "hihat": [-0.5, "\uE0A9"],
        "ride": [0, "\uE0A9"],
        "ride-bell": [0, "\uE0DB"],
        "snare": [1.5, "\uE0A4"],
        "rim": [0.5, "\uE0A9"],
        "sidestick": [1.5, "\uE0E8"],
        "high-tom": [0.5, "\uE0A4"],
        "mid-tom": [1, "\uE0A4"],
        "low-tom": [2.5, "\uE0A4"],
        "bass": [3.5, "\uE0A4"],
    }
}

export default o;