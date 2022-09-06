import { DrumChannel } from "../interfaces/BJson";

const o: {
    ChannelNoteMapping: { [K in DrumChannel]: [number, string] }
    CodepointMap: { [K in string]: string }
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
    },
    CodepointMap: {
        "\uE0A4": "noteheadBlack",
        "\uE0A9": "noteheadXBlack",
        "\uE0B3": "noteheadCircleX",
        "\uE0E8": "noteheadCircledBlackLarge",
        "\uE0AA": "noteheadXOrnate",
        "\uE0AB": "noteheadXOrnateEllipse",
        "\uE0DD": "noteheadDiamondWhite",
        "\uE0DB": "noteheadDiamondBlack",
        "\uE240": "flag8thUp",
        "\uE241": "flag8thDown",
        "\uE242": "flag16thUp",
        "\uE243": "flag16thDown",
        "\uE1E7": "augmentationDot",
        "\uE4A0": "articAccentAbove",
        "\uE4A1": "articAccentBelow",
        "\uE0CE": "noteheadParenthesis",
    }
}

export default o;