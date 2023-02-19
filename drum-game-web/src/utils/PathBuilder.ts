import { CapType, CapTypeObject, LinePoint } from "../pages/logo/LineMesh";
import Vector from "./Vector";

export class PathBuilder {
    public Points: LinePoint[] = []

    public CurrentPoint: Vector = new Vector()

    RadiusOverride?: number;

    Connected = false;
    private preCap?: CapType;
    private nextCap?: Partial<CapTypeObject>;

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
    PreCap(cap: CapType = "end") {
        this.preCap = cap;
    }
    Cap(cap: CapType | Vector = "end") {
        if (cap instanceof Vector) {
            cap = { vector: cap }
        }
        this.applyCapToLastPoint(cap)
    }
    private applyCapToLastPoint(v: CapType) {
        if (this.nextCap && v !== "end") {
            v = { ...this.nextCap, ...v }
            this.nextCap = undefined;
        }
        this.LastPoint.cap = v;
    }
    ExtendCap(r = 0.5) {
        this.nextCap = { extend: r }
    }
    NextCap(cap: Partial<CapTypeObject>) {
        this.nextCap = cap;
    }
    Cap2() {
        this.nextCap = { type: "2part" }
    }
    LineTo(point: Vector | [number, number]) {
        const v = Array.isArray(point) ? new Vector(point[0], point[1]) : point
        if (this.Connected && !this.LastPoint.cap) {
            this.applyCapToLastPoint({ vector: v.sub(this.CurrentPoint).norm() })
        }
        this.Points.push({ vector: this.CurrentPoint })
        if (this.RadiusOverride) this.LastPoint.radius = this.RadiusOverride;
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