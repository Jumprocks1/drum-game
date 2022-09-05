import Component from "../framework/Component";
import { CacheMap } from "../interfaces/Cache";
import MapSelectorPage from "../pages/MapSelectorPage";


export default class BeatmapCard extends Component {
    Map: CacheMap


    constructor(map: CacheMap) {
        super();

        this.Map = map

        const bottomLine = <div className="bottom-line" />;
        if (map.Difficulty !== undefined) {
            const diff = <span>{map.DifficultyString}</span>
            diff.classList.add("difficulty-" + map.Difficulty);
            bottomLine.appendChild(diff)
        }

        if (map.Mapper) {
            bottomLine.appendChild(<span> mapped by {map.Mapper}</span>)
        }

        this.HTMLElement = <div className="beatmap-card">
            <div className="title">{map.Title}</div>
            <div className="artist">{map.Artist}</div>
            {bottomLine}
        </div>;

        this.HTMLElement.onclick = () => {
            this.FindParent(MapSelectorPage).LoadMap(this.Map);
        }
    }
}