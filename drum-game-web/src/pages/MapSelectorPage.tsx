import MapCarousel from "../selector/MapCarousel";
import PageComponent from "../framework/PageComponent";
import Router from "../framework/Router";
import GlobalData from "../GlobalData";
import { CacheMap } from "../interfaces/Cache";
import BeatmapCard from "../selector/BeatmapCard";
import BeatmapPlayerPage from "./BeatmapPlayerPage";

export default class MapSelectorPage extends PageComponent {
    static Route = ".*"
    static RouteUrl = ""

    AfterParent() {
        super.AfterParent();

        const carousel = new MapCarousel(BeatmapCard, 106);

        this.Add(carousel);

        GlobalData.LoadMapList().then(maps => {
            if (!this.Alive) return;
            carousel.SetItems(Object.values(maps.Maps));
        })
    }

    AfterRemove() {
        super.AfterRemove();
        this.ChildrenAfterRemove();
    }

    LoadMap(map: CacheMap | undefined) {
        if (!map || !this.Alive) return;
        GlobalData.LoadBravura(); // preload
        const ext = ".bjson";
        const target = map.FileName.endsWith(ext) ? map.FileName.substring(0, map.FileName.length - ext.length) : map.FileName
        this.FindParent(Router).NavigateTo(BeatmapPlayerPage, target) // this will kill us
    }
}