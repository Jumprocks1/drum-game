import Router, { GlobalRouter, PageType } from "./Router";
import Component from "../framework/Component";

export default ({ page, parameters }: { page: PageType, parameters?: string[] }) => <button onclick={function (this: HTMLElement) {
    this.Component?.FindParent(Router).NavigateTo(page, ...(parameters ?? []));
}} />

export class RouteLink extends Component {

    Page: PageType;
    private _parameters?: string[];

    constructor({ page, parameters }: { page: PageType, parameters?: string[] }) {
        super();
        this.Page = page;
        this._parameters = parameters;
        this.HTMLElement = <a className="route-link" onclick={this.OnClick} />
        this.HTMLElement.Component = this;
    }

    UpdateHref() {
        (this.HTMLElement as HTMLAnchorElement).href = Router.BuildRoute(this.Page, ...(this._parameters ?? []));
    }

    OnClick = (e: MouseEvent) => {
        e.preventDefault();
        GlobalRouter?.NavigateTo(this.Page, ...(this._parameters ?? []));
    }

    set Parameters(parameters: string[]) {
        this._parameters = parameters;
        this.UpdateHref();
    }
}