import Component from "./framework/Component";
import type { PageType } from "./framework/Router";
import Router from "./framework/Router";
import WebSocketPage from "./pages/WebSocketPage";
import BeatmapLoaderPage from "./pages/BeatmapLoaderPage";
import TestPage from "./pages/TestPage";

const pages: PageType[] = [
    TestPage,
    WebSocketPage,
    BeatmapLoaderPage
]

export default class Root extends Component {
    constructor() {
        super();

        this.DOMNode = document.getElementById("root")!;
        this.Add(new Router(pages));
    }
}