import Component from "../../framework/Component";
import { RegisterListener, RemoveListener, StartDrag } from "../../framework/Framework";
import GlobalData from "../../GlobalData";
import SMuFL from "../../interfaces/SMuFL";
import Beatmap from "../../utils/Beatmap";
import ChannelInfo from "../../utils/ChannelInfo";
import BeatmapPlayer from "../BeatmapPlayer";
import Timeline from "../Timeline";
import RenderGroup from "./RenderGroup";

export default class NotationDisplay extends Component {
    Player: BeatmapPlayer
    Beatmap: Beatmap

    Spacing = 5

    Canvas: HTMLCanvasElement
    Context: CanvasRenderingContext2D
    Timeline: Timeline

    RenderGroups: RenderGroup[]

    // @ts-ignore
    Font: SMuFL

    CanvasLoaded = false;

    StaffHeight = 100; // in real display pixels. This is what is used to set all scaling factors
    static readonly StaffPadding = 1.5; // fraction of staff height, applied to top and bottom (total padding is this * 2)

    InitCanvas() {
        this.InitContext();
        this.CanvasLoaded = true;
        // console.log(this.Bravura);
    }

    Transform() {
        this.Context.resetTransform();
        // offset down to allow stems to go above the staff without clipping
        this.Context.translate(0, this.StaffHeight * NotationDisplay.StaffPadding);

        this.Context.scale(this.StaffHeight / 4, this.StaffHeight / 4)

        const currentBeat = this.Player.CurrentBeat;

        this.Context.translate(-currentBeat * this.Spacing, 0);
    }

    CheckSize(reinit: boolean) {
        const targetHeight = this.StaffHeight * (1 + NotationDisplay.StaffPadding * 2);
        // const pixelRatio = window.devicePixelRatio;
        if (this.Canvas.width !== this.Canvas.clientWidth || this.Canvas.height !== targetHeight) {
            this.Canvas.style.height = targetHeight + "px";
            this.Canvas.height = targetHeight;
            this.Canvas.width = this.Canvas.clientWidth;
            if (reinit) this.InitContext(); // changing size resets all Context parameters
        }
    }

    InitContext() {
        console.log("initializing canvas context");
        this.CheckSize(false);
        // call this whenever the size changes

        // px is the height of the staff in our scaled units.
        // 4 is ideal since a lot of units are expressed in "staff spaces"
        this.Context.font = "4px Bravura"
    }

    Render = () => {
        if (!this.CanvasLoaded) return;
        this.Player.Update();
        this.Timeline.Update();

        this.CheckSize(true);

        const w = this.Canvas.width;
        const scale = this.StaffHeight / 4;

        this.Context.clearRect(0, 0, w, this.Canvas.height);

        this.Context.save();

        this.Transform();


        const lineThickness = this.Font!.engravingDefaults.staffLineThickness;
        for (let i = 0; i < 5; i++) // idk if we need to clip this before drawing
            this.Context.fillRect(0, i - lineThickness / 2, this.Beatmap.Length * this.Spacing, lineThickness);

        const visbileStart = this.Player.CurrentBeat; // we will need to subtract the beat inset here
        const visibleBeats = w / scale / this.Spacing;
        // in beats, we have to overdraw since some notes will render past their normal "bounds"
        // there are extreme cases with MeasureChanges that can cause a lot of overdraw. 1 beat should be enough
        const overdraw = 1;

        const firstGroup = Math.max(0, Math.floor((visbileStart - overdraw) / 4));
        const lastGroup = Math.floor((visbileStart + visibleBeats + overdraw) / 4) + 1;
        // console.log(firstGroup + " : " + lastGroup)
        for (let i = firstGroup; i < lastGroup; i++)
            this.RenderGroups[i].Render(this, this.Context);

        this.Context.restore();
    }

    AfterRemove() {
        super.AfterRemove();
        RemoveListener("newframe", this.Render)
    }

    AfterParent() {
        super.AfterParent();
        RegisterListener("newframe", this.Render);
        this.Add(this.Timeline);
    }

    constructor(player: BeatmapPlayer) {
        super();
        this.Player = player;
        this.Beatmap = player.Beatmap;

        this.Canvas = <canvas /> as HTMLCanvasElement

        const context = this.Canvas.getContext("2d");
        if (!context) throw new Error("Failed to get canvas context");
        this.Context = context;

        const metadata = GlobalData.LoadBravura();

        this.RenderGroups = RenderGroup.BuildRenderGroups(this.Beatmap);

        metadata.then(bravura => {
            this.Font = bravura;
            this.InitCanvas();
        })

        this.HTMLElement = <div className="notation-display">
            <div>{this.Beatmap.BJson.title}</div>
            <div>{this.Beatmap.BJson.mapper}</div>
            <div>{this.Beatmap.BJson.difficulty}</div>
            {this.Canvas}
            <div className="spacer" />
        </div>

        this.HTMLElement.onmousedown = e => {
            if (e.button !== 1) return;
            const player = this.Player;
            const wasPlaying = player.Playing;
            player.Playing = false;
            const gripPoint = e.clientX;
            const startTime = player.CurrentTime;
            StartDrag(e, e => {
                const d = e.clientX - gripPoint;
                player.CurrentTime = this.Beatmap.BeatToMs(this.Beatmap.MsToBeat(startTime) - d * 4 / this.Spacing / this.StaffHeight)
            }, () => {
                if (wasPlaying) player.Playing = true;
            });
        }

        this.Timeline = new Timeline();
    }

    static AnchorCache: { [key: string]: [number, number] } = {};
    GetNoteheadAnchor(codepoint: string, down: boolean) {
        const info = this.Font.glyphsWithAnchors[ChannelInfo.CodepointMap[codepoint]];
        return down ? info.stemDownNW : info.stemUpSE
    }
}