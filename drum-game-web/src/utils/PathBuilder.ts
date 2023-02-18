import { CapType, MeshPoint } from "../pages/logo/LineMesh";
import Vector from "./Vector";

export class PathBuilder {
    public Points: MeshPoint[] = []

    constructor() {
    }

    public CurrentPoint: Vector = new Vector()

    Connected = false;
    private preCap?: CapType;
    private extendCap?: number;

    public get LastPoint() {
        return this.Points[this.Points.length - 1];
    }

    public get X() {
        return this.CurrentPoint.X;
    }
    public get Y() {
        return this.CurrentPoint.Y;
    }

    MoveTo(v: Vector | [number, number]) {
        this.CurrentPoint = Array.isArray(v) ? new Vector(v[0], v[1]) : v;
        this.Connected = false;
    }
    PreCap(cap: CapType = true) {
        this.preCap = cap;
    }
    Cap(cap: CapType = true) {
        this.applyCapToLastPoint(cap)
    }
    private applyCapToLastPoint(v: CapType) {
        if (this.extendCap && v instanceof Vector) {
            this.LastPoint.cap = { vector: v, extend: this.extendCap };
            this.extendCap = undefined;
        } else {
            this.LastPoint.cap = v;
        }
    }
    ExtendCap(r = 0.5) {
        this.extendCap = r;
    }
    LineTo(point: Vector | [number, number]) {
        const v = Array.isArray(point) ? new Vector(point[0], point[1]) : point
        if (this.Connected && !this.LastPoint.cap) {
            this.applyCapToLastPoint(v.sub(this.CurrentPoint).norm())
        }
        this.Points.push({ vector: this.CurrentPoint })
        if (this.preCap) {
            this.LastPoint.cap = this.preCap;
            this.preCap = undefined;
        }
        this.Points.push({ vector: v })
        this.CurrentPoint = v;
        this.Connected = true;
    }
    LineToRelative(v: Vector | [number, number]) {
        this.LineTo(this.CurrentPoint.add(Array.isArray(v) ? new Vector(v[0], v[1]) : v))
    }
}