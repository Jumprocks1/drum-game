import { FrameworkConfig } from "./Framework";
import NoDOMComponent from "./NoDOMComponent";
import PageComponent from "./PageComponent";

export type PageType = (new () => PageComponent) & (
    | { Route: string; RouteUrl?: string, RouteRegex?: RegExp }
    | { RouteUrl: string, RouteRegex: RegExp }
)

export type RouteParameters = string[]

function buildRegex(route: string) {
    if (!route.startsWith("/"))
        route = "/" + route;
    route = route.replace(/\$\d+/g, "([^\/]+)")
    return new RegExp("^" + route + "$")
}

export default class Router extends NoDOMComponent {

    Pages: PageType[];

    constructor(pages: PageType[]) {
        super();
        this.Pages = pages;
    }

    OnHistoryChange = () => {
        this.UpdateRouting();
    }

    AfterParent() {
        super.AfterParent();
        window.addEventListener("popstate", this.OnHistoryChange)
        this.UpdateRouting();
    }
    AfterRemove() {
        super.AfterRemove();
        window.removeEventListener("popstate", this.OnHistoryChange);
    }

    NavigateTo(page: PageType, ...parameters: string[]) {
        // @ts-ignore page.Route will always exists when RouteUrl does not
        let target: string = page.RouteUrl ?? page.Route;
        if (!target.startsWith("/")) target = "/" + target;
        for (let i = 0; i < parameters.length; i++)
            target = target.replace("$" + i, parameters[i]);
        if (FrameworkConfig.baseName)
            target = FrameworkConfig.baseName + target;
        history.pushState(undefined, "", target);
        this.LoadPage(page, parameters);
    }

    LoadPage(page: PageType, parameters?: RouteParameters) {
        if (this.CurrentPage === page && !parameters) return;
        this.Clear();
        const newPage = new page();
        if (parameters) newPage.LoadRoute(parameters);
        this.Add(newPage);
        this.CurrentPage = page;
    }

    CurrentPage: PageType | undefined;

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
                this.LoadPage(page, res.slice(1));
                return;
            }
        }

        console.error(`no page found for route '${route}'`);
    }
}