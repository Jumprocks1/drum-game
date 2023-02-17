import { RegisterListener, RemoveListener } from "../../framework/Framework";
import PageComponent from "../../framework/PageComponent";
import GlobalData from "../../GlobalData";
import PathDrawer from "../../utils/PathDrawer";
import { LogoBackground } from "./LogoBackground";
import MapSelectorPage from "../MapSelectorPage";

function rad(deg: number) {
    return deg / 180 * Math.PI;
}

export default class LogoPage extends PageComponent {
    static Route = "logo"

    Canvas: HTMLCanvasElement
    Context: CanvasRenderingContext2D
    PathDrawer: PathDrawer

    BackgroundCanvas: HTMLCanvasElement
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

    Transform() {
        this.Context.resetTransform();
        const pixelRatio = window.devicePixelRatio;
        this.Context.scale(pixelRatio, pixelRatio);

        // goal is to transform to 200,200 with 0,0 in the center
        const scale = LogoPage.targetSize / (LogoPage.renderRadius * 2);
        this.Context.scale(scale, scale)

        this.Context.translate(100, 100)

        this.Context.scale(1, 1)
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

            this.BackgroundCanvas.style.height = targetHeightBase + "px";
            this.BackgroundCanvas.height = targetHeight;
            this.BackgroundCanvas.width = targetWidth;

            if (reinit) this.InitContext(); // changing size resets all Context parameters
        }
    }

    InitContext() {
        console.log("initializing canvas context");
        this.CheckSize(false);

        this.LogoBackground.Init(this.BackgroundCanvas.getContext("webgl2", { antialias: true }));

        this.Context.lineWidth = this.StrokeWidth;
        this.Context.lineCap = "round"
        this.Context.lineJoin = "round"
    }

    Render = () => {
        if (!this.CanvasLoaded) return;

        this.CheckSize(true);

        this.Context.clearRect(0, 0, this.Canvas.width, this.Canvas.height);

        this.Context.save();

        this.Transform();

        this.LogoBackground.Draw();
        this.Draw();

        this.Context.restore();
    }

    OuterRadius = 100;

    StrokeWidth = 6
    SmallStrokeWidth = 4;

    DPos = [-55, 20]
    DHeight = 90;

    MainAngle = rad(40)

    GPos = [-44, 68] // bottom left (G doesn't actually touch this)
    GSeg = [15, 30, 20, 10, 15]
    GHeight = 60;

    Draw() {
        const context = this.Context;
        const PathDrawer = this.PathDrawer;


        const ang = this.MainAngle;
        // return; 

        context.strokeStyle = "#2472c8"
        context.lineWidth = this.StrokeWidth;
        context.beginPath();

        // D
        PathDrawer.MoveTo([this.DPos[0], this.DPos[1]]);
        PathDrawer.LineToRelative([0, -this.DHeight]);
        const dWidth = Math.sin(ang) / (Math.sin(Math.PI / 2 - ang) / (this.DHeight / 2));
        PathDrawer.LineToRelative([dWidth, this.DHeight / 2])
        const dRight = PathDrawer.X;
        PathDrawer.LineToRelative([-dWidth, this.DHeight / 2])

        // G
        const ang1 = Math.PI / 2 - ang
        const angleSegmentLength = (this.GHeight - this.GSeg[1]) / 2 / Math.sin(ang1)

        const topLeft = [this.GPos[0] + angleSegmentLength * Math.cos(ang1), this.GPos[1] - this.GHeight]

        PathDrawer.MoveTo([topLeft[0] + this.GSeg[0], topLeft[1]])
        PathDrawer.LineToRelative([-this.GSeg[0], 0])
        PathDrawer.LineToRelative([-angleSegmentLength * Math.cos(ang1), this.GHeight / 2 - this.GSeg[1] / 2])
        PathDrawer.LineToRelative([0, this.GSeg[1]])
        PathDrawer.LineToRelative([angleSegmentLength * Math.cos(ang1), this.GHeight / 2 - this.GSeg[1] / 2])
        PathDrawer.LineToRelative([this.GSeg[0], 0])
        PathDrawer.LineToRelative([Math.cos(ang1) * this.GSeg[2], -Math.sin(ang1) * this.GSeg[2]])
        PathDrawer.LineToRelative([0, -this.GSeg[3]])
        const gRight = PathDrawer.X;
        PathDrawer.LineToRelative([-this.GSeg[4], 0])

        context.stroke();
        context.beginPath();

        const smallSpacing = 8;
        const smallLetterHeight = 30;

        // RUM
        context.lineWidth = this.SmallStrokeWidth;
        let baseline = this.DPos[1] - 20;
        let top = baseline - smallLetterHeight;

        let left = dRight + smallSpacing;
        PathDrawer.MoveTo([left, baseline])
        PathDrawer.LineTo([left, top])
        PathDrawer.LineToRelative([18, 0])
        PathDrawer.LineToRelative([0, 12])
        let right = PathDrawer.X;
        PathDrawer.LineToRelative([-3, 3])
        PathDrawer.LineTo([left, PathDrawer.Y])
        PathDrawer.LineTo([right, PathDrawer.Y + 8])
        PathDrawer.LineTo([PathDrawer.X, baseline])

        left = PathDrawer.X + smallSpacing;
        PathDrawer.MoveTo([left, top])
        PathDrawer.LineTo([left, baseline])
        PathDrawer.LineToRelative([17, 0])
        PathDrawer.LineTo([PathDrawer.X, top])


        left = PathDrawer.X + smallSpacing;
        function M() {
            PathDrawer.MoveTo([left, baseline])
            PathDrawer.LineTo([left, top])
            const mX = 9;
            const mY = 11;
            PathDrawer.LineToRelative([mX, mY])
            PathDrawer.LineToRelative([mX, -mY])
            PathDrawer.LineTo([PathDrawer.X, baseline])
        }
        M();

        // A
        left = gRight + 5;
        baseline = this.GPos[1] - 7
        top = baseline - smallLetterHeight
        const aX = 10;
        const aY = 19;
        // we draw the small segment of the A first so we can end on the far right
        const aWidthReduction = aX / smallLetterHeight * (smallLetterHeight - aY);
        PathDrawer.MoveTo([left + aWidthReduction, top + aY])
        PathDrawer.LineToRelative([aX * 2 - aWidthReduction * 2, 0])

        PathDrawer.MoveTo([left, baseline])
        PathDrawer.LineTo([PathDrawer.X + aX, top])
        PathDrawer.LineTo([PathDrawer.X + aX, baseline])

        left = PathDrawer.X + smallSpacing - 2;
        M();

        // E
        left = PathDrawer.X + smallSpacing;
        const eWidth = 13;
        const eWidth2 = 7;
        PathDrawer.MoveTo([left + eWidth, baseline])
        PathDrawer.LineToRelative([-eWidth, 0])
        PathDrawer.LineToRelative([0, -smallLetterHeight])
        PathDrawer.LineToRelative([eWidth, 0])
        PathDrawer.MoveTo([left, baseline - smallLetterHeight / 2])
        PathDrawer.LineToRelative([eWidth2, 0])

        context.stroke();
        // context.arc(50, 50, 20, 0, 3)
        // context.stroke();
    }

    AfterRemove() {
        super.AfterRemove();
        RemoveListener("newframe", this.Render)
    }


    constructor() {
        super();

        this.BackgroundCanvas = <canvas /> as HTMLCanvasElement
        this.Canvas = <canvas style={{ position: "absolute", left: "0" }} /> as HTMLCanvasElement

        const context = this.Canvas.getContext("2d");
        if (!context) throw new Error("Failed to get canvas context");
        this.Context = context;

        this.PathDrawer = new PathDrawer(this.Context);

        this.LogoBackground = new LogoBackground();

        this.HTMLElement = <div style={{ position: "relative" }}>
            {this.BackgroundCanvas}
            {this.Canvas}
        </div>
    }
}