import Component from "./framework/Component";
import type { PageType } from "./framework/Router";
import Router from "./framework/Router";
import WebSocketPage from "./pages/WebSocketPage";
import MapSelectorPage from "./pages/MapSelectorPage";
import TestPage from "./pages/TestPage";
import BeatmapPlayerPage from "./pages/BeatmapPlayerPage";
import DtxPage from "./pages/DtxPage";
import LogoPage from "./pages/logo/LogoPage";
import RequestListPage from "./pages/RequestListPage";

const pages: PageType[] = [
    TestPage,
    WebSocketPage,
    BeatmapPlayerPage,
    DtxPage,
    LogoPage,
    RequestListPage,
    MapSelectorPage,
]

export default class Root extends Component {
    constructor() {
        super();

        this.DOMNode = document.getElementById("root")!;
        var router = new Router(pages);
        router.DefaultTitle = "Drum Game Web"
        this.Add(router);
    }
}