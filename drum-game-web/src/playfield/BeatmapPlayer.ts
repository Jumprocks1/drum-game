import { BJson } from "../interfaces/BJson";
import NoDOMComponent from "../framework/NoDOMComponent";
import MapSelectorPage from "../pages/MapSelectorPage";
import NotationDisplay from "./notation/NotationDisplay";
import Beatmap from "../utils/Beatmap";
import Router from "../framework/Router";
import ClockTrack from "./ClockTrack";
import YouTubeTrack from "./YouTubeTrack";
import Track from "./Track";

export default class BeatmapPlayer extends NoDOMComponent {
    BJson: BJson
    Beatmap: Beatmap

    Track: Track

    constructor(map: BJson) {
        super();
        this.BJson = map;
        this.Beatmap = new Beatmap(map);
        const youTubeId = this.Beatmap.BJson.youTubeID;
        if (youTubeId) {
            this.Track = new YouTubeTrack(this.Beatmap);
        } else {
            this.Track = new ClockTrack(this.Beatmap);
        }
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
            this.FindParent(Router).NavigateBack({ page: MapSelectorPage });
        } else if (e.key === " ") {
            this.Track.Playing = !this.Track.Playing
        }
        return true;
    }
}