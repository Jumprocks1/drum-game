import Component from "./Component";

export default class NoDOMComponent extends Component {
    AfterParent() {
        super.AfterParent();
        this.DOMNode = this.Parent!.DOMNode;
    }

    RemoveFromDOM() {
        for (let i = 0; i < this.Children.length; i++) {
            this.Children[i].RemoveFromDOM();
        }
    }
}