import Component from "../framework/Component";
import { CacheMap } from "../interfaces/Cache";
import MapSelectorPage from "../pages/MapSelectorPage";


export default class BeatmapCard extends Component {
    Map!: CacheMap;

    get CurrentItem() { return this.Map; }

    Title = <div className="title" />
    Artist = <div className="artist" />
    BottomLine = <div className="bottom-line" />
    MappedBy = <span />
    Difficulty = <span />

    SetContent(map: CacheMap) {
        this.Map = map

        this.Title.textContent = map.Title;
        this.Artist.textContent = map.Artist;

        this.MappedBy.textContent = map.Mapper ? ` mapped by ${map.Mapper}` : "";
        this.Difficulty.textContent = map.Difficulty ? map.DifficultyString! : "";
        this.Difficulty.className = "difficulty-" + map.Difficulty;
    }


    constructor(map: CacheMap) {
        super();

        this.BottomLine.appendChild(this.Difficulty);
        this.BottomLine.appendChild(this.MappedBy);

        const card = <div>
            {this.Title}
            {this.Artist}
            {this.BottomLine}
        </div>;
        card.onclick = () => {
            this.FindParent(MapSelectorPage).LoadMap(this.Map);
        }
        this.HTMLElement = <div className="beatmap-card-wrapper">
            {card}
        </div>

        this.SetContent(map);
    }
}