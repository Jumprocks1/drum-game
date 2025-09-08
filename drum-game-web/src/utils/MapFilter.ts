import { CacheMap } from "../interfaces/Cache";


interface FilterOperator {
    identifier: string
    applyNumeric?: (a: number, b: number) => boolean
}
interface FilterField {
    name: string
    type: "string" | "number" | "date"
    alias?: string
    key: keyof CacheMap
    accessor?: (map: CacheMap) => any
}

const Operators: FilterOperator[] = [
    { identifier: "!=", applyNumeric: (a, b) => a !== b },
    { identifier: "=", applyNumeric: (a, b) => a === b },
    { identifier: ">=", applyNumeric: (a, b) => a >= b },
    { identifier: ">", applyNumeric: (a, b) => a > b },
    { identifier: "<=", applyNumeric: (a, b) => a <= b },
    { identifier: "<", applyNumeric: (a, b) => a < b },
    // new SortOp("^^", true),
    // new SortOp("^", false),
]

const Fields: FilterField[] = [
    // alias/name have to be lowercase
    { key: "CreationTime", name: "creationtime", alias: "addedon", type: "date" },
    { key: "MedianBPM", name: "bpm", type: "number" }
]

// returns ms since epoch
function parseTimeSpan(value: string) {
    let offset = Number.NaN
    let i = 0;
    while (i < value.length - 1 && /[a-zA-Z]/.test(value[value.length - i - 1]))
        i++;
    const units = value.substring(value.length - i);
    const d = parseFloat(value.substring(0, value.length - i))
    if (isNaN(d)) return NaN

    if (units === "s")
        offset = d * 1000
    else if (units === "m")
        offset = d * 1000 * 60
    else if (units === "h")
        offset = d * 1000 * 60 * 60
    else if (units === "d")
        offset = d * 1000 * 60 * 60 * 24
    else if (units === "ms" || units === "")
        offset = d

    if (isNaN(offset)) return NaN
    return new Date().getTime() - offset
}

function ApplyFilter(maps: CacheMap[], operator: FilterOperator, field: FilterField, value: string): CacheMap[] {
    const key = field.key
    const accessor = field.accessor ?? ((e: any) => e[key])
    let numericValue = NaN
    if (field.type === "date")
        numericValue = parseTimeSpan(value)
    else if (field.type === "number")
        numericValue = parseFloat(value)
    if (operator.applyNumeric) {
        if (isNaN(numericValue)) return []
        const apply = operator.applyNumeric;
        return maps.filter(e => apply(accessor(e), numericValue))
    }
    return maps
}

function LookupField(field: string): FilterField | undefined {
    for (const s of Fields) if (field === s.name || field === s.alias) return s;
    for (const s of Fields) if (s.name.startsWith(field) || (s.alias && s.alias.startsWith(field))) return s;
    for (const s of Fields) if (s.name.includes(field) || (s.alias && s.alias.includes(field))) return s;
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
    try {
        for (const s of query) {

            let opIndex = Number.MAX_SAFE_INTEGER;
            let op: FilterOperator | undefined;
            for (const o of Operators) {
                let index = s.indexOf(o.identifier);
                if (index >= 0 && index < opIndex) {
                    op = o;
                    opIndex = index;
                }
            }
            if (op == null) {
                res = res.filter(e => e.FilterString!.includes(s));
            }
            else {
                const fieldName = s.substring(0, opIndex).trim()
                const field = LookupField(fieldName)
                const value = s.substring(opIndex + op.identifier.length).trim()
                if (field && value) {
                    res = ApplyFilter(res, op, field, value);
                }
            }
        }
    } catch (e) {
        console.error(e) // if one of the filters fails, just return what we've got
    }
    return res;
}
