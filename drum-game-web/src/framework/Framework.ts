import Component from "./Component";
import { createElement, createFragment } from "./createElement";

type EventMap = {
    newframe: number,
    keydown: WindowEventMap["keydown"]
    resize: WindowEventMap["resize"]
    wheel: WindowEventMap["wheel"]
}

const eventListeners: {
    [K in keyof EventMap]?: ((ev: EventMap[K]) => boolean | void)[]
} = {}

const registeredListeners: Partial<Record<keyof EventMap, boolean>> = {}

function registerGlobalListener(type: keyof EventMap) {
    if (registeredListeners[type]) return;
    registeredListeners[type] = true;
    if (type === "newframe") return;
    window.addEventListener(type, e => {
        const listeners = eventListeners[type];
        if (listeners) {
            for (let i = listeners.length - 1; i >= 0; i--) {
                const l = listeners[i] as any;
                if (l(e)) {
                    return;
                }
            }
        }
    });
}

interface FrameworkConfig {
    baseName: string
}

export const FrameworkConfig: FrameworkConfig = {
    baseName: ""
}

function loadConfig(config: Partial<FrameworkConfig>) {
    let baseName = config.baseName;
    if (baseName) {
        if (!baseName.startsWith("/") && baseName !== "") baseName = "/" + baseName;
        FrameworkConfig.baseName = baseName;
    }
}

export function Start(root: new () => Component, config?: Partial<FrameworkConfig>) {
    if (config) loadConfig(config);
    window.createElement = createElement;
    window.createFragment = createFragment;

    const r = new root();
    r.Parent = r;

    // there's very little reason to not just keep this registered the whole time, so we register it at the start
    let lastTime = new Date().getTime();
    function callback() {
        const listeners = eventListeners["newframe"];
        const newTime = new Date().getTime();
        const dt = newTime - lastTime;
        if (listeners) {
            for (let i = listeners.length - 1; i >= 0; i--)
                listeners[i](dt)
        }
        lastTime = newTime;
        requestAnimationFrame(callback);
    }
    requestAnimationFrame(callback);
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
    }
}

export function StartDrag(down: MouseEvent, onMove: (e: MouseEvent) => void, onRelease?: (e: MouseEvent) => void,
    minimumDelta = 0, onClick?: (e: MouseEvent) => void) {
    down.preventDefault();
    down.stopPropagation();
    let button = down.button;
    const delta2 = minimumDelta * minimumDelta;

    function move(e: MouseEvent) {
        if (minimumDelta > 0) {
            const x = e.clientX - down.clientX;
            const y = e.clientY - down.clientY;
            if (x * x + y * y < delta2) return;
            minimumDelta = 0; // after breaking the threshold, we remove it
        }
        onMove(e);
    }
    function release(e: MouseEvent) {
        if (e.button !== button) return;

        if (minimumDelta > 0) onClick?.(e);
        else onRelease?.(e);

        window.removeEventListener("mousemove", move);
        window.removeEventListener("mouseup", release);
    }

    window.addEventListener("mousemove", move);
    window.addEventListener("mouseup", release);

    move(down);
}

function touchHandler(event: TouchEvent) {
    const touch = event.changedTouches[0];
    let type;
    switch (event.type) {
        case "touchstart": type = "mousedown"; break;
        case "touchmove": type = "mousemove"; break;
        case "touchend": type = "mouseup"; break;
        default: return;
    }

    const simulatedEvent = new MouseEvent(type, {
        bubbles: true, cancelable: true, view: window,
        screenX: touch.screenX,
        screenY: touch.screenY,
        clientX: touch.clientX,
        clientY: touch.clientY
    });

    touch.target.dispatchEvent(simulatedEvent);
    event.preventDefault();
}

export function BindTouchEvents(element: HTMLElement) {
    element.ontouchstart = touchHandler;
    element.ontouchmove = touchHandler;
    element.ontouchend = touchHandler;
    element.ontouchcancel = touchHandler;
}