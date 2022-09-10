import Component from "../framework/Component";

export default class Search extends Component {
    constructor() {
        super();
        this.HTMLElement = <input className="search" placeholder="Type to search..." />
    }

    OnChange?: (value: string) => void

    get Value() { return (this.HTMLElement as HTMLInputElement).value; }

    AfterDOM() {
        super.AfterDOM();
        this.HTMLElement.onblur = () => { this.HTMLElement.focus(); }
        this.HTMLElement.oninput = (e) => {
            this.OnChange?.((this.HTMLElement as HTMLInputElement).value)
        }
        this.HTMLElement.focus();
    }
}