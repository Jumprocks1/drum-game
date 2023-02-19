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
    div(s: number) {
        return new Vector(this.X / s, this.Y / s)
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
    negX() {
        return new Vector(-this.X, this.Y);
    }
    negY() {
        return new Vector(this.X, -this.Y);
    }
    cross(v: Vector) {
        return this.X * v.Y - v.X * this.Y;
    }
    dot(v: Vector) {
        return this.X * v.X + this.Y * v.Y;
    }
    dist(v: Vector) {
        const dx = this.X - v.X;
        const dy = this.Y - v.Y;
        return Math.sqrt(dx * dx + dy * dy);
    }
    ang(v: Vector) {
        return Math.acos(this.dot(v) / (this.length() * v.length()))
    }
}
