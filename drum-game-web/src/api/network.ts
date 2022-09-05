import { FrameworkConfig } from "../framework/Framework"
import { BJson } from "../interfaces/BJson"

export const UNAUTHORIZED = 401
export const FORBIDDEN = 403

export type HttpMethod = "get" | "post" | "delete"

export interface FetchConfig {
    method?: HttpMethod,
    body?: any
}

export async function fetchEndpoint<T = {}>(endpoint: string,
    { method = "get", body }: FetchConfig = {}) {

    if (endpoint.startsWith("/") && FrameworkConfig.baseName)
        endpoint = FrameworkConfig.baseName + endpoint;

    const headers = new Headers();
    const init: RequestInit = { headers, method }
    if (body) {
        if (body instanceof FormData) {
            init.body = body;
        } else {
            headers.append("Content-Type", "application/json")
            init.body = JSON.stringify(body);
        }
    }
    const res = await fetch(endpoint, init);
    if (!res.ok) {
        const e = new Error(await res.json());
        e.name = res.status.toString();
        throw e;
    }
    try {
        return await res.json() as T;
    } catch (e) {
        console.error(e)
        return undefined;
    }
}



export async function loadMap(fileName: string): Promise<BJson> {
    if (!fileName.endsWith(".bjson")) fileName += ".bjson"
    console.log(`Loading ${fileName}`)

    const url = `/maps/${fileName}`
    const res = await fetchEndpoint<BJson>(url);
    if (!res) throw new Error("failed to load");

    return res;
}