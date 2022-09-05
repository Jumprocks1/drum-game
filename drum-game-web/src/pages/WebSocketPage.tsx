import DrumGameWebSocket from "../api/DrumGameWebSocket";
import PageComponent from "../framework/PageComponent";
import RouteButton from "../framework/RouteButton";
import MapSelectorPage from "./MapSelectorPage";

export default class WebSocketPage extends PageComponent {
    static Route = "/ws";

    AfterParent() {
        super.AfterParent();

        console.log("starting websocket");
        let socket: DrumGameWebSocket | undefined;
        const button = <button onclick={() => {
            if (!this.Parent) return;
            if (socket) {
                socket.Kill();
                socket = undefined;
                button.textContent = "Connect to WebSocket";
            } else {
                this.Add(socket = new DrumGameWebSocket())
                button.textContent = "Close WebSocket";
            }
        }}>
            Close WebSocket
        </button>
        this.Add(button);
        this.Add(<RouteButton page={MapSelectorPage}>
            Go to Drum Game web version
        </RouteButton>)
        this.Add(socket = new DrumGameWebSocket())
    }

    AfterRemove() {
        this.ChildrenAfterRemove(DrumGameWebSocket);
        super.AfterRemove();
    }
}