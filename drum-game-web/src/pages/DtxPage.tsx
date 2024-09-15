import PageComponent from "../framework/PageComponent";
import Router, { GlobalRouter, RouteParameters } from "../framework/Router";
import GlobalData from "../GlobalData";
import MapCarousel, { CarouselState } from "../selector/MapCarousel";
import { CacheMap, CacheMapLink } from "../interfaces/Cache";
import BeatmapPlayerPage from "./BeatmapPlayerPage";
import MapPreview from "../components/MapPreview";

export default class DtxPage extends PageComponent {
    static Route = "dtx/$0|dtx/?"
    static RouteUrl = "dtx/$0"
    static PageId = "dtx-page"
    static Title = "DTX Maps"


    MapUrl: string | undefined;

    LoadRoute(parameters: RouteParameters) {
        this.MapUrl = parameters[0]
    }

    LoadMap(map: CacheMap | undefined) {
        if (!map) return;
        const link = CacheMapLink(map)
        if (link)
            GlobalRouter?.NavigateTo({ page: BeatmapPlayerPage, parameters: [link] });
    }

    AfterParent() {
        super.AfterParent();
        const carousel = new MapCarousel();

        const preview = new MapPreview(true);
        carousel.OnMapChange = e => {
            preview.SetMap(e);
            document.title = e ? `${e.Artist} - ${e.Title}` : "No maps found"
            if (this.MapUrl && e) { // if we have a map in the URL, we make sure to keep updating the URL
                const newUrl = CacheMapLink(e);
                if (newUrl !== this.MapUrl) {
                    const router = this.FindParent(Router);
                    this.MapUrl = newUrl;
                    router.ReplaceRoute({ page: DtxPage, parameters: [this.MapUrl ?? ""] })
                }
            }
        }
        carousel.OpenOnCardClick = false;
        carousel.OnMapOpen = this.LoadMap;


        this.Add(preview);
        this.Add(carousel);

        GlobalData.DtxMapList().then(maps => {
            if (!this.Alive) return;
            const o: CacheMap[] = Object.values(maps.Maps);
            let target: CacheMap | undefined = CarouselState.map === undefined ? o[0] : undefined;
            if (this.MapUrl)
                target = maps.Maps[this.MapUrl] ?? maps.Maps[this.MapUrl + ".bjson"]
            carousel.SetItems(o, target);
        })
    }

    AfterRemove() {
        super.AfterRemove();
        this.ChildrenAfterRemove();
    }
}