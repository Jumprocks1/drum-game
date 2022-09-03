export default interface SMuFL {
    fontName: string
    fontVersion: number
    engravingDefaults: EngravingDefaults
}

interface EngravingDefaults {
    arrowShaftThickness: number
    barlineSeparation: number
    beamSpacing: number
    beamThickness: number
    bracketThickness: number
    dashedBarlineDashLength: number
    dashedBarlineGapLength: number
    dashedBarlineThickness: number
    hairpinThickness: number
    legerLineExtension: number
    legerLineThickness: number
    lyricLineThickness: number
    octaveLineThickness: number
    pedalLineThickness: number
    repeatBarlineDotSeparation: number
    repeatEndingLineThickness: number
    slurEndpointThickness: number
    slurMidpointThickness: number
    staffLineThickness: number
    stemThickness: number
    subBracketThickness: number
    textEnclosureThickness: number
    thickBarlineThickness: number
    thinBarlineThickness: number
    tieEndpointThickness: number
    tieMidpointThickness: number
    tupletBracketThickness: number
}