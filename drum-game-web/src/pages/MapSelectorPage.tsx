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

        const div = <div id="map-selector" />;
        this.Add(div);

        GlobalData.LoadMapList().then(maps => {
            if (!this.Alive) return;
            for (const key in maps.Maps)
                div.Component!.Add(new BeatmapCard(maps.Maps[key]))
        })
    }


    Focus(card: BeatmapCard) {
        card.HTMLElement.scrollIntoView();
    }

    showingMap = false;

    loading = false;
    LoadMap(map: CacheMap) {
        if (!this.Alive) return;
        GlobalData.LoadBravura(); // preload
        const ext = ".bjson";
        const target = map.FileName.endsWith(ext) ? map.FileName.substring(0, map.FileName.length - ext.length) : map.FileName
        this.FindParent(Router).NavigateTo(BeatmapPlayerPage, target)
    }
}