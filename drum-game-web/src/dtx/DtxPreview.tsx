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
    SpotifyPreview = <div className="clickable-text" onclick={() => this.ShowSpotifyIframe()}>Show Spotify Preview</div>
    SpotifyIframe?: HTMLIFrameElement;


    ShowSpotifyIframe() {
        if (!this.Map) return;
        this.SpotifyPreview.style.display = "none";
        this.SpotifyIframe = <iframe
            src={`https://open.spotify.com/embed/track/${this.Map.SpotifyTrack}`}
            className="spotify-preview"
            style={{ borderRadius: "12px", display: "block" }}
            width="500"
            height="152" frameBorder="0"
            // @ts-ignore
            loading="lazy"
        /> as HTMLIFrameElement
        this.HTMLElement.appendChild(this.SpotifyIframe);
    }


    constructor() {
        super();
        this.HTMLElement = <div id="map-preview">
            {this.Image}
            {this.Title}
            {this.Description}
            {this.DownloadLine}
            {this.Preview}
            {this.SpotifyPreview}
        </div>
    }
    Map?: CacheMap;

    SetMap(map: CacheMap) {
        this.Map = map;
        if (!map) {
            this.HTMLElement.style.visibility = "hidden";
        } else {
            this.HTMLElement.style.visibility = "unset";
            let imageUrl = map.ImageUrl;

            // get slightly higher resolution
            const check = "_SS400_.jpg";
            if (imageUrl && imageUrl.endsWith(check)) {
                imageUrl = imageUrl.substring(0, imageUrl.length - check.length) + "_SS500_.jpg";
            }

            if (this.SpotifyIframe) {
                this.HTMLElement.removeChild(this.SpotifyIframe);
                this.SpotifyIframe = undefined;
            }

            this.SpotifyPreview.style.display = map.SpotifyTrack ? "block" : "none";
            this.Image.src = imageUrl ?? "";
            this.Title.textContent = `${map.Artist} - ${map.Title}`;
            this.Description.textContent = `${map.BPM} BPM - ${map.DifficultyString}`;
            this.Date.textContent = map.Date ?? "";
            this.Download.href = map.DownloadUrl ?? "";
            (this.Preview.Component as RouteLink).Parameters = [CacheMapLink(map)]
        }
    }
}