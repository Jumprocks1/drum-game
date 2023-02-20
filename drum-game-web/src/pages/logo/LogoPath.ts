import { PathBuilder } from "../../utils/PathBuilder";
import Vector from "../../utils/Vector";

function rad(deg: number) {
    return deg / 180 * Math.PI;
}
const ang = rad(30);
const largeFlatWidth = 0.481125;

function D(builder: PathBuilder, pos: Vector, height: number) {
    builder.MoveTo(pos);
    builder.LineToRelative(new Vector(0, -height));
    builder.Cap2();
    const flatWidth = height * largeFlatWidth;
    const dAngWidth = Math.tan(ang) * height / 2;
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
    const gBottomRight = builder.CurrentPoint;
    builder.LineToRelative(angleDir.neg().mult(seg[3]))
    builder.ExtendCap(0.26)
    builder.LineToRelative(new Vector(-seg[4]))
    builder.Cap();

    return gBottomRight;
}

export function FullLogoPath(builder: PathBuilder) {
    const capSize = 70;
    const dPos = new Vector(-70, 20)
    builder.RadiusOverride = 4.5;
    const dTip = D(builder, dPos, capSize);
    const dRight = dTip.X;
    const gBottomRight = NewG(builder, dTip, capSize);
    builder.RadiusOverride = 2.6;

    const smallSpacing = 8;
    const smallLetterHeight = 20;

    // RUM
    let baseline = dPos.Y - capSize + smallLetterHeight + 5;
    let top = baseline - smallLetterHeight;

    let left = dRight + 5;

    builder.PreCap()
    builder.MoveTo([left, baseline])
    builder.LineTo([left, top])
    builder.Cap2()
    builder.LineToRelative([15, 0])
    builder.LineToRelative([0, 11])
    let right = builder.X;
    builder.LineTo([left, builder.Y])
    builder.LineTo([right, builder.Y + 7])
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
        builder.ExtendCap(0.6);
        const mX = 8.5;
        const mY = 11;
        builder.LineToRelative([mX, mY])
        builder.LineToRelative([mX, -mY])
        builder.ExtendCap(0.6);
        builder.LineTo([builder.X, baseline])
        builder.Cap()
    }
    M();

    // A
    left = gBottomRight.X + 5;
    baseline = dTip.Y + smallLetterHeight + 5

    top = baseline - smallLetterHeight

    // const aX = smallLetterHeight * Math.tan(ang);
    const aX = smallLetterHeight * 0.45;
    const aY = smallLetterHeight * 0.8;
    // we draw the small segment of the A first so we can end on the far right
    const aWidthReduction = aX / smallLetterHeight * (smallLetterHeight - aY);
    builder.MoveTo([left + aWidthReduction, top + aY])
    builder.LineToRelative([aX * 2 - aWidthReduction * 2, 0])

    builder.PreCap();
    builder.MoveTo(new Vector(left, baseline))
    builder.LineTo([builder.X + aX, top])
    builder.ExtendCap()
    builder.LineTo([builder.X + aX, baseline])
    builder.Cap();

    left = builder.X + smallSpacing;
    M();

    // E
    left = builder.X + smallSpacing;
    const eWidth = 12;
    const eWidth2 = 8;
    builder.PreCap();
    builder.MoveTo([left + eWidth, baseline])
    builder.LineToRelative([-eWidth, 0])
    builder.Cap2();
    builder.LineToRelative([0, -smallLetterHeight])
    builder.Cap2();
    builder.LineToRelative([eWidth, 0])
    builder.Cap();
    builder.MoveTo([left, baseline - smallLetterHeight / 2])
    builder.LineToRelative([eWidth2, 0])
    builder.Cap();
}

export function DGLogoPath(builder: PathBuilder) {
    const height = 80;
    const flatWidth = height * largeFlatWidth
    const x = -flatWidth - Math.tan(ang) * height / 4;

    // D
    const dTip = D(builder, new Vector(x, height / 4), height);
    NewG(builder, dTip, height);
}