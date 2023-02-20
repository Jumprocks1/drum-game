import { ErrorOverlay } from "../framework/ErrorOverlay";

export function CompileShader(gl: WebGLRenderingContext, source: string, type: GLenum) {
    const shader = gl.createShader(type)!;
    gl.shaderSource(shader, source);
    gl.compileShader(shader);

    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        const info = gl.getShaderInfoLog(shader);
        ErrorOverlay(`Could not compile ${type === gl.VERTEX_SHADER ? "vertex" : "frag"} WebGL program.\n${info}`);
    }
    return shader;
}

export function ShaderProgram(gl: WebGLRenderingContext, vertex: WebGLShader | string, frag: WebGLShader | string) {
    const program = gl.createProgram()!;
    if (typeof vertex === "string")
        vertex = CompileShader(gl, vertex, gl.VERTEX_SHADER);
    gl.attachShader(program, vertex);
    if (typeof frag === "string")
        frag = CompileShader(gl, frag, gl.FRAGMENT_SHADER);
    gl.attachShader(program, frag);
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS))
        ErrorOverlay(`Unable to initialize the shader program: ${gl.getProgramInfoLog(program)}`);
    return program;
}

export function FrameBufferTexture(gl: WebGLRenderingContext) {
    const tex = gl.createTexture();
    if (tex === null) throw "Failed to create texture";
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.canvas.width, gl.canvas.height, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    return tex;
}