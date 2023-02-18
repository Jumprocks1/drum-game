export type Point = [x: number, y: number]

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