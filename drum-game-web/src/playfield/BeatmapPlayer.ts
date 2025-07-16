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

    AllowYouTube = true;

    constructor(map: BJson) {
        super();
        this.BJson = map;
        this.Beatmap = new Beatmap(map);
        const youTubeId = this.AllowYouTube && this.Beatmap.BJson.youTubeID;
        if (youTubeId) {
            if (this.BJson.youTubeOffset)
                this.BJson.offset += this.BJson.youTubeOffset;
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
        this.Track.Dispose();
    }

    OnEscape = () => this.FindParent(Router).NavigateBack({ page: MapSelectorPage })

    OnKeyDown = (e: KeyboardEvent) => {
        if (e.key === "Escape") {
            this.OnEscape()
        } else if (e.key === " ") {
            this.Track.Playing = !this.Track.Playing
        } else if (e.key === "ArrowRight") {
            this.Track.CurrentBeat += 4;
        } else if (e.key === "ArrowLeft") {
            this.Track.CurrentBeat -= 4;
        } else if (e.key === "ArrowUp") {
            this.Track.CurrentBeat += 8;
        } else if (e.key === "ArrowDown") {
            this.Track.CurrentBeat -= 8;
        }
        return true;
    }
}