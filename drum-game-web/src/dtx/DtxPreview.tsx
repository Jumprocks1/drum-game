import Component from "../framework/Component";
import { CacheMap } from "../interfaces/Cache";

export default class DtxPreview extends Component {
    Image = <img /> as HTMLImageElement


    constructor() {
        super();
        this.HTMLElement = <div id="map-preview">
            {this.Image}
        </div>
    }

    SetMap(map: CacheMap) {
        this.Image.src = map.DtxInfo.image
    }
}