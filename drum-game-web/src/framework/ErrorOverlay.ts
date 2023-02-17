let overlay: HTMLDivElement | undefined = undefined;

interface ErrorMessage {
    message: string
    source?: string
    location?: string
}

export function ErrorOverlay(error: ErrorMessage | string) {
    if (overlay === undefined) {
        overlay = document.createElement("div");
        overlay.id = "error-overlay";
        document.body.appendChild(overlay);
    }

    const message = typeof error === "string" ? error : error.message;
    const config: ErrorMessage | undefined = typeof error === "string" ? undefined : error;

    const errorDiv = document.createElement("div");
    errorDiv.className = "error-message"
    errorDiv.textContent = message;
    overlay.appendChild(errorDiv);

    throw error;
}