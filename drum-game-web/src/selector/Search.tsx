import Component from "../framework/Component";

export default class Search extends Component {
    UpdateNumbers(showing: number, total: number) {
        this.MapCount.textContent = `Showing ${showing} of ${total} maps`
    }

    Input = <input placeholder="Type to search..." name="search" /> as HTMLInputElement;
    MapCount = <div id="map-count"></div>


    constructor(value?: string) {
        super();
        this.UpdateNumbers(0, 0)
        const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg")
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("viewbox", "0 0 24 24")
        const path = document.createElementNS("http://www.w3.org/2000/svg", "path")
        path.setAttribute("d", "M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z")
        svg.appendChild(path)
        const clearButton = <div id="clear-button">
            {svg}
        </div>
        this.HTMLElement = <div className="search">
            <div className="search-row-1" title="Clear">
                {this.Input}
                {clearButton}
            </div>
            {this.MapCount}
        </div>
        clearButton.onclick = () => this.ClearAndFocus();
        if (value) this.Input.value = value;

        clearButton.style.display = this.Input.value ? "" : "none"

        this.Input.oninput = (e) => {
            clearButton.style.display = this.Input.value ? "" : "none"
            this.OnChange?.(this.Input.value)
        }
        this.Input.onmousedown = (e) => {
            this.Input.focus();
            e.stopPropagation();
        }
    }

    ClearAndFocus() {
        this.Input.value = ""
        this.Input.focus()
        this.Input.dispatchEvent(new Event("input"))
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