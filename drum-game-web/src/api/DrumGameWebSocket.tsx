import Component from "../framework/Component";
import YouTube, { LoadYouTubeApi } from "./YouTube";

const genRandomHex = (size: number) => [...Array(size)].map(() => Math.floor(Math.random() * 16).toString(16)).join('');

export default class DrumGameWebSocket extends Component {
    Id = genRandomHex(8)

    Connection: WebSocket
    YouTube?: YouTube

    get Player() {
        return this.YouTube?.Player;
    }

    constructor(uri: string = "ws://127.0.0.1:412/") {
        super();
        const message = <div />
        this.HTMLElement = <div>{message}</div>
        message.textContent = "Connecting..."


        const connection = new WebSocket(uri);
        this.Connection = connection;
        LoadYouTubeApi(); // start loading YouTube script ahead of time


        this.Connection.onopen = () => {
            message.textContent = "WebSocket Connected"
            this.Connection.onmessage = e => {
                const message = JSON.parse(e.data) as DrumGameWebSocketMessage;
                console.log(message);
                const command = message.command;
                youTube.Player.then(player => {
                    if (command === "navigate") {
                        player.cueVideoById(message.videoId)
                        player.pauseVideo();
                    } else if (command == "start") {
                        player.playVideo()
                    } else if (command == "stop") {
                        player.pauseVideo()
                    } else if (command === "volume") {
                        player.setVolume(message.volume)
                    } else if (command == "seek") {
                        player.seekTo(message.position, message.hard)
                    }
                });
            }
            const youTube = new YouTube()
            this.Add(this.YouTube = youTube);
            youTube.Player.then(player => {
                player.getIframe().style.width = "100%";
                let sentId = ""; // current videoId we think DrumGame has
                let sentTime = 0;
                let sentState = -2;


                let interval: any = undefined;
                function updateVideoId(e: YT.OnStateChangeEvent) {
                    const length = player.getDuration();
                    if (length === 0) return;
                    const stateId = player.getPlayerState();
                    const match = player.getVideoUrl().match(/v=([0-9a-zA-Z_-]{11})/);
                    const videoId = match ? match[1] : ""
                    if (sentId !== videoId || sentState !== stateId) {
                        const buffer = new ArrayBuffer(21);
                        const view = new DataView(buffer);
                        view.setUint8(0, 1); // first byte 1 => videoId message
                        new TextEncoder().encodeInto(videoId, new Uint8Array(buffer, 1, 11)) // write video id
                        view.setFloat64(12, length, true);
                        view.setInt8(20, stateId);
                        connection.send(buffer);

                        sentId = videoId;
                        sentState = stateId;
                        sentTime = -1;
                    }
                }
                player.addEventListener("onStateChange", updateVideoId)

                function updateTime() {
                    if (connection.readyState !== WebSocket.OPEN) {
                        clearInterval(interval)
                        return;
                    }
                    const time = player.getCurrentTime();
                    if (time !== undefined && time != sentTime) {
                        const buffer = new ArrayBuffer(9);
                        const view = new DataView(buffer);
                        view.setUint8(0, 2); // first byte 2 => time message
                        view.setFloat64(1, time, true);
                        connection.send(buffer);
                        sentTime = time;
                    }
                }
                interval = setInterval(updateTime, 0);
            })
        };
        this.Connection.onclose = () => {
            this.HTMLElement.textContent = "WebSocket Closed"
            console.log("WebSocket closed");
        }
        this.Connection.onerror = console.error;
    }

    AfterRemove() {
        this.Connection.close();
        this.ChildrenAfterRemove();
        super.AfterRemove();
    }
}

interface NavigateMessage {
    command: "navigate"
    videoId: string
}
interface StartMessage {
    command: "start"
    videoId: string
    position?: number
}
interface StopMessage {
    command: "stop"
    videoId: string
}

type DrumGameWebSocketMessage = NavigateMessage | StartMessage | StopMessage
    | { command: "volume", volume: number }
    | { command: "seek"; videoId: string; position: number; hard: boolean }