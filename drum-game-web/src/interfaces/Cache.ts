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
    PlayableDuration?: number
    Spotify?: string
    MedianBPM?: number
    ImageUrl?: string
    DownloadUrl?: string
    Date?: string
    FilterString?: string
    Tags?: string
    RomanTitle?: string
    RomanArtist?: string
    WriteTime: number
    Audio: string
    FileName?: string
    // C# ticks on load, gets converted to JS ms. If it's null it gets converted to 0 when processing request list
    // currently not set for non-request list stuff
    CreationTime?: number
}

export function CacheMapLink(map: CacheMap) {
    return map.FileName?.substring(0, map.FileName.lastIndexOf("."));
}