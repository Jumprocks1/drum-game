import { ErrorOverlay } from "../../framework/ErrorOverlay";
import { Blur } from "../../utils/blur/Blur";
import { FrameBufferTexture, ShaderProgram } from "../../utils/GL";
import { Mesh } from "./LineMesh";
import shaderSource from "./Shader.frag"

type WebGL = WebGL2RenderingContext

const lineVertexShader = `#version 300 es
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
const lineFragShader = `#version 300 es
precision highp float;

in vec3 vPos;
in vec2 vNormal;
out vec4 fragColor;

void main(void) {
    fragColor = vec4(vNormal.xy / 2. + 0.5, 1.-vPos.z, 1.0);
}
`
const mainVertexShader = `#version 300 es
in vec2 position;
out highp vec2 vUv;
void main() {
    gl_Position = vec4(position,0.,1.0);
    vUv = position;
}
`;


export class LogoRenderer {

    get GL() {
        if (this._gl == null) throw "Failed to create WebGL context";
        return this._gl;
    }

    _gl: WebGL | null = null;


    QuadBuffer: WebGLBuffer | null = null // draws a simple -1,-1 to 1,1 quad

    Program: WebGLProgram | null = null;

    LineTexture: WebGLTexture | null = null; // line texture with normals

    ShadowTexture: WebGLTexture | null = null;

    Mesh: Mesh | null = null;

    StartTime: number = Date.now() - Math.random() * 1000_000;
    TimeUniform: WebGLUniformLocation | null = null;
    DistortionTimeUniform: WebGLUniformLocation | null = null;

    Init(gl: WebGL | null) {
        if (gl == null) throw "Failed to create WebGL context";

        this._gl = gl;
        // only need to call this on resize
        gl.viewport(0, 0, gl.canvas.width, gl.canvas.height);

        this.QuadBuffer = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, this.QuadBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 1, -1, -1, 1, 1, 1]), gl.STATIC_DRAW);

        this.Program = ShaderProgram(gl, mainVertexShader, shaderSource);

        const shadow = gl.getUniformLocation(this.Program, "shadowTexture");
        gl.useProgram(this.Program);
        gl.uniform1i(shadow, 1);
        this.TimeUniform = gl.getUniformLocation(this.Program, "iTime");

        gl.enable(gl.DEPTH_TEST);
        this.DrawMesh();

        this.ShadowTexture = Blur(gl, this.LineTexture!);

        this.PrepareForDrawLoop();
    }

    PrepareForDrawLoop() {
        // these settings should be permanent - they should not change at all inside Draw()
        const gl = this.GL;

        gl.disable(gl.DEPTH_TEST);

        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        gl.useProgram(this.Program);

        gl.bindTexture(gl.TEXTURE_2D, this.LineTexture);
        gl.activeTexture(gl.TEXTURE1);
        gl.bindTexture(gl.TEXTURE_2D, this.ShadowTexture);

        gl.bindBuffer(gl.ARRAY_BUFFER, this.QuadBuffer);
        gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
    }

    public Draw() {
        const gl = this.GL;
        // Tell WebGL to use our program when drawing
        gl.uniform1f(this.TimeUniform, (Date.now() - this.StartTime) / 1000);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
    }

    DrawMesh() {
        const mesh = this.Mesh!;
        const gl = this.GL;

        const framebuffer = gl.createFramebuffer();

        const program = ShaderProgram(gl, lineVertexShader, lineFragShader);
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

        gl.bindFramebuffer(gl.FRAMEBUFFER, framebuffer);
        gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, this.LineTexture, 0);

        const depthBuffer = gl.createRenderbuffer();
        gl.bindRenderbuffer(gl.RENDERBUFFER, depthBuffer);
        gl.renderbufferStorage(gl.RENDERBUFFER, gl.DEPTH_COMPONENT16, gl.canvas.width, gl.canvas.height);
        gl.framebufferRenderbuffer(gl.FRAMEBUFFER, gl.DEPTH_ATTACHMENT, gl.RENDERBUFFER, depthBuffer);

        gl.drawArrays(gl.TRIANGLES, 0, mesh.VertexCount);
    }
}
