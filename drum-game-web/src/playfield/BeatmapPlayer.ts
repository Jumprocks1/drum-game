import { BJson } from "../interfaces/BJson";
import NoDOMComponent from "../framework/NoDOMComponent";
import BeatmapLoaderPage from "../pages/BeatmapLoaderPage";
import NotationDisplay from "./notation/NotationDisplay";
import Beatmap from "../utils/Beatmap";

export default class BeatmapPlayer extends NoDOMComponent {
    BJson: BJson
    Beatmap: Beatmap

    Duration = Number.POSITIVE_INFINITY;

    private startTime = new Date().getTime();
    Playing = true;
    private lastUpdate = this.startTime;
    private _currentTime = 0;
    private _currentBeat = 0;

    get CurrentBeat() {
        return this._currentBeat;
    }

    get CurrentTime() {
        return this._currentTime
    }

    set CurrentTime(value: number) {
        this._currentTime = value;
        this._currentBeat = this.Beatmap.MsToBeat(this._currentTime);
    }

    Update() {
        if (this.Playing)
            this.CurrentTime += (new Date().getTime() - this.lastUpdate)
        this.lastUpdate = new Date().getTime();
    }


    constructor(map: BJson) {
        super();
        this.BJson = map;
        this.Beatmap = new Beatmap(map);
        this.Duration = this.Beatmap.BeatToMs(this.Beatmap.Length);
    }

    AfterParent() {
        super.AfterParent();
        const display = new NotationDisplay(this);
        this.Add(display);
    }

    AfterRemove() {
        super.AfterRemove();
        this.FindChild(NotationDisplay).AfterRemove();
    }

    OnKeyDown = (e: KeyboardEvent) => {
        if (e.key === "Escape") {
            this.FindParent(BeatmapLoaderPage).ShowSelector();
        } else if (e.key === " ") {
            this.Playing = !this.Playing
        }
        return true;
    }
}