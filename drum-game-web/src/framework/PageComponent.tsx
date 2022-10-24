import Component from "./Component";
import { RouteParameters } from "./Router";

/**
 * PageComponents can have the following extra static properties
 * Route: string;
 *      
 * RouteUrl?: string;
 *   Only used when we call NavigateTo. This will be the URL that displays when we navigate to the page.
 * RouteRegex?: RegExp;
 *   Manual Regex for matching a route. Usually used with RouteUrl.
 */
export default abstract class PageComponent extends Component {
    PageId(): string | undefined {
        // return KebabCase(this.constructor.name) // this doesn't work with webpack
        return (this.constructor as any).PageId
    }

    constructor() {
        super();
        this.HTMLElement = <div className="page" id={this.PageId()} />
    }
    LoadRoute(parameters: RouteParameters) { }
}