import { PathBuilder } from "../../utils/PathBuilder";
import Vector from "../../utils/Vector";

function rad(deg: number) {
    return deg / 180 * Math.PI;
}

export default function (builder: PathBuilder) {
    const DPos = new Vector(-53, 20);
    const DHeight = 90;

    const ang = rad(40);

    const GPos = [-45, 67]; // bottom left (G doesn't actually touch this)
    const GSeg = [15, 30, 20, 10, 15];
    const GHeight = 60;


    // D
    builder.MoveTo(DPos);
    builder.LineToRelative(new Vector(0, -DHeight));
    const dWidth = Math.sin(ang) / (Math.sin(Math.PI / 2 - ang) / (DHeight / 2));
    builder.LineToRelative([dWidth, DHeight / 2])
    const dRight = builder.X;
    builder.LineToRelative([-dWidth, DHeight / 2])

    // G
    const ang1 = Math.PI / 2 - ang
    const angleSegmentLength = (GHeight - GSeg[1]) / 2 / Math.sin(ang1)

    const topLeft = [GPos[0] + angleSegmentLength * Math.cos(ang1), GPos[1] - GHeight]

    builder.MoveTo([topLeft[0] + GSeg[0], topLeft[1]])
    builder.LineToRelative([-GSeg[0], 0])
    builder.LineToRelative([-angleSegmentLength * Math.cos(ang1), GHeight / 2 - GSeg[1] / 2])
    builder.LineToRelative([0, GSeg[1]])
    builder.LineToRelative([angleSegmentLength * Math.cos(ang1), GHeight / 2 - GSeg[1] / 2])
    builder.LineToRelative([GSeg[0], 0])
    builder.LineToRelative([Math.cos(ang1) * GSeg[2], -Math.sin(ang1) * GSeg[2]])
    builder.LineToRelative([0, -GSeg[3]])
    const gRight = builder.X;
    builder.LineToRelative([-GSeg[4], 0])

    const smallSpacing = 8;
    const smallLetterHeight = 25;

    // RUM
    let baseline = DPos.Y - 20;
    let top = baseline - smallLetterHeight;

    let left = dRight + smallSpacing;
    builder.MoveTo([left, baseline])
    builder.LineTo([left, top])
    builder.LineToRelative([15, 0])
    builder.LineToRelative([0, 8])
    let right = builder.X;
    builder.LineToRelative([-3, 3])
    builder.LineTo([left, builder.Y])
    builder.LineTo([right, builder.Y + 8])
    builder.LineTo([builder.X, baseline])

    left = builder.X + smallSpacing;
    builder.MoveTo([left, top])
    builder.LineTo([left, baseline])
    builder.LineToRelative([15, 0])
    builder.LineTo([builder.X, top])


    left = builder.X + smallSpacing;
    function M() {
        builder.MoveTo([left, baseline])
        builder.LineTo([left, top])
        const mX = 8.5;
        const mY = 11;
        builder.LineToRelative([mX, mY])
        builder.LineToRelative([mX, -mY])
        builder.LineTo([builder.X, baseline])
    }
    M();

    // A
    left = gRight + 5;
    baseline = GPos[1] - 7
    top = baseline - smallLetterHeight
    const aX = 10;
    const aY = 18;
    // we draw the small segment of the A first so we can end on the far right
    const aWidthReduction = aX / smallLetterHeight * (smallLetterHeight - aY);
    builder.MoveTo([left + aWidthReduction, top + aY])
    builder.LineToRelative([aX * 2 - aWidthReduction * 2, 0])

    builder.MoveTo([left, baseline])
    builder.LineTo([builder.X + aX, top])
    builder.LineTo([builder.X + aX, baseline])

    left = builder.X + smallSpacing - 2;
    M();

    // E
    left = builder.X + smallSpacing;
    const eWidth = 13;
    const eWidth2 = 7;
    builder.MoveTo([left + eWidth, baseline])
    builder.LineToRelative([-eWidth, 0])
    builder.LineToRelative([0, -smallLetterHeight])
    builder.LineToRelative([eWidth, 0])
    builder.MoveTo([left, baseline - smallLetterHeight / 2])
    builder.LineToRelative([eWidth2, 0])
}