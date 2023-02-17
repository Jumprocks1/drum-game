
export type Point = [x: number, y: number]

export default class PathDrawer {
    readonly Context: CanvasRenderingContext2D

    constructor(context: CanvasRenderingContext2D) {
        this.Context = context;
    }

    public CurrentPoint: Point = [0, 0]

    public get X() {
        return this.CurrentPoint[0];
    }
    public get Y() {
        return this.CurrentPoint[1];
    }

    MoveTo(point: Point) {
        this.CurrentPoint = point;
        this.Context.moveTo(point[0], point[1])
    }
    LineTo(point: Point) {
        this.CurrentPoint = point;
        this.Context.lineTo(point[0], point[1])
    }
    LineToRelative(point: Point) {
        this.LineTo([this.CurrentPoint[0] + point[0], this.CurrentPoint[1] + point[1]])
    }
}

export interface IPathBuilder {
    MoveTo: (point: Point) => void;
    LineToRelative: (point: Point) => void;
    LineTo: (point: Point) => void;
    X: number;
    Y: number;
}

export class PathBuilder {
    public Points: Point[] = []

    constructor() {
    }

    public CurrentPoint: Point = [0, 0]

    public get X() {
        return this.CurrentPoint[0];
    }
    public get Y() {
        return this.CurrentPoint[1];
    }

    MoveTo(point: Point) {
        this.CurrentPoint = point;
    }
    LineTo(point: Point) {
        this.Points.push(this.CurrentPoint)
        this.Points.push(point)
        this.CurrentPoint = point;
    }
    LineToRelative(point: Point) {
        this.LineTo([this.CurrentPoint[0] + point[0], this.CurrentPoint[1] + point[1]])
    }
}