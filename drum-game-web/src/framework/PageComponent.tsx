import { KebabCase } from "../utils/Util";
import Component from "./Component";
import NoDOMComponent from "./NoDOMComponent";
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
        return KebabCase(this.constructor.name)
    }

    constructor() {
        super();
        this.HTMLElement = <div className="page" id={this.PageId()} />
    }
    LoadRoute(parameters: RouteParameters) { }
}