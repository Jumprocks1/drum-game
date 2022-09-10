import Component from "../framework/Component";
import { RegisterListener, RemoveListener } from "../framework/Framework";
import { CacheMap } from "../interfaces/Cache";
import { Clamp, ExpLerp, Filter } from "../utils/Util";
import BeatmapCard from "./BeatmapCard";
import Search from "./Search";


const circleY = 800;
const circleX = 350;

export default class MapCarousel extends Component { // could merge this back with VirtualizedContainer eventually

    ItemHeight: number
    Items: CacheMap[] = [];
    FilteredMaps: CacheMap[] = []

    TotalHeight: number = 0;

    Renderer: new (item: CacheMap) => BeatmapCard;

    Free: BeatmapCard[] = [];

    Active: Set<BeatmapCard> = new Set();
    ActiveMap: Map<CacheMap, BeatmapCard> = new Map();

    private _selectedIndex: number = 0;

    Dragging = false;

    get SelectedIndex() { return this._selectedIndex; }
    set SelectedIndex(value: number) {
        this.TargetScroll = value * this.ItemHeight;
        this._selectedIndex = Clamp(value, 0, this.Items.length - 1);
    }
    get SelectedMap() { return this.Items[this._selectedIndex] }

    get SelectedMapPosition() { return this._selectedIndex * this.ItemHeight; }

    TargetScroll: number = 0;
    CurrentScroll: number = 0; // this is like scrollTop
    UserTargetScroll: number = 0;

    Search = new Search();

    Render(item: CacheMap) {
        if (this.Free.length > 0) {
            const pop = this.Free.pop()!;
            pop.SetContent(item);
            return pop;
        }
        return new this.Renderer(item);
    }

    constructor(renderer: new (item: CacheMap) => BeatmapCard, itemHeight: number) {
        super();
        this.Renderer = renderer;
        this.ItemHeight = itemHeight;
        this.HTMLElement = <div className="map-selector" />
        this.Search.OnChange = this.OnSearch;

        this.Add(this.Search);
    }

    OnSearch(value: string) {
        this.FilteredMaps = Filter(value, this.Items);
    }

    AfterRemove() {
        super.AfterRemove()
        RemoveListener("newframe", this.UpdateScroll)
        RemoveListener("wheel", this.OnWheel);
    }

    AfterDOM() {
        super.AfterDOM();
        RegisterListener("newframe", this.UpdateScroll);
        RegisterListener("wheel", this.OnWheel);
        this.Update();
    }

    UpdateScroll = (dt: number) => {
        const oldScroll = this.CurrentScroll;

        if (!this.Dragging) {
            // pulls the target to the nearest card
            this.TargetScroll = ExpLerp(this.TargetScroll, this.SelectedMapPosition, 0.99, dt, 0.01);
        }
        const newScroll = ExpLerp(this.CurrentScroll, this.TargetScroll, 0.99, dt, 0.02)
        if (newScroll === oldScroll) return;
        this.CurrentScroll = newScroll;
        this.Update();
    }

    OnWheel = (e: WheelEvent) => {
        this.SelectedIndex += Math.sign(e.deltaY)
    }

    OnKeyDown = (e: KeyboardEvent) => {
        if (e.key === "Home" && e.ctrlKey)
            this.SelectedIndex = 0;
        else if (e.key === "End" && e.ctrlKey)
            this.SelectedIndex = this.Items.length - 1;
        else if (e.key === "PageUp")
            this.SelectedIndex -= Math.round(this.HTMLElement.clientHeight / this.ItemHeight);
        else if (e.key === "PageDown")
            this.SelectedIndex += Math.round(this.HTMLElement.clientHeight / this.ItemHeight);
        else if (e.key === "ArrowDown")
            this.SelectedIndex += 1;
        else if (e.key === "ArrowUp")
            this.SelectedIndex -= 1;
    }

    Select(e: CacheMap) {
        this.SelectedIndex = this.Items.indexOf(e);
    }

    Update() {
        const clientHeight = this.HTMLElement.clientHeight;
        const itemHeight = this.ItemHeight;
        const scroll = this.CurrentScroll;

        const visibleStart = this.CurrentScroll - (clientHeight - itemHeight) * 0.5;
        const visibleEnd = visibleStart + clientHeight;

        const renderStart = Math.max(0, Math.floor(visibleStart / itemHeight));
        const renderEnd = Math.min(this.Items.length, Math.ceil(visibleEnd / itemHeight)) // exclusive

        const selected = this.SelectedIndex;

        const pendingFree = this.Active; // tentatively free all active renderers
        let newActive = new Set<BeatmapCard>();
        for (let i = renderStart; i < renderEnd; i++) {
            const e = this.Items[i];
            const renderer = this.ActiveMap.get(e);
            if (renderer) {
                renderer.Selected = i === selected;

                const y = i * itemHeight - scroll;
                renderer.HTMLElement.style.top = y + (clientHeight - itemHeight) * 0.5 + "px";

                const clampedY = Clamp(y / circleY, -1, 1);
                renderer.HTMLElement.style.right = ((Math.cos(clampedY * Math.PI / 2) - 1) * circleX) + "px";

                newActive.add(renderer);
                pendingFree.delete(renderer);
            }
        }
        for (const e of pendingFree) {
            e.Kill();
            this.ActiveMap.delete(e.CurrentItem);
            this.Free.push(e);
        }

        for (let i = renderStart; i < renderEnd; i++) {
            const e = this.Items[i];
            if (!this.ActiveMap.get(e)) {
                const renderer = this.Render(e);
                renderer.Selected = i === selected;

                const y = i * itemHeight - scroll;
                renderer.HTMLElement.style.top = y + (clientHeight - itemHeight) * 0.5 + "px";

                const clampedY = Clamp(y / circleY, -1, 1);
                renderer.HTMLElement.style.right = ((Math.cos(clampedY * Math.PI / 2) - 1) * circleX) + "px";


                this.ActiveMap.set(e, renderer);
                this.Add(renderer);
                newActive.add(renderer);
            }
        }

        this.Active = newActive;
    }

    OnPageResize = () => {
        this.Update();
    }

    SetItems(items: CacheMap[]) {
        this.Items = items;
        this.OnSearch(this.Search.Value);
        if (this.Alive) this.Update();
    }
}