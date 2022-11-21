import Beatmap from "../utils/Beatmap";

export default abstract class Track {
    abstract get CurrentTime(): number;
    abstract set CurrentTime(value: number);

    protected _currentBeat = 0;

    get CurrentBeat() {
        return this._currentBeat;
    }
    set CurrentBeat(value: number) {
        this.CurrentTime = this.Beatmap.BeatToMs(value)
    }

    Beatmap: Beatmap;
    Duration: number = Number.MAX_VALUE;
    LeadIn: number = 0; // this is usually a positive value
    Playing = false;

    SeekToRatio(ratio: number) {
        this.CurrentTime = (Math.max(Math.min(ratio, 1), 0) * (this.Duration + this.LeadIn) - this.LeadIn)
    }

    RatioAt(time: number) {
        return Math.max(Math.min((time + this.LeadIn) / (this.Duration + this.LeadIn), 1), 0)
    }

    Update() { }

    constructor(beatmap: Beatmap) {
        this.Beatmap = beatmap;
    }
}