import Component from "./Component";

type FC = (props: Record<string, any>) => JSX.Element

export function createElement(element: string | FC,
    properties?: any, ...children: any[]) {
    let el: HTMLElement;
    if (typeof element === "string") {
        el = document.createElement(element);
        if (properties) {
            for (const key in properties) {
                if (key === "style") {
                    for (const key2 in properties[key])
                        // @ts-ignore
                        el[key][key2] = properties[key][key2];
                } else {
                    const v = properties[key];
                    if (v !== undefined)
                        // @ts-ignore
                        el[key] = v!;
                }
            }
        }
    } else {
        if (element.prototype instanceof Component) {
            // TODO this is pretty sketchy
            const component: Component = new (element as any)(properties ?? {});
            el = component.HTMLElement
        } else {
            el = element(properties ?? {});
        }
    }

    for (let i = 0; i < children.length; i++) {
        const child = children[i];
        if (typeof child === "object") {
            el.appendChild(child);
        } else if (typeof child === "string") {
            el.appendChild(document.createTextNode(child));
        } else if (child !== undefined && child !== false) {
            el.appendChild(document.createTextNode(child.toString()));
        }
    }
    return el;
}