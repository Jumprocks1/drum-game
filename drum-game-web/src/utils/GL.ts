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

export function ShaderProgram(gl: WebGLRenderingContext, vertex: WebGLShader, frag: WebGLShader) {
    const program = gl.createProgram()!;
    gl.attachShader(program, vertex);
    gl.attachShader(program, frag);
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS))
        ErrorOverlay(`Unable to initialize the shader program: ${gl.getProgramInfoLog(program)}`);
    return program;
}