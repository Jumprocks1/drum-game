import { BJson, Note } from "../interfaces/BJson";
import ChannelInfo from "./ChannelInfo";

export default class Beatmap {
    TickRate: number = 14400

    Length = 4 // in beats

    BJson: BJson

    static readonly TimeToBeatFraction = 1 << 10;
    static readonly BeatEpsilon = 1 / Beatmap.TimeToBeatFraction;

    HitObjects: Note[] = []
    TempoChanges: TempoChange[] = []
    MeasureChanges: MeasureChange[] = [] // probably won't be supported at the start

    // load BJson stuff + tick rate
    constructor(json: BJson) {
        this.BJson = json;
        this.Init();
    }

    private Init() { // ported from `Beatmap.Init`
        const bjson = this.BJson;
        if (bjson.notes == null) throw new Error("Notes missing");

        // load note objects
        this.HitObjects = bjson.notes.map(e => {
            e.tick = this.BeatToTick(e.time);
            e.voice = e.channel === "bass" || e.channel === "hihat-pedal" ? 1 : 0
            e.noteMapping = ChannelInfo.ChannelNoteMapping[e.channel];
            return e;
        });

        this.HitObjects.sort((a, b) => a.tick - b.tick); // this is not a stable sort, but it should be okay
        if (this.HitObjects.length > 0)
            this.Length = Math.max(Math.floor(this.HitObjects[this.HitObjects.length - 1].time as number + 1), 4);

        this.loadTempo();
    }





    BeatToTick(beat: number) {
        return Math.floor(beat * this.TickRate + 0.5);
    }
    MsToBeat(ms: number) {
        ms -= this.BJson.offset;
        const tempos = this.TempoChanges;
        if (ms < 0) {
            const negativeTempo = tempos.length == 0 || tempos[0].tick > 0 ? defaultTempoChange : tempos[0];
            return ms * 1_000 / negativeTempo.microsecondsPerQuarterNote;
        }
        let tempo = defaultTempoChange;
        let lastTime = 0;
        for (const tempoChange of tempos) {
            const dt = tempoChange.tick - tempo.tick;
            const realTime = lastTime + dt / this.TickRate * tempo.microsecondsPerQuarterNote / 1_000;
            if (realTime >= ms)
                return tempo.tick / this.TickRate + (ms - lastTime) * 1_000 / tempo.microsecondsPerQuarterNote;
            tempo = tempoChange;
            lastTime = realTime;
        }
        return tempo.tick / this.TickRate + (ms - lastTime) * 1_000 / tempo.microsecondsPerQuarterNote;
    }
    BeatToMs(beats: number) {
        const tempos = this.TempoChanges;
        if (beats < 0) {
            const negativeTempo = tempos.length == 0 || tempos[0].tick > 0 ? defaultTempoChange : tempos[0];
            return beats * negativeTempo.microsecondsPerQuarterNote / 1_000 + this.BJson.offset;
        }
        let tempo = defaultTempoChange.microsecondsPerQuarterNote;
        const ticks = beats * this.TickRate;
        const quarterNote = this.TickRate;
        let realTime = 0.0;
        let lastEvent = 0;
        for (const ev of tempos) {
            if (ev.tick > ticks)
                return realTime + (ticks - lastEvent) / this.TickRate * tempo / 1000 + this.BJson.offset;

            const delta = ev.tick - lastEvent;
            if (delta > 0)
                realTime += delta / quarterNote * tempo / 1000;
            tempo = ev.microsecondsPerQuarterNote;
            lastEvent = ev.tick;
        }
        return realTime + (ticks - lastEvent) / this.TickRate * tempo / 1000 + this.BJson.offset;
    }

    private loadTempo() {
        const bjson = this.BJson;
        const bpmToken = bjson.bpm;
        if (bpmToken) {
            if (Array.isArray(bpmToken)) {
                for (const token of bpmToken) {
                    const bpm = token.bpm;
                    const time = token.time;
                    this.TempoChanges.push({ tick: this.BeatToTick(time), microsecondsPerQuarterNote: Math.round(60_000_000 / bpm) });
                }
            } else {
                this.TempoChanges.push({ tick: 0, microsecondsPerQuarterNote: Math.round(60_000_000 / bpmToken) })
            }
        }
        const measureChanges = bjson.measureConfig;
        if (measureChanges) {
            if (Array.isArray(measureChanges)) {
                for (const token of measureChanges) {
                    this.MeasureChanges.push({ tick: this.BeatToTick(token.time ?? 0), beats: token.beats });
                }
            } else {
                this.MeasureChanges.push({ tick: this.BeatToTick(measureChanges.time ?? 0), beats: measureChanges.beats });
            }
        }
    }
}
const defaultTempoChange: TempoChange = { tick: 0, microsecondsPerQuarterNote: 500_000 }
interface TempoChange {
    tick: number
    microsecondsPerQuarterNote: number
}
interface MeasureChange {
    tick: number
    beats: number
}