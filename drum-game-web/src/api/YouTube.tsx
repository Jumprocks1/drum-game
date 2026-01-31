import Component from "../framework/Component";

let api: Promise<void> | undefined;

export function LoadYouTubeApi() {
    if (api) return api;
    const tag = document.createElement('script');
    tag.src = "https://www.youtube.com/iframe_api";
    const firstScriptTag = document.getElementsByTagName('script')[0]!;
    firstScriptTag.parentNode!.insertBefore(tag, firstScriptTag);
    return api = new Promise<void>(e => {
        // @ts-ignore
        window.onYouTubeIframeAPIReady = e;
    })
}

export async function loadVideo(id: string) {
    await LoadYouTubeApi();

    return new Promise<YT.Player>(res => {
        const player: YT.Player = new YT.Player(id, {
            height: '720',
            width: '1280',
            host: 'https://www.youtube-nocookie.com',
            playerVars: {
                playsinline: 1,
                // we can also hide a lot of fluff from YouTube such as showing videos at the end
            },
            events: {
                onReady: () => res(player),
            }
        });
    });
}

export default class YouTube extends Component {

    Player: Promise<YT.Player>;

    constructor() {
        super();

        this.HTMLElement = <div id="youtube-target" />

        this.Player = loadVideo("youtube-target");
    }

    async WaitForState(state: YT.PlayerState) {
        const player = await this.Player;
        return new Promise<YT.Player>(res => {
            const handler = (e: YT.OnStateChangeEvent) => {
                if (e.data === state) {
                    player.removeEventListener("onStateChange", handler)
                    res(player);
                }
            }
            player.addEventListener("onStateChange", handler);
        })
    }

    AfterRemove() {
        super.AfterRemove();
        this.Player.then(e => e.destroy());
    }
}