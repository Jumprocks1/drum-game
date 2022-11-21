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
            const player = new BeatmapPlayer(fullMap);
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

    AfterRemove() {
        super.AfterRemove();
        this.Clear();
    }
}