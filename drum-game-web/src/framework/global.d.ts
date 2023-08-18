declare namespace JSX {
    type IntrinsicElements = {
        [K in keyof HTMLElementTagNameMap]: DeepPartial<HTMLElementTagNameMap[K]>
    };

    type Element = HTMLElement // technically this should be `Node`, but HTMLElement is easier to work with
}

type DeepPartial<T> = {
    [P in keyof T]?: Partial<T[P]>;
};


declare function createElement<T extends keyof HTMLElementTagNameMap>(
    element: T, properties?: DeepPartial<HTMLElementTagNameMap[T]>, ...children: any[]): JSX.Element
declare function createElement<T extends (props: Record<string, any>) => JSX.Element>(
    element: T, properties?: Parameters<T>[0], ...children: any[]): JSX.Element
declare function createFragment(...children: any[]): JSX.Element

interface HTMLElement {
    Component?: import("./Component").default
}


declare module "*.frag" {
    const value: string;
    export = value;
}