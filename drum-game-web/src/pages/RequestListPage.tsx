import MapCarousel, { CarouselState } from "../selector/MapCarousel";
import PageComponent from "../framework/PageComponent";
import Router from "../framework/Router";
import GlobalData from "../GlobalData";
import { CacheMap } from "../interfaces/Cache";
import BeatmapPlayerPage from "./BeatmapPlayerPage";
import MapPreview from "../components/MapPreview";

export default class MapSelectorPage extends PageComponent {
    static Route = "request"
    static PageId = "request-list-page"
    static Title = "Jumprocks Request List"

    AfterParent() {
        super.AfterParent();

        const carousel = new MapCarousel();
        carousel.OnMapOpen = () => { }

        const preview = new MapPreview();
        carousel.OnMapChange = e => {
            preview.SetMap(e);
        }

        this.Add(preview);
        this.Add(carousel);

        GlobalData.LoadRequestList().then(maps => {
            if (!this.Alive) return;
            carousel.SetItems(Object.values(maps));
        })
    }

    AfterRemove() {
        super.AfterRemove();
        this.ChildrenAfterRemove();
    }
}