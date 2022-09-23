import Component from "../framework/Component";

export default class Search extends Component {
    UpdateNumbers(showing: number, total: number) {
        this.MapCount.textContent = `Showing ${showing} of ${total} maps`
    }

    Input = <input placeholder="Type to search..." /> as HTMLInputElement;
    MapCount = <div id="map-count"></div>


    constructor() {
        super();
        this.UpdateNumbers(0, 0)
        this.HTMLElement = <div className="search">
            {this.Input}
            {this.MapCount}
        </div>
        this.Input.onblur = () => {
            setTimeout(() => this.Input.focus()); // focusing right away seems to break chrome a bit
        }
        this.Input.oninput = (e) => {
            this.OnChange?.(this.Input.value)
        }
        // we aren't actually in the DOM at this point unfortunately, we just have a DOM parent
        // just wait for rendering to finish, then we can focus
        setTimeout(() => this.Input.focus());
    }

    OnChange?: (value: string) => void

    get Value() { return this.Input.value; }
}