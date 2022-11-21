import Component from "../framework/Component";

export default class Search extends Component {
    UpdateNumbers(showing: number, total: number) {
        this.MapCount.textContent = `Showing ${showing} of ${total} maps`
    }

    Input = <input placeholder="Type to search..." /> as HTMLInputElement;
    MapCount = <div id="map-count"></div>


    constructor(value?: string) {
        super();
        this.UpdateNumbers(0, 0)
        this.HTMLElement = <div className="search">
            {this.Input}
            {this.MapCount}
        </div>
        if (value) this.Input.value = value;
        this.Input.oninput = (e) => {
            this.OnChange?.(this.Input.value)
        }
        this.Input.onmousedown = (e) => {
            this.Input.focus();
            e.stopPropagation();
        }
    }

    OnChange?: (value: string) => void

    OnKeyDown = (e: KeyboardEvent) => {
        if (e.ctrlKey) return;
        if (e.key.length === 1 || e.key === "Backspace") {
            if (!document.activeElement || document.activeElement === document.body) {
                this.Input.focus();
                return true;
            }
        }
    }

    get Value() { return this.Input.value; }
}