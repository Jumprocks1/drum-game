export default class Vector {
    X: number;
    Y: number;
    constructor(x?: number, y?: number) {
        this.X = x ?? 0;
        this.Y = y ?? 0;
    }

    add(a: Vector) {
        return new Vector(this.X + a.X, this.Y + a.Y)
    }
    mult(s: number) {
        return new Vector(this.X * s, this.Y * s)
    }
    sub(a: Vector) {
        return new Vector(this.X - a.X, this.Y - a.Y)
    }
    length() {
        return Math.sqrt(this.X * this.X + this.Y * this.Y)
    }
    norm() {
        const length = this.length();
        return new Vector(this.X / length, this.Y / length);
    }
    neg() {
        return new Vector(-this.X, -this.Y);
    }
}
