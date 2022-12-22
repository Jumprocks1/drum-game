import YouTube from "../../api/YouTube";
import Component from "../../framework/Component";
import { RegisterListener, RemoveListener, StartDrag } from "../../framework/Framework";
import GlobalData from "../../GlobalData";
import SMuFL from "../../interfaces/SMuFL";
import Beatmap from "../../utils/Beatmap";
import ChannelInfo from "../../utils/ChannelInfo";
import BeatmapPlayer from "../BeatmapPlayer";
import Timeline from "../Timeline";
import YouTubeTrack from "../YouTubeTrack";
import RenderGroup from "./RenderGroup";

export default class NotationDisplay extends Component {
    Player: BeatmapPlayer
    get Track() { return this.Player.Track; }
    Beatmap: Beatmap

    Spacing = 5

    Canvas: HTMLCanvasElement
    Context: CanvasRenderingContext2D
    Timeline: Timeline

    RenderGroups: RenderGroup[]

    CursorInset = 4; // beats

    DrawMeasureLines = true;

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
        const pixelRatio = window.devicePixelRatio;
        this.Context.scale(pixelRatio, pixelRatio);
        // offset down to allow stems to go above the staff without clipping
        this.Context.translate(0, this.StaffHeight * NotationDisplay.StaffPadding);

        this.Context.scale(this.StaffHeight / 4, this.StaffHeight / 4)

        const currentBeat = this.Track.CurrentBeat;

        this.Context.translate((this.CursorInset - currentBeat) * this.Spacing, 0);
    }

    CheckSize(reinit: boolean) {
        const pixelRatio = window.devicePixelRatio;
        const targetHeightBase = this.StaffHeight * (1 + NotationDisplay.StaffPadding * 2);
        const targetHeight = targetHeightBase * pixelRatio;
        const targetWidth = this.Canvas.clientWidth * pixelRatio;

        if (this.Canvas.width !== targetWidth || this.Canvas.height !== targetHeight) {
            this.Canvas.style.height = targetHeightBase + "px";
            this.Canvas.height = targetHeight;
            this.Canvas.width = targetWidth;
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
        this.Track.Update();
        this.Timeline.Update();

        this.CheckSize(true);

        const w = this.Canvas.width;
        const scale = this.StaffHeight / 4 * window.devicePixelRatio;

        this.Context.clearRect(0, 0, w, this.Canvas.height);

        this.Context.save();

        this.Transform();


        const lineThickness = this.Font!.engravingDefaults.staffLineThickness;
        for (let i = 0; i < 5; i++) // idk if we need to clip this before drawing
            this.Context.fillRect(0, i - lineThickness / 2, this.Beatmap.Length * this.Spacing, lineThickness);

        const visibleStart = this.Track.CurrentBeat - this.CursorInset;
        const visibleBeats = w / scale / this.Spacing;
        // in beats, we have to overdraw since some notes will render past their normal "bounds"
        // there are extreme cases with MeasureChanges that can cause a lot of overdraw. 1 beat should be enough
        const overdraw = 1;

        const overdrawStart = visibleStart - overdraw;
        const overdrawEnd = visibleStart + visibleBeats + overdraw;

        const firstGroup = Math.max(0, Math.floor((overdrawStart) / 4));
        const lastGroup = Math.min(Math.floor(overdrawEnd / 4) + 1, this.RenderGroups.length - 1);
        // console.log(firstGroup + " : " + lastGroup)
        for (let i = firstGroup; i < lastGroup; i++)
            this.RenderGroups[i].Render(this, this.Context);

        this.Context.fillStyle = "rgba(100, 149, 237, 0.5)"
        this.Context.fillRect(this.Track.CurrentBeat * this.Spacing - 0.25, -2, 0.5, 8)

        if (this.DrawMeasureLines) { // this only works for 4/4 maps
            // draw measure lines
            const firstMeasureLine = Math.max(0, Math.ceil(overdrawStart / 4));
            const lastMeasureLineInclusive = Math.floor(overdrawEnd / 4);
            this.Context.fillStyle = "rgba(70, 110, 160, 0.4)"
            for (let i = firstMeasureLine; i <= lastMeasureLineInclusive; i++) {
                this.Context.fillRect(i * 4 * this.Spacing - 0.25, -2, 0.5, 8)
            }
        }

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
            <div id="title">{this.Beatmap.BJson.artist} - {this.Beatmap.BJson.title}</div>
            {this.Canvas}
            <div id="visualContainer">
                <div className="wip">Drum Game Web is still a work in progress.<br />
                    For the full experience, <a href="https://github.com/Jumprocks1/drum-game/releases">download the desktop version</a>.</div>
            </div>
        </div>

        if (this.Track instanceof YouTubeTrack) {
            const youTube = this.Track.YouTube;
            this.Add(youTube, false);
            this.HTMLElement.querySelector("#visualContainer")?.appendChild(youTube.HTMLElement);
        }

        this.HTMLElement.onmousedown = e => {
            if (e.button !== 1 && e.button !== 0) return;
            const track = this.Track;
            const wasPlaying = track.Playing;
            track.Playing = false;
            const gripPoint = e.clientX;
            const startTime = track.CurrentTime;
            StartDrag(e, e => {
                const d = e.clientX - gripPoint;
                track.CurrentTime = this.Beatmap.BeatToMs(this.Beatmap.MsToBeat(startTime) - d * 4 / this.Spacing / this.StaffHeight)
            }, () => {
                if (wasPlaying) track.Playing = true;
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