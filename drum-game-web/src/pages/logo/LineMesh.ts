import Vector from "../../utils/Vector";

export default function LineMesh(points: Vector[]): { positions: Float32Array, normals: Float32Array, VertexCount: number } {

    const VertexCount = points.length / 2 * 12;
    const positions = new Float32Array(3 * VertexCount);
    const normals = new Float32Array(2 * VertexCount);

    let v = 0;

    const radius = 2;

    function pushVertex(pos: Vector, height: number, normal: Vector) {
        positions[v * 3] = pos.X / 100;
        positions[v * 3 + 1] = -pos.Y / 100;
        positions[v * 3 + 2] = height;
        normals[v * 2] = normal.X;
        normals[v * 2 + 1] = -normal.Y;
        v += 1;
    }

    for (let i = 0; i < points.length; i += 2) {
        const a = points[i];
        const b = points[i + 1];
        const dir = b.sub(a).norm();
        const left = new Vector(dir.Y, -dir.X);
        const right = left.neg();
        const leftR = left.mult(radius);

        pushVertex(points[i], 1, left);
        pushVertex(points[i].add(leftR), 0, left);
        pushVertex(points[i + 1].add(leftR), 0, left);
        pushVertex(points[i + 1].add(leftR), 0, left);
        pushVertex(points[i + 1], 1, left);
        pushVertex(points[i], 1, left);

        pushVertex(points[i], 1, right);
        pushVertex(points[i].sub(leftR), 0, right);
        pushVertex(points[i + 1].sub(leftR), 0, right);
        pushVertex(points[i + 1].sub(leftR), 0, right);
        pushVertex(points[i + 1], 1, right);
        pushVertex(points[i], 1, right);
    }

    console.log({ positions, normals, VertexCount })




    return { positions, normals, VertexCount };
}