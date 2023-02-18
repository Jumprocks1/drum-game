import { RegisterListener, RemoveListener } from "../../framework/Framework";
import PageComponent from "../../framework/PageComponent";
import GlobalData from "../../GlobalData";
import { PathBuilder } from "../../utils/PathBuilder";
import { LogoBackground } from "./LogoBackground";
import MapSelectorPage from "../MapSelectorPage";
import LogoPath from "./LogoPath";

export default class LogoPage extends PageComponent {
    static Route = "logo"

    Canvas: HTMLCanvasElement
    LogoBackground: LogoBackground

    static targetSize = 800;
    static renderRadius = 100;

    CanvasLoaded = false;

    AfterParent() {
        super.AfterParent();
        RegisterListener("newframe", this.Render);
        this.InitCanvas();
    }

    InitCanvas() {
        this.InitContext();
        this.CanvasLoaded = true;
    }

    CheckSize(reinit: boolean) {
        const pixelRatio = window.devicePixelRatio;
        const targetHeightBase = 800;
        const targetHeight = targetHeightBase * pixelRatio;
        const targetWidth = targetHeight;

        if (Math.abs(this.Canvas.width - targetWidth) > 0.01 || Math.abs(this.Canvas.height - targetHeight) > 0.01) {
            this.Canvas.style.height = targetHeightBase + "px";
            this.Canvas.height = targetHeight;
            this.Canvas.width = targetWidth;

            if (reinit) this.InitContext(); // changing size resets all Context parameters
        }
    }

    InitContext() {
        console.log("initializing canvas context");
        this.CheckSize(false);

        this.LogoBackground.Init(this.Canvas.getContext("webgl2", { antialias: true }));
    }

    Render = () => {
        if (!this.CanvasLoaded) return;

        this.CheckSize(true);

        this.LogoBackground.Draw();
    }

    AfterRemove() {
        super.AfterRemove();
        RemoveListener("newframe", this.Render)
    }


    constructor() {
        super();

        this.Canvas = <canvas /> as HTMLCanvasElement

        const pathBuilder = new PathBuilder();

        LogoPath(pathBuilder);

        this.LogoBackground = new LogoBackground();
        this.LogoBackground.Points = pathBuilder.Points;

        this.HTMLElement = <div style={{ position: "relative" }}>
            {this.Canvas}
        </div>
    }
}