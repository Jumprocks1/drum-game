// group of notes that can be rendered to a canvas
// meant to be grouped each ~4 beats (but not necessarily by measure)

import { Note } from "../../interfaces/BJson"
import Beatmap from "../../utils/Beatmap"
import ChannelInfo from "../../utils/ChannelInfo";
import NotationDisplay from "./NotationDisplay";

const stemHeight = 2.5;
const AugmentationDotGap = 0.1;
const AccentGap = 0.1;

// contains multiple NoteGroups
export default class RenderGroup {
    NoteGroups: NoteGroup[] = []

    Geometry: [] | undefined = undefined;

    ComputeGeometry() { // basically prepares for Render() ahead of time
        this.Geometry = []
    }

    Index: number;
    constructor(index: number) {
        this.Index = index;
    }

    first = true;

    Render(display: NotationDisplay, context: CanvasRenderingContext2D) {
        if (!this.Geometry) this.ComputeGeometry();
        const font = display.Font;
        const engravingDefaults = font.engravingDefaults;
        const spacing = display.Spacing;
        for (const group of this.NoteGroups) {
            const flags = group.flags;
            const down = group.voice === 1;
            const dir = down ? 1 : -1;
            const beamHeight = -dir * engravingDefaults.beamThickness

            const targetHeight = group.highestNote + dir * stemHeight

            let beamLeft = Number.MAX_VALUE;
            let beamRight = Number.MIN_VALUE;

            // there's like 200 lines of code we need to add here
            for (const flag of flags) {
                let bottomNote = flag.notes[0];
                for (const note of flag.notes) {
                    if (down ? note.noteMapping[0] < bottomNote.noteMapping[0] : note.noteMapping[0] > bottomNote.noteMapping[0])
                        bottomNote = note;
                }
                const bottomAnchor = display.GetNoteheadAnchor(bottomNote.noteMapping[1], down);
                const bottomX = flag.beat * spacing; // this is where we will render the bottom note for certain

                let accent = false;

                for (const note of flag.notes) {
                    accent ||= note.modifier === "accent"
                    const l = note.noteMapping;
                    const anchor = display.GetNoteheadAnchor(l[1], down)[0];
                    const noteX = bottomX + bottomAnchor[0] - anchor;
                    context.fillText(l[1], noteX, l[0])
                    if (note.modifier === "ghost")
                        context.fillText("\uE0CE", noteX, l[0])

                    // handle note dotting
                    if (flag.duration == 0.75 || flag.duration == 0.375) {
                        const rightSide = down ? font.glyphsWithAnchors[ChannelInfo.CodepointMap[l[1]]].stemUpSE[0] : anchor;
                        let dotY = l[0];
                        console.log(dotY)
                        if (Number.isInteger(dotY)) dotY += dir / 2; // prevent dot being placed on a line
                        context.fillText("\uE1E7", noteX + rightSide + AugmentationDotGap, l[0])
                    }
                }
                let flagLeft = bottomX + bottomAnchor[0];
                flag.flagLeft = flagLeft;
                if (!down) flagLeft -= engravingDefaults.stemThickness;
                beamLeft = Math.min(beamLeft, flagLeft);
                beamRight = Math.max(beamRight, flagLeft + engravingDefaults.stemThickness);
                // stem
                context.fillRect(flagLeft, bottomNote.noteMapping[0] - bottomAnchor[1], engravingDefaults.stemThickness,
                    targetHeight - bottomNote.noteMapping[0] + bottomAnchor[1]);


                let flagGlyph: any = undefined;
                if (flags.length == 1) // fancy flag
                {
                    if (flag.duration! <= 0.375)
                        flagGlyph = down ? "\uE243" : "\uE242";
                    else if (flag.duration! <= 0.75)
                        flagGlyph = down ? "\uE241" : "\uE240";
                    if (flagGlyph) {
                        // this extension should technically be applied to targetHeight prior to this
                        const extension = down ? 0.5 : -0.5;
                        const y = targetHeight + extension;
                        const name = ChannelInfo.CodepointMap[flagGlyph];
                        const anchor = down ? font.glyphsWithAnchors[name].stemDownSW : font.glyphsWithAnchors[name].stemUpNW;
                        context.fillText(flagGlyph, flagLeft - anchor[0], y + anchor[1]);
                    }
                }
                if (accent) {
                    const accentHeight = targetHeight + dir * (AccentGap +
                        (flags.length > 1 || flagGlyph ? engravingDefaults.beamThickness : 0));
                    context.fillText(down ? "\uE4A1" : "\uE4A0", bottomX, accentHeight);
                }
            }

            if (flags.length > 1) { // beam
                context.fillRect(beamLeft, targetHeight, beamRight - beamLeft, -beamHeight);
                let beamY = targetHeight - dir * (engravingDefaults.beamSpacing);
                let duration = 0.25;
                // depth 1 = 16th, depth 2 = 32nd
                for (let depth = 1; depth <= 2; depth++) {
                    let added = false;
                    let beamStart = 0;
                    let beamCount = 0;
                    for (let i = 0; i < flags.length; i++) {
                        const flag = flags[i];
                        if (flag.duration! <= duration || flag.duration! == duration * 1.5) {
                            if (beamCount == 0) beamStart = flag.flagLeft;
                            beamCount += 1;
                        } else {
                            // there's a note that is not part of the current beam, so we have to end it
                            if (beamCount > 0) {
                                var end = beamCount == 1 ? (beamStart + flag.flagLeft + engravingDefaults.stemThickness) / 2 : flags[i - 1].flagLeft;

                                context.fillRect(beamStart, beamY, end - beamStart, beamHeight);
                                added = true;
                                beamCount = 0;
                            }
                        }
                    }
                    // we finished going through the notes and there is an active beam
                    if (beamCount > 0) {
                        if (beamCount == 1) {
                            const a = beamStart - spacing * duration * 0.5
                            context.fillRect(a, beamY, beamStart - a, beamHeight);
                            added = true;
                        }
                        else {
                            var end = flags[flags.length - 1].flagLeft;
                            context.fillRect(beamStart, beamY, beamStart - end, beamHeight);
                            added = true;
                        }
                    }
                    if (!added) break; // if we didn't add any beams, there won't be any more
                    duration *= 0.5;
                    beamY += dir * -(engravingDefaults.beamSpacing + engravingDefaults.beamThickness);
                }
            }
        }
        this.first = false;
    }

    // start and end are the RenderGroup index, with index 0 = beats 0-4 = ticks 0-57600
    // note, only the NoteGroup's tick is used for assigning the render group index
    // since a NoteGroup can be up to 1 full beat, it could start a 3.999 and push a full beat into the next group's rendering area
    // see `ReloadNotes` in MusicNotationBeatmapDisplay.cs for how to calculate the index from a tick
    static BuildRenderGroups(beatmap: Beatmap, startIndex = 0, endIndex?: number) {
        const o: RenderGroup[] = []

        let currentGroup = (undefined as any) as RenderGroup;
        let currentNoteGroups: { [key: number]: NoteGroup } = {}
        let currentBeat = Number.MIN_VALUE;

        for (const note of beatmap.HitObjects) {
            if (note.time < 0) continue;

            // TODO this will not work after we add MeasureChanges, see `ReloadNotes`
            // with fractional measures, this can be something like 3.5
            const beat = Math.floor(note.time);

            const container = Math.floor(beat / 4);
            while (container !== (o.length - 1)) { // push current/empty containers until we get to where we need to be
                currentGroup = new RenderGroup(o.length);
                o.push(currentGroup);
            }

            if (beat !== currentBeat) {
                currentNoteGroups = {};
                currentBeat = beat;
            }

            const voice = note.voice;
            if (!(voice in currentNoteGroups)) {
                // TODO end is not correct with MeasureChanges
                const newGroup: NoteGroup = { flags: [], beat, voice, highestNote: note.noteMapping[0], end: beat + 1 };
                currentGroup.NoteGroups.push(newGroup);
                currentNoteGroups[voice] = newGroup
            }
            addNote(currentNoteGroups[voice], note)
        }

        for (const e of o) {
            for (const group of e.NoteGroups) {
                for (let i = 0; i < group.flags.length; i++) {
                    if (i === group.flags.length - 1) {
                        group.flags[i].duration = group.end - group.flags[i].beat
                    } else {
                        group.flags[i].duration = group.flags[i + 1].beat - group.flags[i].beat
                    }
                }
            }
        }

        return o
    }
}

function addNote(noteGroup: NoteGroup, note: Note) {
    const lastFlag = noteGroup.flags[noteGroup.flags.length - 1];
    if (note.voice === 1) {
        if (note.noteMapping[0] > noteGroup.highestNote)
            noteGroup.highestNote = note.noteMapping[0]
    } else {
        if (note.noteMapping[0] < noteGroup.highestNote)
            noteGroup.highestNote = note.noteMapping[0]
    }
    if (!lastFlag || lastFlag.beat !== note.time) {
        noteGroup.flags.push({ beat: note.time, notes: [note] } as Flag)
    } else {
        lastFlag.notes.push(note);
    }
}

interface NoteGroup {
    flags: Flag[]
    highestNote: number
    beat: number // this number is what is used to push NoteGroups into RenderGroups. Simply divide it by 4 and round down to get the RenderGroup
    end: number // used to calculate flag durations
    voice: number
}
interface Flag {
    notes: Note[]
    beat: number
    duration?: number
    flagLeft: number
}