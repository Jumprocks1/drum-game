import Component from "./Component";

interface Renderer<T> extends Component {
    SetContent: (content: T) => void
    CurrentItem: T
}

export default class VirtualizedContainer<T> extends Component {

    ItemHeight: number // could make this a function, but everything is soooo much better with constant height
    Items: T[] = [];

    TotalHeight: number = 0;

    Renderer: new (item: T) => Renderer<T>;

    InnerContainer = <div />

    get InnerNode() { return this.InnerContainer; }


    Free: Renderer<T>[] = [];

    Active: Set<Renderer<T>> = new Set();
    ActiveMap: Map<T, Renderer<T>> = new Map();

    Render(item: T) {
        if (this.Free.length > 0) {
            const pop = this.Free.pop()!;
            pop.SetContent(item);
            return pop;
        }
        return new this.Renderer(item);
    }

    constructor(renderer: new (item: T) => Renderer<T>, itemHeight: number) {
        super();
        this.Renderer = renderer;
        this.ItemHeight = itemHeight;
        this.HTMLElement = <div className="virtualized-container">
            {this.InnerContainer}
        </div>
    }

    AfterRemove() {
        super.AfterRemove()
        this.HTMLElement.removeEventListener("scroll", this.OnScroll)
    }

    AfterDOM() {
        super.AfterDOM();
        this.HTMLElement.addEventListener("scroll", this.OnScroll)
        this.Update();
    }

    OnScroll = () => {
        this.Update();
    }

    Update() {
        const parentHeight = this.HTMLElement.parentElement!.clientHeight;
        const itemHeight = this.ItemHeight;
        let computedHeight = this.Items.length * this.ItemHeight
        if (this.TotalHeight !== computedHeight) {
            this.TotalHeight = computedHeight;
            this.InnerContainer.style.height = computedHeight + "px";
        }


        const visibleStart = this.HTMLElement.scrollTop;
        const visibleEnd = visibleStart + this.HTMLElement.clientHeight;

        const renderStart = Math.max(0, Math.floor(visibleStart / itemHeight));
        const renderEnd = Math.min(this.Items.length, Math.ceil(visibleEnd / itemHeight)) // exclusive

        const pendingFree = this.Active; // tentatively free all active renderers
        let newActive = new Set<Renderer<T>>();
        for (let i = renderStart; i < renderEnd; i++) {
            const e = this.Items[i];
            const active = this.ActiveMap.get(e);
            if (active) {
                active.HTMLElement.style.top = i * itemHeight + "px";
                newActive.add(active);
                pendingFree.delete(active);
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
                renderer.HTMLElement.style.top = i * itemHeight + "px";
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

    SetItems(items: T[]) {
        this.Items = items;
        if (this.Alive) this.Update();
    }
}