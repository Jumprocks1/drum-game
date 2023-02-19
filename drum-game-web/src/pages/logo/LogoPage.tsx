import { RegisterListener, RemoveListener } from "../../framework/Framework";
import PageComponent from "../../framework/PageComponent";
import GlobalData from "../../GlobalData";
import { PathBuilder } from "../../utils/PathBuilder";
import { LogoRenderer } from "./LogoRenderer";
import MapSelectorPage from "../MapSelectorPage";
import { DGLogoPath, FullLogoPath } from "./LogoPath";
import LineMesh from "./LineMesh";

type LogoType = "full" | "initials"

export default class LogoPage extends PageComponent {
    static Route = "logo"

    Canvas: HTMLCanvasElement
    Renderer: LogoRenderer

    static targetSize = 800;
    static renderRadius = 100;

    Type: LogoType = "initials"

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

        this.Renderer.Init(this.Canvas.getContext("webgl2", { antialias: true }));
    }

    Render = () => {
        if (!this.CanvasLoaded) return;

        this.CheckSize(true);

        this.Renderer.Draw();
    }

    AfterRemove() {
        super.AfterRemove();
        RemoveListener("newframe", this.Render)
    }


    constructor() {
        super();

        this.Canvas = <canvas /> as HTMLCanvasElement

        const pathBuilder = new PathBuilder();

        this.Renderer = new LogoRenderer();

        if (this.Type === "full") {
            FullLogoPath(pathBuilder);
            this.Renderer.Mesh = LineMesh(pathBuilder.Points, 3);
        } else {
            DGLogoPath(pathBuilder);
            this.Renderer.Mesh = LineMesh(pathBuilder.Points, 6);
        }

        this.HTMLElement = <div style={{ position: "relative" }}>
            {this.Canvas}
        </div>
    }
}