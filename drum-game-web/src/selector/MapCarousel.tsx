import Component from "../framework/Component";
import { BindTouchEvents, RegisterListener, RemoveListener, StartDrag } from "../framework/Framework";
import { CacheMap } from "../interfaces/Cache";
import MapSelectorPage from "../pages/MapSelectorPage";
import { Clamp, EnsureParent, ExpLerp, Filter } from "../utils/Util";
import BeatmapCard from "./BeatmapCard";
import Search from "./Search";

// stored globally so we can restore it later
export const CarouselState: { search: string, map: string | undefined } = {
    search: "",
    map: undefined
}


const circleY = 800;
const circleX = 350;

export default class MapCarousel extends Component { // could merge this back with VirtualizedContainer eventually

    ItemHeight: number = 106
    Items: CacheMap[] = [];
    FilteredMaps: CacheMap[] = []

    TotalHeight: number = 0;

    Free: BeatmapCard[] = [];

    Active: Set<BeatmapCard> = new Set();
    ActiveMap: Map<CacheMap, BeatmapCard> = new Map();

    private _selectedIndex: number = Number.NaN;

    Dragging = false;

    OnMapChange = (map: CacheMap) => { }

    get SelectedIndex() { return this._selectedIndex; }
    set SelectedIndex(value: number) {
        if (!this.Dragging)
            this.TargetScroll = value * this.ItemHeight;
        this._selectedIndex = Clamp(value, 0, this.FilteredMaps.length - 1);
        CarouselState.map = this.FilteredMaps[this._selectedIndex]?.Id
        this.OnMapChange(this.FilteredMaps[this._selectedIndex])
    }
    get SelectedMap(): CacheMap | undefined { return this.FilteredMaps[this._selectedIndex] }

    get SelectedMapPosition() { return this._selectedIndex * this.ItemHeight; }

    TargetScroll: number = 0;
    CurrentScroll: number = 0; // this is like scrollTop
    _loadedScroll: number = Number.NaN;

    Search = new Search(CarouselState.search);

    Render(item: CacheMap) {
        if (this.Free.length > 0) {
            const pop = this.Free.pop()!;
            pop.SetContent(item);
            return pop;
        }
        return new BeatmapCard(item);
    }

    constructor() {
        super();
        this.HTMLElement = <div id="map-selector" />
        this.HTMLElement.onmousedown = e => { // TODO this makes it so you can't search on mobile D:
            if (e.button !== 1 && e.button !== 0) return;
            const gripPoint = e.clientY;
            const initialScroll = this.CurrentScroll;
            StartDrag(e, e => {
                this.Dragging = true;
                const d = e.clientY - gripPoint;
                this.TargetScroll = initialScroll - d;
            }, () => {
                this.Dragging = false;
            }, 20, e => {
                const card = (e.target as HTMLElement).closest(".beatmap-card-wrapper");
                if (card) {
                    (card as any).Component.Click();
                }
            });
        }
        BindTouchEvents(this.HTMLElement)

        this.Search.OnChange = this.OnSearch;

        this.Add(this.Search);
    }

    OnSearch = (value: string) => {
        CarouselState.search = value;
        const selected = this.SelectedMap;
        this.FilteredMaps = Filter(value, this.Items);
        this.Search.UpdateNumbers(this.FilteredMaps.length, this.Items.length);
        let newIndex = -1;
        const target = CarouselState.map;
        if (Number.isNaN(this.SelectedIndex) && target)
            newIndex = this.FilteredMaps.findIndex(e => e.Id === target);
        if (newIndex === -1 && selected)
            newIndex = this.FilteredMaps.indexOf(selected);
        if (newIndex === -1)
            newIndex = Math.floor(Math.random() * this.FilteredMaps.length);
        this.HardPull(newIndex);
        this.Update();
    }

    HardPull(index: number) {
        this.SelectedIndex = index;
        this.CurrentScroll = this.SelectedMapPosition;
    }

    AfterRemove() {
        super.AfterRemove()
        this.Search.AfterRemove();
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
        if (!this.Dragging) {
            // pulls the target to the nearest card
            this.TargetScroll = ExpLerp(this.TargetScroll, this.SelectedMapPosition, 0.99, dt, 0.01);
        } else {
            this.SelectedIndex = Math.round(this.TargetScroll / this.ItemHeight);
        }
        const newScroll = ExpLerp(this.CurrentScroll, this.TargetScroll, 0.99, dt, 0.02)
        this.CurrentScroll = newScroll;
        if (this.CurrentScroll === this._loadedScroll) return;
        this.Update();
    }

    OnWheel = (e: WheelEvent) => {
        this.SelectedIndex += Math.sign(e.deltaY)
    }

    OnKeyDown = (e: KeyboardEvent) => {
        if (e.key === "Enter")
            this.FindParent(MapSelectorPage).LoadMap(this.SelectedMap)
        else if (e.key === "Home" && e.ctrlKey)
            this.SelectedIndex = 0;
        else if (e.key === "End" && e.ctrlKey)
            this.SelectedIndex = this.FilteredMaps.length - 1;
        else if (e.key === "PageUp")
            this.SelectedIndex -= Math.round(this.HTMLElement.clientHeight / this.ItemHeight / 2);
        else if (e.key === "PageDown")
            this.SelectedIndex += Math.round(this.HTMLElement.clientHeight / this.ItemHeight / 2);
        else if (e.key === "ArrowDown")
            this.SelectedIndex += 1;
        else if (e.key === "ArrowUp")
            this.SelectedIndex -= 1;
        else if (e.key === "F2")
            this.SelectedIndex = Math.floor(Math.random() * this.FilteredMaps.length);
        else return;
        e.preventDefault();
    }

    Select(e: CacheMap) {
        this.SelectedIndex = this.FilteredMaps.indexOf(e);
    }
    HardSelect(e: CacheMap) {
        this.HardPull(this.FilteredMaps.indexOf(e));
    }

    Update() {
        this._loadedScroll = this.CurrentScroll;
        const clientHeight = this.HTMLElement.clientHeight;
        const itemHeight = this.ItemHeight;
        const scroll = this.CurrentScroll;

        const visibleStart = this.CurrentScroll - (clientHeight - itemHeight) * 0.5;
        const visibleEnd = visibleStart + clientHeight;

        const renderStart = Math.max(0, Math.floor(visibleStart / itemHeight));
        const renderEnd = Math.min(this.FilteredMaps.length, Math.ceil(visibleEnd / itemHeight)) // exclusive

        const selected = this.SelectedIndex;

        const pendingFree = this.Active; // tentatively free all active renderers
        let newActive = new Set<BeatmapCard>();
        for (let i = renderStart; i < renderEnd; i++) {
            const e = this.FilteredMaps[i];
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
            const e = this.FilteredMaps[i];
            if (!this.ActiveMap.get(e)) {
                const newItem = this.Render(e);
                newItem.Selected = i === selected;

                const y = i * itemHeight - scroll;
                newItem.HTMLElement.style.top = y + (clientHeight - itemHeight) * 0.5 + "px";

                const clampedY = Clamp(y / circleY, -1, 1);
                newItem.HTMLElement.style.right = ((Math.cos(clampedY * Math.PI / 2) - 1) * circleX) + "px";


                this.ActiveMap.set(e, newItem);
                this.Add(newItem);
                newActive.add(newItem);
            }
        }

        this.Active = newActive;

        EnsureParent(this.HTMLElement, this.NoMaps, this.FilteredMaps.length === 0)
    }

    NoMaps = <div id="no-maps">Loading...</div>

    OnPageResize = () => {
        this.Update();
    }

    SetItems(items: CacheMap[]) {
        this.NoMaps.textContent = "No maps found";
        this.Items = items;
        this.OnSearch(this.Search.Value);
        if (this.Alive) this.Update();
    }
}