import Vector from "../../utils/Vector";

export interface MeshPoint {
    vector: Vector;
}

export default function LineMesh(points: MeshPoint[]): { positions: Float32Array, normals: Float32Array, VertexCount: number } {

    const VertexCount = points.length / 2 * 12;
    const positions = new Float32Array(3 * VertexCount);
    const normals = new Float32Array(2 * VertexCount);

    let v = 0;

    const radius = 3;

    function pushVertex(pos: Vector, height: number, normal: Vector) {
        positions[v * 3] = pos.X / 100;
        positions[v * 3 + 1] = -pos.Y / 100;
        positions[v * 3 + 2] = height;
        normals[v * 2] = normal.X;
        normals[v * 2 + 1] = -normal.Y;
        v += 1;
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
    }

    console.log({ positions, normals, VertexCount })




    return { positions, normals, VertexCount };
}