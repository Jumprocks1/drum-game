// group of notes that can be rendered to a canvas
// meant to be grouped each ~4 beats (but not necessarily by measure)

import { Note } from "../../interfaces/BJson"
import Beatmap from "../../utils/Beatmap"
import ChannelInfo from "../../utils/ChannelInfo";
import NotationDisplay from "./NotationDisplay";

const stemHeight = 2.5;

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
        const lookup = ChannelInfo.ChannelNoteMapping
        const tickRate = display.Beatmap.TickRate;
        const spacing = display.Spacing;
        const offset = this.Index * 5;
        for (const group of this.NoteGroups) {
            const down = group.voice;
            const dir = down ? 1 : -1;

            const targetHeight = group.highestNote + dir * stemHeight

            var beamLeft = Number.MAX_VALUE;
            var beamRight = Number.MIN_VALUE;

            // there's like 200 lines of code we need to add here
            for (const flag of group.flags) {
                for (const note of flag.notes) {
                    const l = note.noteMapping;
                    context.fillText(l[1], note.time * spacing, l[0])
                }
                // var bottomAnchor = GetNoteheadAnchor(flag.BottomNote.Note.Glyph, down);
                // context.fillRect(flag.beat * spacing, targetHeight, flag.duration! * spacing, 0.2)
            }
            // context.fillRect(group.flags[0].beat * spacing, targetHeight, 0.5, 0.5)
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
        noteGroup.flags.push({ beat: note.time, notes: [note] })
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
}