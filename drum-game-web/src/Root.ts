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
        const meta = [
            ["og:title", "Drum Game Test Title"],
            ["og:description", "Test description"],
            ["og:image", "https://m.media-amazon.com/images/I/81m81sm7O7L._SS500_.jpg"],
        ]

        for (const e of meta) {
            const m = document.createElement("meta");
            m.setAttribute("property", e[0]);
            m.setAttribute("content", e[1]);
            document.head.appendChild(m)
        }

        this.DOMNode = document.getElementById("root")!;
        this.Add(new Router(pages));
    }
}