import PageComponent from "../framework/PageComponent";
import GlobalData from "../GlobalData";
import BeatmapLoaderPage from "./BeatmapLoaderPage";

export default class TestPage extends PageComponent {
    static Route = "test"

    AfterParent() {
        super.AfterParent();

        GlobalData.LoadMapList().then(e => {
            this.Add(new BeatmapLoaderPage(e.Maps["torikago.bjson"]));
        });
    }
}