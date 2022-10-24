import { BJson } from "../interfaces/BJson";
import NoDOMComponent from "../framework/NoDOMComponent";
import MapSelectorPage from "../pages/MapSelectorPage";
import NotationDisplay from "./notation/NotationDisplay";
import Beatmap from "../utils/Beatmap";
import Router from "../framework/Router";
import ClockTrack from "./ClockTrack";

export default class BeatmapPlayer extends NoDOMComponent {
    BJson: BJson
    Beatmap: Beatmap

    Track: ClockTrack

    constructor(map: BJson) {
        super();
        this.BJson = map;
        this.Beatmap = new Beatmap(map);
        this.Track = new ClockTrack(this.Beatmap);
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
            this.FindParent(Router).NavigateTo(MapSelectorPage); // TODO we need to be able to tell this where to go
        } else if (e.key === " ") {
            this.Track.Playing = !this.Track.Playing
        }
        return true;
    }
}