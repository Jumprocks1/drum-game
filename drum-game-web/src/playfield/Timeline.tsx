import Component from "../framework/Component";
import { StartDrag } from "../framework/Framework";
import { Clamp, FormatTime } from "../utils/Util";
import BeatmapPlayer from "./BeatmapPlayer";
import Track from "./Track";

export default class Timeline extends Component {

    Track?: Track;

    Timer = <div className="timer" />;
    Thumb = <div className="thumb" />
    Bar = <div className="bar">
        {this.Thumb}
    </div>

    Update() {
        const time = this.Track!.CurrentTime;
        const ratio = this.Track!.RatioAt(time)
        this.Timer.textContent = `${FormatTime(time)} / ${FormatTime(this.Track!.Duration)}`;
        this.Thumb.style.left = ratio * 100 + "%"
    }

    AfterParent() {
        super.AfterParent();

        this.Track = this.FindParent(BeatmapPlayer).Track;

        this.DOMNode = <div className="timeline">
            {this.Timer}
            <div className="bar-container">
                {this.Bar}
            </div>
        </div>

        this.HTMLElement.onmousedown = e => {
            if (e.button !== 0) return;
            const track = this.Track!;
            const wasPlaying = track.Playing;
            track.Playing = false;
            StartDrag(e, e => {
                const rect = this.Bar.getBoundingClientRect();
                const x = Clamp((e.clientX - rect.left) / rect.width, 0, 1);
                track.SeekToRatio(x)
            }, () => {
                if (wasPlaying) track.Playing = true;
            });
        }
    }
}