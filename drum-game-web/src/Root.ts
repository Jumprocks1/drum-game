import Component from "./framework/Component";
import type { PageType } from "./framework/Router";
import Router from "./framework/Router";
import WebSocketPage from "./pages/WebSocketPage";
import MapSelectorPage from "./pages/MapSelectorPage";
import TestPage from "./pages/TestPage";
import BeatmapPlayerPage from "./pages/BeatmapPlayerPage";
import DtxPage from "./pages/DtxPage";

const pages: PageType[] = [
    TestPage,
    WebSocketPage,
    BeatmapPlayerPage,
    DtxPage,
    MapSelectorPage,
]

export default class Root extends Component {
    constructor() {
        super();

        const meta = document.getElementsByTagName("meta");
        for (const e of meta) {
            if (e.content === "Drum Game Test") {
                e.content = "Drum Game Test 2"
            }
        }

        this.DOMNode = document.getElementById("root")!;
        this.Add(new Router(pages));
    }
}