import Router, { GlobalRouter, PageType, RouterState } from "./Router";
import Component from "../framework/Component";

export default (route: RouterState) => <button onclick={function (this: HTMLElement) {
    this.Component?.FindParent(Router).NavigateTo(route);
}} />

export class RouteLink extends Component {

    State: RouterState

    constructor(route: RouterState) {
        super();
        this.State = route;
        this.HTMLElement = <a className="route-link" onclick={this.OnClick} />
        this.HTMLElement.Component = this;
    }

    UpdateHref() {
        (this.HTMLElement as HTMLAnchorElement).href = Router.BuildRoute(this.State);
    }

    OnClick = (e: MouseEvent) => {
        e.preventDefault();
        GlobalRouter?.NavigateTo(this.State);
    }

    set Parameters(parameters: string[]) {
        this.State = { ...this.State, parameters }
        this.UpdateHref();
    }
}