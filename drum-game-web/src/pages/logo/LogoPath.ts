import { PathBuilder } from "../../utils/PathBuilder";
import Vector from "../../utils/Vector";

function rad(deg: number) {
    return deg / 180 * Math.PI;
}
const ang = rad(30);

function D(builder: PathBuilder, pos: Vector, height: number) {
    builder.MoveTo(pos);
    builder.LineToRelative(new Vector(0, -height));
    builder.Cap2();
    const flatWidth = height * 0.45;
    const dAngWidth = Math.sin(ang) / (Math.sin(Math.PI / 2 - ang) / (height / 2));
    builder.LineToRelative(new Vector(flatWidth))
    builder.Cap2();
    builder.LineToRelative([dAngWidth, height / 2])
    builder.Cap2();
    const tip = builder.CurrentPoint;
    builder.LineToRelative([-dAngWidth, height / 2])
    builder.Cap2();
    builder.LineToRelative(new Vector(-flatWidth))
    builder.Cap2();
    builder.Cap(new Vector(0, -1))

    return tip;
}

function NewG(builder: PathBuilder, pos: Vector, height: number) {
    const seg = [
        25, // flat top
        30, // left angled segment, used 2 times
        25, // flat bottom
        30, // right angled segment
        23
    ];

    const scale = height / (seg[1] * 2 * Math.cos(ang))
    for (let i = 0; i < seg.length; i++) seg[i] *= scale;

    // G
    const angleDir = new Vector(-Math.sin(ang), Math.cos(ang))
    const angleSeg = angleDir.mult(seg[1])

    builder.PreCap()
    builder.MoveTo(pos.add(new Vector(seg[0])))
    builder.LineToRelative(new Vector(-seg[0]))
    builder.Cap2();
    builder.LineToRelative(angleSeg)
    builder.Cap2();
    builder.LineToRelative(angleSeg.negX())
    builder.Cap2();
    builder.LineToRelative(new Vector(seg[2]))
    builder.Cap2();
    builder.LineToRelative(angleDir.neg().mult(seg[3]))
    builder.ExtendCap(0.26)
    const gRight = builder.X;
    builder.LineToRelative(new Vector(-seg[4]))
    builder.Cap();

    return gRight;
}

export function FullLogoPath(builder: PathBuilder) {


    const dTip = D(builder, new Vector(-53, 20), 90);
    const dRight = dTip.X;
    const gRight = NewG(builder, new Vector(-18, 7), 1);

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
    left = gRight + 6;
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
    const dTip = D(builder, new Vector(-50, 20), 80);
    NewG(builder, dTip, 80);
}