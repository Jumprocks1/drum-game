export default interface Cache {
    Version: number
    Maps: Record<string, CacheMap>
}

export interface CacheMap {
    Id: string
    Title: string
    Artist: string
    Mapper?: string
    Difficulty: number
    DifficultyString?: string
    SpotifyTrack?: string
    BPM?: number
    ImageUrl?: string
    DownloadUrl?: string
    Date?: string
    FilterString?: string
    Tags?: string
    WriteTime: number
    Audio: string
    FileName: string
}

export function CacheMapLink(map: CacheMap) {
    return map.FileName.substring(0, map.FileName.lastIndexOf("."));
}