import MapCarousel, { CarouselState } from "../selector/MapCarousel";
import PageComponent from "../framework/PageComponent";
import Router, { GlobalRouter } from "../framework/Router";
import GlobalData from "../GlobalData";
import { CacheMap } from "../interfaces/Cache";
import BeatmapPlayerPage from "./BeatmapPlayerPage";
import MapPreview from "../components/MapPreview";
import { loadMap } from "../api/network";
import BeatmapPlayer from "../playfield/BeatmapPlayer";
import Loading from "../components/Loading";

export default class MapSelectorPage extends PageComponent {
    static Route = "upload"
    static PageId = "file-upload-page"
    static Title = "Custom BJson upload"

    FileUpload = <input type="file" /> as HTMLInputElement

    TryLoadCurrentFile() {
        const files = this.FileUpload.files
        if (files?.length !== 1) return
        const file = files[0]
        if (!file.name.endsWith("bjson")) return

        this.FileUpload.remove()

        const loading = (async () => {
            const fullMap = JSON.parse(await file.text())
            if (!this.Alive) return;
            this.Clear();
            document.title = `${fullMap.artist} - ${fullMap.title}`
            const player = new BeatmapPlayer(fullMap);
            player.OnEscape = () => {
                GlobalRouter?.NavigateTo({ page: MapSelectorPage }, true)
            }
            this.Add(player)

            // should only really do this if we are previewing the sheet music
            // this is just to make sure that users aren't confused if they see a page with no notes on it
            const firstHitObject = player.Beatmap.HitObjects[0];
            if (firstHitObject) {
                player.Track.CurrentBeat = firstHitObject.time;
            }
        })()
        this.Add(Loading(loading));
    }

    AfterParent() {
        super.AfterParent();

        // we don't remove this handlers but I think that's fine
        this.HTMLElement.addEventListener("dragenter", e => e.preventDefault());
        this.HTMLElement.addEventListener("dragover", e => e.preventDefault());
        this.HTMLElement.addEventListener("drop", e => {
            const files = e.dataTransfer?.files
            if (files && files.length > 0) {
                this.FileUpload.files = files
                this.TryLoadCurrentFile()
                e.preventDefault()
            }
        })

        this.FileUpload.addEventListener("change", this.TryLoadCurrentFile)

        this.HTMLElement.append(this.FileUpload)

        const rawData = GlobalRouter?.State?.rawData
        if (rawData) {
            this.FileUpload.files = rawData
            this.TryLoadCurrentFile()
        }
    }

    AfterRemove() {
        super.AfterRemove();
        this.ChildrenAfterRemove();
    }
}