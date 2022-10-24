import { RegisterListener, RemoveListener } from "./Framework";
import NoDOMComponent from "./NoDOMComponent";

export default class Component {
    DOMNode: Node | undefined;

    Children: Component[];

    Parent: Component | undefined;

    get HTMLElement() {
        return this.DOMNode as HTMLElement;
    }

    set HTMLElement(element: HTMLElement) {
        this.DOMNode = element;
    }

    get InnerNode() {
        return this.DOMNode;
    }

    constructor() {
        this.Children = []
    }

    OnKeyDown?: (event: KeyboardEvent) => boolean | void;
    OnPageResize?: (event: UIEvent) => boolean | void;

    Clear() {
        while (this.Children.length > 0)
            this.RemoveAt(this.Children.length - 1);
    }

    Kill() {
        if (this.Parent) {
            this.Parent.Remove(this);
        }
    }

    AfterParent() {
        // everything in here should also be handled in AfterRemove
        if (this.OnKeyDown)
            RegisterListener("keydown", this.OnKeyDown);
        if (this.OnPageResize)
            RegisterListener("resize", this.OnPageResize);
    }

    AfterRemove() {
        if (this.OnKeyDown)
            RemoveListener("keydown", this.OnKeyDown);
        if (this.OnPageResize)
            RemoveListener("resize", this.OnPageResize);
    }

    AfterDOM() { }

    Add(component: Component | HTMLElement) {
        if (!(component instanceof Component)) {
            let c = new Component();
            c.DOMNode = component;
            component.Component = c;
            component = c;
        }
        this.Children.push(component);
        component.Parent = this;
        component.AfterParent(); // we call AfterParent before checking DOMNode since AfterParent might assign the DOMNode
        if (component.DOMNode && component.DOMNode !== this.InnerNode) { // for NoDOMComponents, these will be equal
            this.InnerNode?.appendChild(component.DOMNode);
            component.AfterDOM();
        }
        return component;
    }

    RemoveFromDOM() {
        const node = this.DOMNode;
        if (node) node.parentElement?.removeChild(node);
    }

    RemoveAt(index: number) {
        const res = this.Children.splice(index, 1)[0];
        res.RemoveFromDOM();
        res.Parent = undefined;
        res.AfterRemove();
    }

    Remove(component: Component) {
        const i = this.Children.indexOf(component);
        if (i === -1) return;
        this.RemoveAt(i);
    }
    RemoveAll(componentType: new (...args: any) => Component) {
        for (let i = this.Children.length - 1; i >= 0; i--)
            if (this.Children[i] instanceof componentType)
                this.RemoveAt(i);
    }
    ChildrenAfterRemove(componentType: (new (...args: any) => Component) | undefined = undefined) {
        if (!componentType)
            for (let i = this.Children.length - 1; i >= 0; i--)
                this.Children[i].AfterRemove();
        else
            for (let i = this.Children.length - 1; i >= 0; i--)
                if (this.Children[i] instanceof componentType)
                    this.Children[i].AfterRemove();
    }

    FindParent<T extends abstract new (...args: any) => any>(type: T) {
        const res = this.TryFindParent(type);
        if (!res) throw new Error(`Parent of type ${type.name} not found`);
        return res;
    }
    TryFindParent<T extends abstract new (...args: any) => any>(type: T) {
        let target = this.Parent;
        while (target !== undefined) {
            if (target instanceof type) return target as InstanceType<T>;
            if (target.Parent === target) break;
            target = target.Parent
        }
        return undefined;
    }

    FindChild<T extends abstract new (...args: any) => any>(type: T) {
        for (let i = 0; i < this.Children.length; i++) {
            if (this.Children[i] instanceof type) {
                return this.Children[i] as InstanceType<T>;
            }
        }
        throw new Error(`Child of type ${type.name} not found`);
    }

    get Alive() {
        let target: Component | undefined = this;
        while (target !== undefined) {
            if (target.Parent === target) return true;
            target = target.Parent;
        }
        return false;
    }
}