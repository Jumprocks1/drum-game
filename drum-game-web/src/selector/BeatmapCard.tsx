import Component from "../framework/Component";
import { CacheMap } from "../interfaces/Cache";
import MapSelectorPage from "../pages/MapSelectorPage";
import MapCarousel from "./MapCarousel";


export default class BeatmapCard extends Component {
    Map!: CacheMap;

    get CurrentItem() { return this.Map; }

    Title = <div className="title" />
    Artist = <div className="artist" />
    BottomLine = <div className="bottom-line" />
    MappedBy = <span />
    Date = <span className="date" />
    Difficulty = <span />
    Card = <div>
        {this.Title}
        {this.Artist}
        {this.BottomLine}
        {this.Date}
    </div>


    private _selected = false;
    set Selected(value: boolean) {
        if (this._selected === value) return;
        this._selected = value;
        if (value)
            this.Card.classList.add("active")
        else
            this.Card.classList.remove("active")
    }

    SetContent(map: CacheMap) {
        this.Map = map

        this.Title.textContent = map.Title;
        this.Artist.textContent = map.Artist;
        this.Date.textContent = map.Date ?? "";

        this.MappedBy.textContent = map.Mapper ? ` mapped by ${map.Mapper}` : "";
        this.Difficulty.textContent = map.Difficulty ? map.DifficultyString! : "";
        this.Difficulty.className = "difficulty-" + map.Difficulty;
    }

    Click() {
        const carousel = this.FindParent(MapCarousel);
        if (carousel.SelectedMap === this.Map && carousel.OpenOnCardClick) carousel.OpenMap(this.Map);
        else (carousel.Select(this.Map))
    }


    constructor(map: CacheMap) {
        super();

        this.BottomLine.appendChild(this.Difficulty);
        this.BottomLine.appendChild(this.MappedBy);

        this.HTMLElement = <div className="beatmap-card-wrapper">
            {this.Card}
        </div>
        this.HTMLElement.Component = this; // little sketchy

        this.SetContent(map);
    }
}