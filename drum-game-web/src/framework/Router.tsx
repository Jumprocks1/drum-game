import { FrameworkConfig } from "./Framework";
import NoDOMComponent from "./NoDOMComponent";
import PageComponent from "./PageComponent";

export type PageType = (new () => PageComponent) & (
    | { Route: string; RouteUrl?: string, RouteRegex?: RegExp }
    | { RouteUrl: string, RouteRegex: RegExp }
)

function buildRegex(route: string) {
    if (!route.startsWith("/"))
        route = "/" + route;
    return new RegExp("^" + route + "$")
}

export default class Router extends NoDOMComponent {

    Pages: PageType[];

    constructor(pages: PageType[]) {
        super();
        this.Pages = pages;
    }

    AfterParent() {
        super.AfterParent();
        this.UpdateRouting();
    }

    NavigateTo(page: PageType, updatePath = true) {
        if (this.CurrentPage === page) return;
        this.Clear();
        if (updatePath) {
            // @ts-ignore page.Route will always exists when RouteUrl does not
            let target = page.RouteUrl ?? page.Route;
            if (!target.startsWith("/")) target = "/" + target;
            if (FrameworkConfig.baseName)
                target = FrameworkConfig.baseName + target;
            history.pushState({}, "", target);
        }
        this.Add(new page());
        this.CurrentPage = page;
    }

    CurrentPage: PageType | undefined;

    private UpdateRouting() {
        let targetPage: PageType | undefined = undefined;

        let route = window.location.pathname
        if (FrameworkConfig.baseName && route.startsWith(FrameworkConfig.baseName))
            route = route.substring(FrameworkConfig.baseName.length)

        for (const page of this.Pages) {
            // @ts-ignore page.Route will always exists when RouteRegex does not
            const regex = page.RouteRegex ??= buildRegex(page.Route)
            if (regex.test(route)) {
                targetPage = page;
                break;
            }
        }

        if (targetPage) this.NavigateTo(targetPage, false);
        else console.error("no page found");
    }
}