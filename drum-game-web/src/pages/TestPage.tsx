import PageComponent from "../framework/PageComponent";
import GlobalData from "../GlobalData";
import MapSelectorPage from "./MapSelectorPage";

export default class TestPage extends PageComponent {
    static Route = "test"

    AfterParent() {
        super.AfterParent();
    }
}