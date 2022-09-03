export function FormatTime(ms: number) {
    const t = ms > 0 ? Math.floor(ms / 1000) : 0;
    const d = Math.floor(t / 60);
    const s = Math.floor(t - d * 60)
    return `${d}:${s >= 10 ? s : "0" + s}`;
}

export function Clamp(n: number, min: number, max: number) { return Math.min(Math.max(n, min), max); }