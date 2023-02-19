import { PathBuilder } from "../../utils/PathBuilder";
import Vector from "../../utils/Vector";

function rad(deg: number) {
    return deg / 180 * Math.PI;
}
const ang = rad(40);

function D(builder: PathBuilder, pos: Vector, height: number) {
    builder.MoveTo(pos);
    builder.LineToRelative(new Vector(0, -height));
    const dWidth = Math.sin(ang) / (Math.sin(Math.PI / 2 - ang) / (height / 2));
    builder.ExtendCap();
    builder.LineToRelative([dWidth, height / 2])
    const dRight = builder.X;
    builder.LineToRelative([-dWidth, height / 2])
    builder.ExtendCap();
    builder.Cap(new Vector(0, -1))

    return dRight;
}

function G(builder: PathBuilder, pos: Vector, height: number) {
    const GSeg = [15, 30, 20, 10, 15];

    // G
    const ang1 = Math.PI / 2 - ang
    const angleSegmentLength = (height - GSeg[1]) / 2 / Math.sin(ang1)

    const topLeft = [pos.X + angleSegmentLength * Math.cos(ang1), pos.Y - height]

    builder.PreCap()
    builder.MoveTo([topLeft[0] + GSeg[0], topLeft[1]])
    builder.LineToRelative([-GSeg[0], 0])
    builder.LineToRelative([-angleSegmentLength * Math.cos(ang1), height / 2 - GSeg[1] / 2])
    builder.LineToRelative([0, GSeg[1]])
    builder.LineToRelative([angleSegmentLength * Math.cos(ang1), height / 2 - GSeg[1] / 2])
    builder.LineToRelative([GSeg[0], 0])
    builder.LineToRelative([Math.cos(ang1) * GSeg[2], -Math.sin(ang1) * GSeg[2]])
    builder.LineToRelative([0, -GSeg[3]])
    const gRight = builder.X;
    builder.LineToRelative([-GSeg[4], 0])
    builder.Cap();

    return gRight;
}

export function FullLogoPath(builder: PathBuilder) {


    const dRight = D(builder, new Vector(-53, 20), 90);
    const gRight = G(builder, new Vector(-45, 67), 60);

    const smallSpacing = 8;
    const smallLetterHeight = 25;

    // RUM
    let baseline = 0;
    let top = baseline - smallLetterHeight;

    let left = dRight + smallSpacing;

    builder.PreCap()
    builder.MoveTo([left, baseline])
    builder.LineTo([left, top])
    builder.LineToRelative([15, 0])
    builder.LineToRelative([0, 11])
    let right = builder.X;
    builder.LineTo([left, builder.Y])
    builder.LineTo([right, builder.Y + 8])
    builder.LineTo([builder.X, baseline])
    builder.Cap()

    builder.PreCap()
    left = builder.X + smallSpacing;
    builder.MoveTo([left, top])
    builder.LineTo([left, baseline])
    builder.LineToRelative([15, 0])
    builder.LineTo([builder.X, top])
    builder.Cap()


    left = builder.X + smallSpacing;
    function M() {
        builder.PreCap()
        builder.MoveTo([left, baseline])
        builder.LineTo([left, top])
        builder.ExtendCap();
        const mX = 8.5;
        const mY = 11;
        builder.LineToRelative([mX, mY])
        builder.LineToRelative([mX, -mY])
        builder.ExtendCap();
        builder.LineTo([builder.X, baseline])
        builder.Cap()
    }
    M();

    // A
    left = gRight + 5;
    baseline = 60
    top = baseline - smallLetterHeight
    const aX = 10;
    const aY = 18;
    // we draw the small segment of the A first so we can end on the far right
    const aWidthReduction = aX / smallLetterHeight * (smallLetterHeight - aY);
    builder.MoveTo([left + aWidthReduction, top + aY])
    builder.LineToRelative([aX * 2 - aWidthReduction * 2, 0])

    builder.PreCap();
    builder.MoveTo([left, baseline])
    builder.LineTo([builder.X + aX, top])
    builder.ExtendCap()
    builder.LineTo([builder.X + aX, baseline])
    builder.Cap();

    left = builder.X + smallSpacing - 2;
    M();

    // E
    left = builder.X + smallSpacing;
    const eWidth = 12;
    const eWidth2 = 8;
    builder.PreCap();
    builder.MoveTo([left + eWidth, baseline])
    builder.LineToRelative([-eWidth, 0])
    builder.LineToRelative([0, -smallLetterHeight])
    builder.LineToRelative([eWidth, 0])
    builder.Cap();
    builder.MoveTo([left, baseline - smallLetterHeight / 2])
    builder.LineToRelative([eWidth2, 0])
    builder.Cap();
}

export function DGLogoPath(builder: PathBuilder) {
    // D
    D(builder, new Vector(-50, 20), 110);

    G(builder, new Vector(-45, 67), 100);
}