import Vector from "./Vector";

interface MeshPoint {
    vector: Vector;
}

export class PathBuilder {
    public Points: MeshPoint[] = []

    constructor() {
    }

    public CurrentPoint: Vector = new Vector()

    public get X() {
        return this.CurrentPoint.X;
    }
    public get Y() {
        return this.CurrentPoint.Y;
    }

    MoveTo(v: Vector | [number, number]) {
        this.CurrentPoint = Array.isArray(v) ? new Vector(v[0], v[1]) : v;
    }
    LineTo(point: Vector | [number, number]) {
        const v = Array.isArray(point) ? new Vector(point[0], point[1]) : point
        this.Points.push({ vector: this.CurrentPoint })
        this.Points.push({ vector: v })
        this.CurrentPoint = v;
    }
    LineToRelative(v: Vector | [number, number]) {
        this.LineTo(this.CurrentPoint.add(Array.isArray(v) ? new Vector(v[0], v[1]) : v))
    }
}