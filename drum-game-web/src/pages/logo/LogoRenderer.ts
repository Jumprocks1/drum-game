import { ErrorOverlay } from "../../framework/ErrorOverlay";
import { Blur } from "../../utils/blur/Blur";
import { CompileShader, ShaderProgram } from "../../utils/GL";
import { Mesh } from "./LineMesh";
import shaderSource from "./Shader.frag"

type WebGL = WebGL2RenderingContext

const vertexShaderSource = `#version 300 es
in vec3 position;
in vec2 normal;

out highp vec3 vPos;
out highp vec2 vNormal;

void main() {
    gl_Position = vec4(position,1.0);
    vPos = position;
    vNormal = normal;
}
`;


export class LogoRenderer {

    get GL() {
        if (this._gl == null) throw "Failed to create WebGL context";
        return this._gl;
    }

    _gl: WebGL | null = null;


    PositionBuffer: WebGLBuffer | null = null
    NormalBuffer: WebGLBuffer | null = null
    Program: WebGLProgram | null = null;
    PositionCount = 4;

    Mesh: Mesh | null = null;


    StartTime: number = Date.now() - Math.random() * 1000_000;

    Init(gl: WebGL | null) {
        if (gl == null) throw "Failed to create WebGL context";

        const mesh = this.Mesh!;

        gl.enable(gl.DEPTH_TEST);
        gl.depthFunc(gl.GEQUAL);

        this._gl = gl;
        // only need to call this on resize
        gl.viewport(0, 0, gl.canvas.width, gl.canvas.height);

        if (this.PositionBuffer) return;

        this.PositionBuffer = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, this.PositionBuffer);
        const positions = [1.0, 1.0, -1.0, -1.0, 1.0, -1.0, 1.0, -1.0, -1.0, -1.0, 1.0, -1.0, 1.0, -1.0, -1.0, -1.0, -1.0, -1.0];
        const positionData = new Float32Array(positions.length + mesh.positions.length);
        positionData.set(positions);
        positionData.set(mesh.positions, positions.length);
        gl.bufferData(gl.ARRAY_BUFFER, positionData, gl.STATIC_DRAW);


        this.NormalBuffer = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, this.NormalBuffer);
        const normals = [0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0];
        const normalData = new Float32Array(positions.length + mesh.normals.length);
        normalData.set(normals);
        normalData.set(mesh.normals, normals.length);
        gl.bufferData(gl.ARRAY_BUFFER, normalData, gl.STATIC_DRAW);

        this.PositionCount = 6 + mesh.VertexCount;

        gl.clearColor(0.0, 0.0, 0.0, 1.0); // Clear to black, fully opaque
        gl.clearDepth(-1.);

        gl.bindTexture(gl.TEXTURE_2D, Blur(gl, () => this.ShadowDraw()));

        this.InitProgram();
    }

    ShadowDraw() {
        const gl = this.GL;
        gl.bindBuffer(gl.ARRAY_BUFFER, this.PositionBuffer);
        gl.vertexAttribPointer(0, 3, gl.FLOAT, false, 0, 0);
        gl.enableVertexAttribArray(0);
        gl.drawArrays(gl.TRIANGLES, 0, this.PositionCount);
    }

    public Draw() {
        const gl = this.GL;

        gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

        // Tell WebGL to use our program when drawing
        gl.useProgram(this.Program);

        gl.bindBuffer(gl.ARRAY_BUFFER, this.PositionBuffer);
        gl.vertexAttribPointer(0, 3, gl.FLOAT, false, 0, 0);
        gl.enableVertexAttribArray(0);

        gl.bindBuffer(gl.ARRAY_BUFFER, this.NormalBuffer);
        gl.vertexAttribPointer(1, 2, gl.FLOAT, false, 0, 0);
        gl.enableVertexAttribArray(1);

        gl.uniform1f(this.TimeUniform, (Date.now() - this.StartTime) / 1000);

        gl.drawArrays(gl.TRIANGLES, 0, this.PositionCount);
    }

    TimeUniform: WebGLUniformLocation | null = null;

    InitProgram() {
        const gl = this.GL;

        if (this.Program) return;

        const vertex = CompileShader(gl, vertexShaderSource, this.GL.VERTEX_SHADER);
        const frag = CompileShader(gl, shaderSource, this.GL.FRAGMENT_SHADER);
        this.Program = ShaderProgram(gl, vertex, frag);

        this.TimeUniform = gl.getUniformLocation(this.Program, "iTime");
    }
}
