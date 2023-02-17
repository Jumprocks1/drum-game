import { ErrorOverlay } from "../../framework/ErrorOverlay";
import shaderSource from "./Shader.frag"

type WebGL = WebGL2RenderingContext

const vertexShaderSource = `#version 300 es
in vec2 position;
in vec3 vertexColor;

out lowp vec3 vColor;
out highp vec2 uv;

void main() {
    gl_Position = vec4(position,0.0,1.0);
    uv = position;
    
    vColor = vertexColor;
}
`;


export class LogoBackground {

    get GL() {
        if (this._gl == null) throw "Failed to create WebGL context";
        return this._gl;
    }

    _gl: WebGL | null = null;


    VertexBuffer: WebGLBuffer | null = null
    Program: WebGLProgram | null = null;

    StartTime: number = Date.now() - Math.random() * 1000_000;

    Init(gl: WebGL | null) {
        if (gl == null) throw "Failed to create WebGL context";
        this._gl = gl;
        // only need to call this on resize
        gl.viewport(0, 0, gl.canvas.width, gl.canvas.height);

        if (this.VertexBuffer) return;

        this.VertexBuffer = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, this.VertexBuffer);
        const positions = [1.0, 1.0, -1.0, 1.0, 1.0, -1.0, -1.0, -1.0];
        gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(positions), gl.STATIC_DRAW);

        this.InitProgram();
    }

    public Draw() {
        const gl = this.GL;

        gl.clearColor(0.0, 0.0, 0.0, 1.0); // Clear to black, fully opaque
        gl.clear(gl.COLOR_BUFFER_BIT);

        // Tell WebGL to use our program when drawing
        gl.useProgram(this.Program);

        gl.bindBuffer(gl.ARRAY_BUFFER, this.VertexBuffer);
        gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
        gl.enableVertexAttribArray(0);

        gl.uniform1f(this.TimeUniform, (Date.now() - this.StartTime) / 1000);

        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
    }

    TimeUniform: WebGLUniformLocation | null = null;

    InitProgram() {
        const gl = this.GL;

        if (this.Program) return;

        const vertex = this.compileShader(vertexShaderSource, this.GL.VERTEX_SHADER);
        const frag = this.compileShader(shaderSource, this.GL.FRAGMENT_SHADER);


        this.Program = this.GL.createProgram()!;
        gl.attachShader(this.Program, vertex);
        gl.attachShader(this.Program, frag);
        gl.linkProgram(this.Program);

        if (!gl.getProgramParameter(this.Program, gl.LINK_STATUS)) {
            ErrorOverlay(
                `Unable to initialize the shader program: ${gl.getProgramInfoLog(
                    this.Program
                )}`
            );
        }

        this.TimeUniform = gl.getUniformLocation(this.Program, "iTime");
    }

    compileShader(source: string, type: GLenum) {
        const shader = this.GL.createShader(type)!;
        this.GL.shaderSource(shader, source);
        this.GL.compileShader(shader);

        if (!this.GL.getShaderParameter(shader, this.GL.COMPILE_STATUS)) {
            const info = this.GL.getShaderInfoLog(shader);
            ErrorOverlay(`Could not compile ${type === this.GL.VERTEX_SHADER ? "vertex" : "frag"} WebGL program.\n${info}`);
        }
        return shader;
    }
}
