import DrumGameWebSocket from "../api/DrumGameWebSocket";
import Component from "../framework/Component";
import GlobalData from "../GlobalData";
import type Cache from "../interfaces/Cache";
import Root from "../Root";
import BeatmapCard from "./BeatmapCard";

export default class MapSelector extends Component {
    constructor() {
        super();

        const div = <div id="map-selector" />;

        this.DOMNode = div;

        GlobalData.LoadMapList().then(maps => {
            if (!this.Alive) return;
            for (const key in maps.Maps)
                this.Add(new BeatmapCard(maps.Maps[key]))
        })
    }

    Focus(card: BeatmapCard) {
        card.HTMLElement.scrollIntoView();
    }
}