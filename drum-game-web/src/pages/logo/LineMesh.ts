import Vector from "../../utils/Vector";

export interface LinePoint {
    vector: Vector;
    cap?: CapType;
}

export interface Mesh {
    positions: Float32Array,
    normals: Float32Array,
    VertexCount: number
}

export type CapTypeObject = { vector: Vector, extend?: number, type?: "2part" };

export type CapType = "end" | CapTypeObject

export default function LineMesh(points: LinePoint[], radius: number): Mesh {

    const positions: number[] = []; // using arrays like this is probably not ideal
    const normals: number[] = [];

    let v = 0;

    function pushVertex(pos: Vector, height: number, normal: Vector) {
        positions.push(pos.X / 100);
        positions.push(-pos.Y / 100);
        positions.push(height);
        normals.push(normal.X);
        normals.push(-normal.Y);
        v += 1;
    }

    function cap(p: Vector, dir: Vector, cap: CapType) {
        const left = new Vector(dir.Y, -dir.X);
        const right = new Vector(-dir.Y, dir.X);
        const leftR = left.mult(radius);
        if (cap === "end") {
            pushVertex(p, 1, left);
            pushVertex(p.add(leftR), 0, left);
            pushVertex(p.add(leftR).add(dir.mult(radius)), 0, left);

            pushVertex(p, 1, right);
            pushVertex(p.sub(leftR), 0, right);
            pushVertex(p.sub(leftR).add(dir.mult(radius)), 0, right);

            pushVertex(p, 1, dir);
            pushVertex(p.add(leftR).add(dir.mult(radius)), 0, dir);
            pushVertex(p.sub(leftR).add(dir.mult(radius)), 0, dir);
        } else if (cap.type === "2part") {
            const v = cap.vector;

            const cross = dir.cross(v);
            const sign = Math.sign(cross); // this tells us if we are making a left or right turn

            const side = left.mult(sign);
            const side2 = new Vector(v.Y, -v.X).mult(sign);

            const a = p.add(leftR.mult(sign));
            const b = p.add(side2.mult(radius));

            const sum = dir.add(v);
            // I would love to fix this so it doesn't have the condition
            const extension = Math.abs(sum.X) > Math.abs(sum.Y) ? (b.X - a.X) / sum.X : (b.Y - a.Y) / sum.Y

            const c = a.add(dir.mult(extension))

            pushVertex(p, 1, side);
            pushVertex(c, 0, side);
            pushVertex(a, 0, side);

            pushVertex(p, 1, side2);
            pushVertex(c, 0, side2);
            pushVertex(b, 0, side2);
        } else if (cap.extend) {
            const v = cap.vector;
            const extension = cap.extend * radius;
            // I couldn't figure out a perfect extension calculation, not sure what it should be
            // We could try setting it the value such that the norm interescts the outer edge at exactly `radius`

            const norm = dir.sub(v).norm();
            const cross = dir.cross(v);

            const sign = Math.sign(cross); // this tells us if we are making a left or right turn

            const side = left.mult(sign);
            const sideR = leftR.mult(sign);

            const a = p.add(sideR);
            const aFar = a.add(dir.mult(extension));

            const side2 = new Vector(v.Y, -v.X).mult(sign);
            const sideR2 = side2.mult(radius);
            const b = p.add(sideR2);
            const bFar = b.sub(v.mult(extension));

            pushVertex(p, 1, norm);
            pushVertex(aFar, 0, norm);
            pushVertex(bFar, 0, norm);

            // console.log({
            //     angle: bFar.sub(p).ang(aFar.sub(p)) / Math.PI * 180,
            //     angle2: aFar.sub(p).ang(dir) / Math.PI * 180
            // })

            pushVertex(p, 1, side);
            pushVertex(aFar, 0, side);
            pushVertex(a, 0, side);

            pushVertex(p, 1, side2);
            pushVertex(bFar, 0, side2);
            pushVertex(b, 0, side2);
        } else {
            const v = cap.vector;
            const norm = dir.sub(v).norm();
            const cross = dir.cross(v);
            const sign = Math.sign(cross); // this tells us if we are making a left or right turn

            pushVertex(p, 1, norm);
            pushVertex(p.add(leftR.mult(sign)), 0, norm);
            const left2 = new Vector(v.Y, -v.X)
            pushVertex(p.add(left2.mult(radius * sign)), 0, norm);
        }
    }

    for (let i = 0; i < points.length; i += 2) {
        const a = points[i].vector;
        const b = points[i + 1].vector;
        const dir = b.sub(a).norm();
        const left = new Vector(dir.Y, -dir.X);
        const right = left.neg();
        const leftR = left.mult(radius);

        pushVertex(a, 1, left);
        pushVertex(a.add(leftR), 0, left);
        pushVertex(b.add(leftR), 0, left);
        pushVertex(b.add(leftR), 0, left);
        pushVertex(b, 1, left);
        pushVertex(a, 1, left);

        pushVertex(a, 1, right);
        pushVertex(a.sub(leftR), 0, right);
        pushVertex(b.sub(leftR), 0, right);
        pushVertex(b.sub(leftR), 0, right);
        pushVertex(b, 1, right);
        pushVertex(a, 1, right);

        const aCap = points[i].cap;
        if (aCap) {
            cap(a, dir.neg(), aCap);
        }
        const bCap = points[i + 1].cap;
        if (bCap) {
            cap(b, dir, bCap);
        }
    }



    return { positions: new Float32Array(positions), normals: new Float32Array(normals), VertexCount: v };
}