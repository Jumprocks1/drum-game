declare namespace JSX {
    type IntrinsicElements = {
        [K in keyof HTMLElementTagNameMap]: DeepPartial<HTMLElementTagNameMap[K]>
    };

    type Element = HTMLElement
}

type DeepPartial<T> = {
    [P in keyof T]?: Partial<T[P]>;
};


declare function createElement<T extends keyof HTMLElementTagNameMap>(
    element: T, properties?: DeepPartial<HTMLElementTagNameMap[T]>, ...children: any[]): JSX.Element
declare function createElement<T extends (props: Record<string, any>) => JSX.Element>(
    element: T, properties?: Parameters<T>[0], ...children: any[]): JSX.Element

interface HTMLElement {
    Component?: import("./Component").default
}
