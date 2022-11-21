import Beatmap from "../utils/Beatmap";
import Track from "./Track";

export default class ClockTrack extends Track {
    private startTime = new Date().getTime();
    private lastUpdate = this.startTime;


    private _currentTime = 0;

    constructor(beatmap: Beatmap) {
        super(beatmap);
        this.Duration = this.Beatmap.BeatToMs(this.Beatmap.Length);
        this.LeadIn = this.Beatmap.BJson.leadIn ?? 0;
        this.CurrentTime = -this.LeadIn
    }

    Update() {
        if (this.Playing)
            this.CurrentTime += (new Date().getTime() - this.lastUpdate)
        this.lastUpdate = new Date().getTime();
    }

    set CurrentTime(value: number) {
        this._currentTime = value;
        this._currentBeat = this.Beatmap.MsToBeat(this._currentTime);
    }

    get CurrentTime() {
        return this._currentTime
    }
}