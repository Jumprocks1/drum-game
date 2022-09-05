import Component from "./framework/Component";
import type { PageType } from "./framework/Router";
import Router from "./framework/Router";
import WebSocketPage from "./pages/WebSocketPage";
import MapSelectorPage from "./pages/MapSelectorPage";
import TestPage from "./pages/TestPage";
import BeatmapPlayerPage from "./pages/BeatmapPlayerPage";

const pages: PageType[] = [
    TestPage,
    WebSocketPage,
    BeatmapPlayerPage,
    MapSelectorPage
]

export default class Root extends Component {
    constructor() {
        super();

        this.DOMNode = document.getElementById("root")!;
        this.Add(new Router(pages));
    }
}