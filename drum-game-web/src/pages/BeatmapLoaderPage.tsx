import PageComponent from "../framework/PageComponent";
import Router from "../framework/Router";
import GlobalData from "../GlobalData";
import { CacheMap } from "../interfaces/Cache";
import MapSelector from "../selector/MapSelector";
import BeatmapPlayerPage from "./BeatmapPlayerPage";

export default class BeatmapLoaderPage extends PageComponent {
    static Route = ".*"
    static RouteUrl = ""

    AfterParent() {
        super.AfterParent();
        this.Add(new MapSelector()) // TODO merge this into this component
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