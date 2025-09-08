import { buildUrl } from "../api/network";
import { getUrl } from "../api/spotify";
import Component from "../framework/Component";
import { RouteLink } from "../framework/RouteButton";
import { GlobalRouter } from "../framework/Router";
import { CacheMap, CacheMapLink } from "../interfaces/Cache";
import BeatmapPlayerPage from "../pages/BeatmapPlayerPage";
import RequestListPage from "../pages/RequestListPage";

function formatDuration(duration: number | undefined) {
    if (duration === undefined) return 0
    const s = Math.floor(duration / 1000)
    const m = Math.floor(s / 60)
    return `${m}:${(s % 60).toString().padStart(2, "0")}`
}

export default class MapPreview extends Component {
    private Dtx = false;

    Image = <img /> as HTMLImageElement
    Title = <h3 />
    Description = <h5 />
    Download = <a target="_blank" rel="noreferrer noopener"></a> as HTMLAnchorElement
    Date = <span />
    DownloadLine = <div></div>
    CopyResultText: HTMLElement | undefined

    Preview = <RouteLink page={BeatmapPlayerPage}>Preview Sheet Music</RouteLink>
    SpotifyPreview = <a className="clickable-text" target="_blank" rel="noreferrer noopener">Listen on Spotify</a> as HTMLAnchorElement


    constructor(dtx = false) {
        super();
        this.Dtx = dtx;


        const requestListPage = GlobalRouter?.State?.page === RequestListPage

        if (dtx) {
            this.Download.innerText = "Download DTX"
        } else {
            this.Download.innerText = "Download .bjson file"
        }
        let requestListButton: HTMLElement | undefined = undefined
        if (requestListPage) {
            this.CopyResultText = <div></div>
            requestListButton = <div id="copy-button-container">
                <button onclick={() => this.copyRequestCommand()}>
                    Copy request command
                </button>
                {this.CopyResultText}
            </div>
        }
        this.HTMLElement = <div id="map-preview">
            {this.Image}
            {this.Title}
            {this.Description}
            {this.DownloadLine}
            {requestListButton}
            <div style={{ fontSize: "0.7em" }}>
                <div>{this.SpotifyPreview}</div>
                {this.Preview}
            </div>
        </div>
    }
    Map?: CacheMap;

    async copyRequestCommand() {
        const map = this.Map;
        if (map) {
            const copy = `!rq ${map.Artist} - ${map.Title}`
            await navigator.clipboard.writeText(copy)
            if (this.CopyResultText)
                this.CopyResultText.innerText = " - Copied to clipboard"
        }
    }

    SetMap(map: CacheMap | undefined) {
        this.Map = map;
        if (!map) {
            this.HTMLElement.style.visibility = "hidden";
        } else {
            this.HTMLElement.style.visibility = "unset";
            if (this.CopyResultText)
                this.CopyResultText.innerText = ""

            const requestListPage = GlobalRouter?.State?.page === RequestListPage

            let imageUrl = map.ImageUrl;
            // ignore images on request list page for now
            if (requestListPage)
                imageUrl = undefined

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
            if (this.Image.src && this.Image.src != imageUrl) {
                // blank out image while loading
                // without this, it will display the previous image while it loads the next one
                this.Image.src = ""
            }
            this.Image.src = imageUrl ?? "";
            this.Title.textContent = `${map.Artist} - ${map.Title}`;
            this.Description.textContent = `${map.MedianBPM} BPM - ${map.DifficultyString} - Length: ${formatDuration(map.PlayableDuration)}`;
            this.Date.textContent = map.Date ?? "";

            let download = map.DownloadUrl;
            let fileName = map.FileName;
            if (!download && fileName) {
                if (!fileName.endsWith(".bjson")) fileName += ".bjson"
                download = buildUrl(`/maps/${fileName}`)
            }
            this.Download.href = download ?? "";
            this.DownloadLine.replaceChildren(map.Date ? <>{this.Download} - {this.Date}</> : this.Download);
            this.Download.style.display = download ? "" : "none"

            const link = CacheMapLink(map);
            const routeLink = (this.Preview.Component as RouteLink)
            routeLink.HTMLElement.style.display = link ? "" : "none"
            if (link)
                routeLink.Parameters = [link];
        }
    }
}