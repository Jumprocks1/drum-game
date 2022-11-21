import YouTube from "../api/YouTube";
import Beatmap from "../utils/Beatmap";
import Track from "./Track";

export default class YouTubeTrack extends Track {
    private startTime = new Date().getTime();
    private lastUpdate = this.startTime;


    YouTube = new YouTube();


    private _currentTime = 0;

    constructor(beatmap: Beatmap) {
        super(beatmap);
        this.Duration = this.Beatmap.BeatToMs(this.Beatmap.Length);
        this.LeadIn = this.Beatmap.BJson.leadIn ?? 0;
        this.CurrentTime = -this.LeadIn
        const youTube = this.YouTube;
        const track = this;
        youTube.Player.then(player => {
            if (!youTube.Alive) return;
            player.cueVideoById(this.Beatmap.BJson.youTubeID!);


            function updateState(e: YT.OnStateChangeEvent) {
                const length = player.getDuration();
                if (length === 0) return;
                const stateId = player.getPlayerState();
                track.Playing = stateId === 1;
            }

            player.addEventListener("onStateChange", updateState)

            let interval: any = undefined;
            function updateTime() {
                if (!youTube.Alive) {
                    clearInterval(interval)
                    return;
                }
                if (track.Playing)
                    track.CurrentTime = player.getCurrentTime() * 1000;
            }
            interval = setInterval(updateTime, 0);
        });
    }

    Update() {
        if (this.Playing)
            this.CurrentTime += (new Date().getTime() - this.lastUpdate)
        this.lastUpdate = new Date().getTime();
    }

    set CurrentTime(value: number) {
        this._currentTime = value;
        this._currentBeat = this.Beatmap.MsToBeat(this._currentTime);
    }

    get CurrentTime() {
        return this._currentTime
    }
}