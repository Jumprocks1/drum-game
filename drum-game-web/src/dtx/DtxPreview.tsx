import Component from "../framework/Component";
import { RouteLink } from "../framework/RouteButton";
import { CacheMap, CacheMapLink } from "../interfaces/Cache";
import BeatmapPlayerPage from "../pages/BeatmapPlayerPage";

export default class DtxPreview extends Component {
    Image = <img /> as HTMLImageElement
    Title = <h3 />
    Description = <h5 />
    Download = <a target="_blank" rel="noreferrer noopener">Download</a> as HTMLAnchorElement
    Date = <span />
    DownloadLine = <div>
        {this.Download} - {this.Date}
    </div>
    Preview = <RouteLink page={BeatmapPlayerPage}>Preview Sheet Music</RouteLink>


    constructor() {
        super();
        this.HTMLElement = <div id="map-preview">
            {this.Image}
            {this.Title}
            {this.Description}
            {this.DownloadLine}
            {this.Preview}
        </div>
    }

    SetMap(map: CacheMap) {
        if (!map) {
            this.HTMLElement.style.visibility = "hidden";
        } else {
            this.HTMLElement.style.visibility = "unset";
            this.Image.src = map.ImageUrl ?? "";
            this.Title.textContent = `${map.Artist} - ${map.Title}`;
            this.Description.textContent = `${map.BPM} BPM - ${map.DifficultyString}`;
            this.Date.textContent = map.Date ?? "";
            this.Download.href = map.DownloadUrl ?? "";
            (this.Preview.Component as RouteLink).Parameters = [CacheMapLink(map)]
        }
    }
}