import { ErrorOverlay } from "../../framework/ErrorOverlay";
import { Blur } from "../../utils/blur/Blur";
import { FrameBufferTexture, ShaderProgram } from "../../utils/GL";
import { Mesh } from "./LineMesh";
import shaderSource from "./Shader.frag"
import lineShaderSource from "./LineShader.frag"
import distortionSource from "./DistortionShader.frag"

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
const mainVertexShader = `#version 300 es
in vec2 position;
out highp vec2 uv;
void main() {
    gl_Position = vec4(position,0.,1.0);
    uv = position;
}
`;
const distortionVertex = `#version 300 es
in vec2 position;
out highp vec2 uv;

void main() {
    gl_Position = vec4(position,0.0,1.0);
    uv = (position + 1.) / 2.;
}
`;


export class LogoRenderer {

    get GL() {
        if (this._gl == null) throw "Failed to create WebGL context";
        return this._gl;
    }

    _gl: WebGL | null = null;


    QuadBuffer: WebGLBuffer | null = null

    PositionBuffer: WebGLBuffer | null = null
    NormalBuffer: WebGLBuffer | null = null

    Program: WebGLProgram | null = null;
    DistortionProgram: WebGLProgram | null = null;

    Framebuffer: WebGLFramebuffer | null = null;
    FramebufferTexture: WebGLTexture | null = null;

    LineTexture: WebGLTexture | null = null;

    BlurTexture: WebGLTexture | null = null;
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
        gl.bufferData(gl.ARRAY_BUFFER, mesh.positions, gl.STATIC_DRAW);


        this.NormalBuffer = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, this.NormalBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, mesh.normals, gl.STATIC_DRAW);

        this.QuadBuffer = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, this.QuadBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 1, -1, -1, 1, 1, 1]), gl.STATIC_DRAW);

        gl.clearColor(0.0, 0.0, 0.0, 1.0); // Clear to black, fully opaque
        gl.clearDepth(-1.);

        this.Framebuffer = gl.createFramebuffer();
        this.FramebufferTexture = FrameBufferTexture(gl);


        this.Program = ShaderProgram(gl, mainVertexShader, shaderSource);
        this.DistortionProgram = ShaderProgram(gl, distortionVertex, distortionSource);


        gl.bindFramebuffer(gl.FRAMEBUFFER, this.Framebuffer);
        gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, this.FramebufferTexture, 0);

        gl.bindTexture(gl.TEXTURE_2D, null);



        const shadow = gl.getUniformLocation(this.Program, "shadowTexture");
        gl.useProgram(this.Program);
        gl.uniform1i(shadow, 1);
        this.TimeUniform = gl.getUniformLocation(this.Program, "iTime");
        this.DistortionTimeUniform = gl.getUniformLocation(this.DistortionProgram, "iTime");


        this.DrawMesh();

        this.BlurTexture = Blur(gl, this.LineTexture!);

        this.PrepareForDrawLoop();
    }

    PrepareForDrawLoop() {
        // these settings should be permanent - they should not change at all inside Draw()
        const gl = this.GL;

        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        gl.useProgram(this.Program);

        gl.bindTexture(gl.TEXTURE_2D, this.LineTexture);
        gl.activeTexture(gl.TEXTURE1);
        gl.bindTexture(gl.TEXTURE_2D, this.BlurTexture);

        gl.bindBuffer(gl.ARRAY_BUFFER, this.QuadBuffer);
        gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
    }

    public Draw() {
        const gl = this.GL;
        gl.clear(gl.DEPTH_BUFFER_BIT);

        // Tell WebGL to use our program when drawing
        gl.uniform1f(this.TimeUniform, (Date.now() - this.StartTime) / 1000);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
    }

    DrawMesh() {
        const mesh = this.Mesh!;
        const gl = this.GL;

        const program = ShaderProgram(gl, vertexShaderSource, lineShaderSource);
        gl.useProgram(program);

        const position = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, position);
        gl.bufferData(gl.ARRAY_BUFFER, mesh.positions, gl.STATIC_DRAW);
        gl.vertexAttribPointer(0, 3, gl.FLOAT, false, 0, 0);
        gl.enableVertexAttribArray(0);

        const normal = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, normal);
        gl.bufferData(gl.ARRAY_BUFFER, mesh.normals, gl.STATIC_DRAW);
        gl.vertexAttribPointer(1, 2, gl.FLOAT, false, 0, 0);
        gl.enableVertexAttribArray(1);

        this.LineTexture = FrameBufferTexture(gl);

        gl.bindFramebuffer(gl.FRAMEBUFFER, this.Framebuffer);
        gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, this.LineTexture, 0);

        const depthBuffer = gl.createRenderbuffer();
        gl.bindRenderbuffer(gl.RENDERBUFFER, depthBuffer);
        gl.renderbufferStorage(gl.RENDERBUFFER, gl.DEPTH_COMPONENT16, gl.canvas.width, gl.canvas.height);
        gl.framebufferRenderbuffer(gl.FRAMEBUFFER, gl.DEPTH_ATTACHMENT, gl.RENDERBUFFER, depthBuffer);

        gl.clear(gl.DEPTH_BUFFER_BIT);

        gl.drawArrays(gl.TRIANGLES, 0, mesh.VertexCount);
    }

    TimeUniform: WebGLUniformLocation | null = null;
    DistortionTimeUniform: WebGLUniformLocation | null = null;
}
