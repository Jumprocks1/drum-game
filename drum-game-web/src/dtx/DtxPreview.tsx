import Component from "../framework/Component";
import { CacheMap } from "../interfaces/Cache";

export default class DtxPreview extends Component {
    Image = <img /> as HTMLImageElement
    Title = <h3 />
    Description = <h5 />
    Download = <a href="https://jumprocks1.github.io/drum-game" target="_blank" rel="noreferrer noopener">Download</a>
    Date = <span />
    DownloadLine = <div>
        {this.Download} - {this.Date}
    </div>


    constructor() {
        super();
        this.HTMLElement = <div id="map-preview">
            {this.Image}
            {this.Title}
            {this.Description}
            {this.DownloadLine}
        </div>
    }

    SetMap(map: CacheMap) {
        this.Image.src = map.ImageUrl ?? "";
        this.Title.textContent = `${map.Artist} - ${map.Title}`;
        this.Description.textContent = `${map.BpmString} BPM - ${map.DifficultyString}`;
        this.Date.textContent = map.Date ?? "";
    }
}