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
export default abstract class PageComponent extends NoDOMComponent {
    LoadRoute(parameters: RouteParameters) { }
}