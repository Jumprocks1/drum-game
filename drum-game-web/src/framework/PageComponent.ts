import NoDOMComponent from "./NoDOMComponent";
import { RouteParameters } from "./Router";

export default abstract class PageComponent extends NoDOMComponent {
    LoadRoute(parameters: RouteParameters) { }
}