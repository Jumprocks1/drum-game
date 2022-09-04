import Component from "./Component";
import { createElement } from "./createElement";

type EventMap = {
    newframe: undefined,
    keydown: WindowEventMap["keydown"]
}

const eventListeners: {
    [K in keyof EventMap]?: ((ev: EventMap[K]) => boolean | void)[]
} = {}

const registeredListeners: Partial<Record<keyof EventMap, boolean>> = {}

function registerGlobalListener(type: keyof EventMap) {
    if (registeredListeners[type]) return;
    registeredListeners[type] = true;
    if (type === "newframe") {
        function callback() {
            const listeners = eventListeners["newframe"];
            if (listeners) {
                for (let i = listeners.length - 1; i >= 0; i--)
                    listeners[i](undefined)
            }
            if (registeredListeners["newframe"])
                requestAnimationFrame(callback);
        }
        requestAnimationFrame(callback);
    } else {
        window.addEventListener(type, e => {
            const listeners = eventListeners[type];
            if (listeners) {
                for (let i = listeners.length - 1; i >= 0; i--) {
                    const l = listeners[i];
                    if (l(e)) {
                        return;
                    }
                }
            }
        });
    }
}

function removeGlobalListener(type: keyof EventMap) {
    if (type === "newframe")
        registeredListeners[type] = false;
}

interface FrameworkConfig {
    baseName?: string
}

export const FrameworkConfig: FrameworkConfig = {}

function loadConfig(config: FrameworkConfig) {
    let baseName = config.baseName;
    if (baseName) {
        if (!baseName.startsWith("/")) baseName = "/" + baseName;
        FrameworkConfig.baseName = baseName;
    }
}

export function Start(root: new () => Component, config?: FrameworkConfig) {
    if (config) loadConfig(config);
    window.createElement = createElement;
    const r = new root();
    r.Parent = r;
}

export function RegisterListener<K extends keyof EventMap>(type: K, handler: (ev: EventMap[K]) => boolean | void) {
    const arr = eventListeners[type];
    if (arr) arr.push(handler);
    // @ts-ignore
    else eventListeners[type] = [handler];
    if (!arr || arr.length === 1)
        registerGlobalListener(type);
}

export function RemoveListener<K extends keyof EventMap>(type: K, handler: (ev: EventMap[K]) => boolean | void) {
    const arr = eventListeners[type];
    if (arr) {
        const i = arr.indexOf(handler);
        if (i !== -1) arr.splice(i, 1);
        if (arr.length === 0) removeGlobalListener(type);
    }
}

export function StartDrag(down: MouseEvent, onMove: (e: MouseEvent) => void, onRelease?: (e: MouseEvent) => void) {
    down.preventDefault();
    let button = down.button;

    function move(e: MouseEvent) {
        onMove(e);
    }
    function release(e: MouseEvent) {
        if (e.button !== button) return;
        onRelease?.(e);
        window.removeEventListener("mousemove", move);
        window.removeEventListener("mouseup", release);
    }

    window.addEventListener("mousemove", move);
    window.addEventListener("mouseup", release);

    move(down);
}