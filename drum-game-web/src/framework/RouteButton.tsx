import Router, { PageType } from "./Router";

export default ({ page }: { page: PageType }) => <button onclick={function (this: HTMLElement) {
    this.Component?.FindParent(Router).NavigateTo(page);
}} />