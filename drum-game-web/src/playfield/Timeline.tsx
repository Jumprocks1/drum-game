import Component from "../framework/Component";
import { StartDrag } from "../framework/Framework";
import { Clamp, FormatTime } from "../utils/Util";
import BeatmapPlayer from "./BeatmapPlayer";

export default class Timeline extends Component {

    Player?: BeatmapPlayer;

    Timer = <div className="timer" />;
    Thumb = <div className="thumb" />
    Bar = <div className="bar">
        {this.Thumb}
    </div>

    Update() {
        const time = this.Player!.CurrentTime;
        const ratio = Math.max(Math.min(time / this.Player!.Duration, 1), 0);
        this.Timer.textContent = `${FormatTime(time)} / ${FormatTime(this.Player!.Duration)}`;
        this.Thumb.style.left = ratio * 100 + "%"
    }


    AfterParent() {
        super.AfterParent();

        this.Player = this.FindParent(BeatmapPlayer);

        this.DOMNode = <div className="timeline">
            {this.Timer}
            <div className="bar-container">
                {this.Bar}
            </div>
        </div>

        this.HTMLElement.onmousedown = e => {
            if (e.button !== 0) return;
            const player = this.Player!;
            const wasPlaying = player.Playing;
            player.Playing = false;
            StartDrag(e, e => {
                const rect = this.Bar.getBoundingClientRect();
                const x = Clamp((e.clientX - rect.left) / rect.width, 0, 1);
                player.CurrentTime = x * player.Duration;
            }, () => {
                if (wasPlaying) player.Playing = true;
            });
        }
    }
}