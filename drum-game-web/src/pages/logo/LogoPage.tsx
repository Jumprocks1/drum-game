import PageComponent from "../../framework/PageComponent";
import Logo from "./Logo";

export default class LogoPage extends PageComponent {
    static Route = "logo"

    constructor() {
        super()
        this.Add(new Logo());
        // const logo2 = new Logo();
        // logo2.Type = "full"
        // this.Add(logo2);
    }
}