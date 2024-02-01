import { CacheMap } from "../interfaces/Cache";

export function FormatTime(ms: number) {
    const t = ms > 0 ? Math.floor(ms / 1000) : 0;
    const d = Math.floor(t / 60);
    const s = Math.floor(t - d * 60)
    return `${d}:${s >= 10 ? s : "0" + s}`;
}

export function Clamp(n: number, min: number, max: number) { return Math.max(Math.min(n, max), min); }


export function ExpLerp(current: number, target: number, pow: number, dt: number, linearStep = 0) {
    if (current == target) return current;
    const blend = Math.pow(pow, dt); // 0.99 means we will move 1% percent towards target for each ms
    current = target * (1 - blend) + current * blend;

    if (linearStep > 0) {
        linearStep *= dt; // this gives us a very small linear movement, which helps stabilize
        const diff = target - current;
        if (Math.abs(diff) < linearStep)
            current = target;
        else
            current += Math.sign(diff) * linearStep;
    }

    return current;
}

export function Filter(search: string, maps: CacheMap[]) {
    const query = search.toLowerCase().split(" ").filter(e => e.length > 0);
    if (maps.length > 0 && maps[0].FilterString === undefined) {
        for (const map of maps) {
            if (map.FilterString === undefined)
                map.FilterString = `${map.Title ?? ""} ${map.Artist ?? ""} ${map.Mapper ?? ""} ${map.DifficultyString ?? ""} ${map.Tags ?? ""} ${map.RomanTitle ?? ""} ${map.RomanArtist ?? ""}`.toLowerCase();
        }
    }
    let res = maps;
    for (const s of query) {
        res = res.filter(e => e.FilterString!.includes(s));
    }
    return res;
}

export function EnsureParent(parent: Node, child: Node, setParent: boolean = true) {
    if (setParent) {
        if (child.parentNode !== parent) parent.appendChild(child);
    } else {
        if (child.parentNode === parent) parent.removeChild(child)
    }
}

export function KebabCase(s: string) {
    let o = "";
    for (let i = 0; i < s.length; i++) {
        const lower = s[i].toLowerCase();
        if (lower === s[i] || i === 0) o += lower;
        else o += "-" + lower
    }
    return o;
}