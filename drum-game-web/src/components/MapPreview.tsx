import { buildUrl } from "../api/network";
import { getUrl } from "../api/spotify";
import Component from "../framework/Component";
import { RouteLink } from "../framework/RouteButton";
import { CacheMap, CacheMapLink } from "../interfaces/Cache";
import BeatmapPlayerPage from "../pages/BeatmapPlayerPage";

export default class MapPreview extends Component {
    private Dtx = false;

    Image = <img /> as HTMLImageElement
    Title = <h3 />
    Description = <h5 />
    Download = <a target="_blank" rel="noreferrer noopener"></a> as HTMLAnchorElement
    Date = <span />
    DownloadLine = <div></div>

    Preview = <RouteLink page={BeatmapPlayerPage}>Preview Sheet Music</RouteLink>
    SpotifyPreview = <a className="clickable-text" target="_blank" rel="noreferrer noopener">Listen on Spotify</a> as HTMLAnchorElement


    constructor(dtx = false) {
        super();
        this.Dtx = dtx;
        if (dtx) {
            this.Download.innerText = "Download DTX"
        } else {
            this.Download.innerText = "Download .bjson file"
        }
        this.HTMLElement = <div id="map-preview">
            {this.Image}
            {this.Title}
            {this.Description}
            {this.DownloadLine}
            <div style={{ fontSize: "0.7em" }}>
                <div>{this.SpotifyPreview}</div>
                {this.Preview}
            </div>
        </div>
    }
    Map?: CacheMap;

    SetMap(map: CacheMap | undefined) {
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


            const spotifyUrl = getUrl(map.Spotify);
            this.SpotifyPreview.style.display = spotifyUrl ? "unset" : "none";
            if (spotifyUrl) {
                this.SpotifyPreview.href = spotifyUrl;
            }
            this.Image.src = imageUrl ?? "";
            this.Title.textContent = `${map.Artist} - ${map.Title}`;
            this.Description.textContent = `${map.MedianBPM} BPM - ${map.DifficultyString}`;
            this.Date.textContent = map.Date ?? "";

            let download = map.DownloadUrl;
            if (!download) {
                let fileName = map.FileName;
                if (!fileName.endsWith(".bjson")) fileName += ".bjson"
                download = buildUrl(`/maps/${fileName}`)
            }

            this.Download.href = download ?? "";

            this.DownloadLine.replaceChildren(map.Date ? <>{this.Download} - {this.Date}</> : this.Download);

            (this.Preview.Component as RouteLink).Parameters = [CacheMapLink(map)]
        }
    }
}