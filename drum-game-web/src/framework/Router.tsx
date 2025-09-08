import { FrameworkConfig } from "./Framework";
import NoDOMComponent from "./NoDOMComponent";
import PageComponent from "./PageComponent";

export type PageType = (new () => PageComponent) & (
    | { Route: string; RouteUrl?: string, RouteRegex?: RegExp }
    | { RouteUrl: string, RouteRegex: RegExp }
) & {
    PageId?: string
    Title?: string | ((state: RouterState) => string)
}

export type RouteParameters = string[]

function buildRegex(route: string) {
    if (!route.startsWith("/"))
        route = "/" + route;
    route = route.replace(/\$\d+/g, "([^\/]+)")
    return new RegExp("^" + route + "$")
}

export let GlobalRouter: Router | undefined = undefined;

export interface RouterState {
    page: PageType
    parameters?: RouteParameters
    data?: any
    rawData?: any // doesn't get pushed to browser history
}

export default class Router extends NoDOMComponent {

    Pages: PageType[];
    State: RouterState | undefined

    History: RouterState[] = []

    DefaultTitle?: string;

    constructor(pages: PageType[]) {
        super();
        GlobalRouter = this;
        this.Pages = pages;
    }

    OnHistoryChange = () => {
        this.UpdateRouting();
    }

    AfterParent() {
        super.AfterParent();
        // doesn't trigger for replaceState it seems.
        // Think that's fine, if we want to update routing we can manually trigger after replaceState
        window.addEventListener("popstate", this.OnHistoryChange)
        this.UpdateRouting();
    }
    AfterRemove() {
        super.AfterRemove();
        window.removeEventListener("popstate", this.OnHistoryChange);
    }

    static BuildRoute(state: RouterState) {
        // @ts-ignore page.Route will always exists when RouteUrl does not
        let target: string = state.page.RouteUrl ?? state.page.Route;
        if (!target.startsWith("/")) target = "/" + target;
        if (state.parameters)
            for (let i = 0; i < state.parameters.length; i++)
                target = target.replace("$" + i, state.parameters[i]);
        if (FrameworkConfig.baseName)
            target = FrameworkConfig.baseName + target;
        return target;
    }

    // this does not trigger a page load
    // it also doesn't push the current history
    ReplaceRoute(state: RouterState) {
        history.replaceState(state.data, "", Router.BuildRoute(state));
        this.State = state;
    }

    NavigateTo(state: RouterState, force = false) { // this sets the route and loads the page
        history.pushState(state.data, "", Router.BuildRoute(state));
        if (this.State) this.History.push(this.State);
        this.LoadPage(state, force); // this will set this.State for us
    }

    NavigateBack(fallbackState?: RouterState) {
        if (this.History.length === 0) {
            if (fallbackState)
                this.NavigateTo(fallbackState);
        } else {
            this.History.pop();
            history.back();
        }
    }

    // force will trigger a reload of the page even if it's the same
    LoadPage(state: RouterState, force = false) {
        const page = state.page;
        // this check usually doesn't work since parameters is an array
        if (!force && this.State?.page === page && state.parameters === this.State.parameters) return;
        this.State = state;
        this.Clear();
        const newPage = new page();
        if (state.parameters) newPage.LoadRoute(state.parameters);
        this.Add(newPage);

        const pageTitle = typeof page.Title === "function" ? page.Title(state) : page.Title;

        document.title = pageTitle ?? this.DefaultTitle ?? "";
    }

    private UpdateRouting() {
        let route = window.location.pathname
        if (route.startsWith(FrameworkConfig.baseName))
            route = route.substring(FrameworkConfig.baseName.length)

        console.log(`updating route to ${route}`);

        for (const page of this.Pages) {
            // @ts-ignore page.Route will always exists when RouteRegex does not
            const regex = page.RouteRegex ??= buildRegex(page.Route)
            const res = route.match(regex);
            if (res) {
                this.LoadPage({ page, parameters: res.slice(1) });
                return;
            }
        }

        console.error(`no page found for route '${route}'`);
    }
}