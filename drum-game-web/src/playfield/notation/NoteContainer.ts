// group of notes that can be rendered to a canvas
// meant to be grouped each ~4 beats (but not necessarily by measure)

import { Note } from "../../interfaces/BJson"
import Beatmap from "../../utils/Beatmap"
import ChannelInfo from "../../utils/ChannelInfo";
import NotationDisplay from "./NotationDisplay";

// contains multiple NoteGroups
export default class NoteContainer {
    NoteGroups: NoteGroup[] = []

    Render(display: NotationDisplay, context: CanvasRenderingContext2D) {
        const lookup = ChannelInfo.ChannelNoteMapping
        for (const group of this.NoteGroups) {
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
        const o: NoteContainer[] = []
        const tickRate = beatmap.TickRate;

        let currentGroup = new NoteContainer();

        for (const note of beatmap.HitObjects) {
            // TODO this will not work after we add MeasureChanges, see `ReloadNotes`
            // with fractional measures, this can be something like 3.5
            const beat = Math.floor(note.time);
            const container = Math.floor(beat / 4);
            while (container !== o.length) { // push current/empty containers until we get to where we need to be
                o.push(currentGroup);
                currentGroup = new NoteContainer();
            }
            currentGroup.NoteGroups.push({ flags: [{ notes: [note] }] })
        }
        o.push(currentGroup);

        return o
    }
}

interface NoteGroup {
    flags: Flag[]
}
interface Flag {
    notes: Note[]
}