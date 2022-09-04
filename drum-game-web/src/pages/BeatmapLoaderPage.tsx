import { loadMap } from "../api/network";
import Loading from "../components/Loading";
import PageComponent from "../framework/PageComponent";
import GlobalData from "../GlobalData";
import { CacheMap } from "../interfaces/Cache";
import WebSocketPage from "./WebSocketPage";
import BeatmapPlayer from "../playfield/BeatmapPlayer";
import MapSelector from "../selector/MapSelector";

export default class BeatmapLoaderPage extends PageComponent {
    static Route = ".*"
    static RouteUrl = ""

    CurrentMap?: CacheMap;

    constructor(startingMap?: CacheMap) {
        super();
        this.CurrentMap = startingMap;
    }

    AfterParent() {
        super.AfterParent();
        if (this.CurrentMap) {
            this.LoadMap(this.CurrentMap);
        } else {
            this.Add(new MapSelector())
        }
    }

    showingMap = false;

    loading = false;
    LoadMap(map: CacheMap) {
        if (this.loading) return;
        GlobalData.LoadBravura(); // preload
        this.showingMap = true;
        this.loading = true;
        this.Clear();
        const loading = (async () => {
            const fullMap = await loadMap(map.FileName);
            this.Clear();
            this.Add(new BeatmapPlayer(fullMap))
        })()

        this.Add(Loading(loading));
    }

    ShowSelector() {
        if (!this.showingMap) return;
        this.loading = false;
        this.showingMap = false;
        this.Clear();
        this.Add(new MapSelector())
    }
}