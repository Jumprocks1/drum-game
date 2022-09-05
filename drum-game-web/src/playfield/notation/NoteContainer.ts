// group of notes that can be rendered to a canvas
// meant to be grouped each ~4 beats (but not necessarily by measure)

import { Note } from "../../interfaces/BJson"
import Beatmap from "../../utils/Beatmap"
import ChannelInfo from "../../utils/ChannelInfo";
import NotationDisplay from "./NotationDisplay";

// contains multiple NoteGroups
export default class RenderGroup {
    NoteGroups: NoteGroup[] = []

    Geometry: [] | undefined = undefined;

    ComputeGeometry() { // basically prepares for Render() ahead of time
        this.Geometry = []
    }

    Render(display: NotationDisplay, context: CanvasRenderingContext2D) {
        if (!this.Geometry) this.ComputeGeometry();
        const bravura = display.Bravura;
        const lookup = ChannelInfo.ChannelNoteMapping
        for (const group of this.NoteGroups) {
            const down = group.voice;
            const dir = down ? 1 : -1;

            for (const flag of group.flags) {
                for (const note of flag.notes) {
                    const l = lookup[note.channel];
                    context.fillText(l[1], note.time * display.Spacing, l[0])
                }
            }
        }
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
                currentGroup = new RenderGroup();
                o.push(currentGroup);
            }

            if (beat !== currentBeat) {
                currentNoteGroups = {};
                currentBeat = beat;
            }

            const voice = note.voice;
            if (!(voice in currentNoteGroups)) {
                const newGroup = { flags: [], beat, voice };
                currentGroup.NoteGroups.push(newGroup);
                currentNoteGroups[voice] = newGroup
            }
            addNote(currentNoteGroups[voice], note)
        }

        return o
    }
}

function addNote(noteGroup: NoteGroup, note: Note) {
    const lastFlag = noteGroup.flags[noteGroup.flags.length - 1];
    if (!lastFlag || lastFlag.tick !== note.tick) {
        noteGroup.flags.push({ tick: note.tick, notes: [note] })
    } else {
        lastFlag.notes.push(note);
    }
}

interface NoteGroup {
    flags: Flag[]
    beat: number // this number is what is used to push NoteGroups into RenderGroups. Simply divide it by 4 and round down to get the RenderGroup
    voice: number
}
interface Flag {
    notes: Note[]
    tick: number
}