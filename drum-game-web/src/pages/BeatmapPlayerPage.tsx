import { loadMap } from "../api/network";
import Loading from "../components/Loading";
import PageComponent from "../framework/PageComponent";
import { RouteParameters } from "../framework/Router";
import BeatmapPlayer from "../playfield/BeatmapPlayer";

export default class BeatmapPlayerPage extends PageComponent {
    static Route = "play/$0"

    Map: string | undefined;

    LoadRoute(parameters: RouteParameters) {
        this.Map = parameters[0];
    }

    AfterParent() {
        super.AfterParent();
        if (!this.Map) return;
        const loading = (async () => {
            const fullMap = await loadMap(this.Map!);
            if (!this.Alive) return;
            this.Clear();
            this.Add(new BeatmapPlayer(fullMap))
        })()
        this.Add(Loading(loading));
    }

    AfterRemove() {
        super.AfterRemove();
        this.Clear();
    }
}